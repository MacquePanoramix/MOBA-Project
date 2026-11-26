using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public enum PreviewStatField
    {
        AttackDamage = 0,
        Strength = 1,
        Agility = 2,
        Intelligence = 3,
        AttackSpeed = 4,
        AbilityHaste = 5,
        HealPower = 6,
        MaxHp = 7,
        MaxMana = 8,
        ManaRegen = 9,
        ResourceCostReduction = 10,
    }

    public readonly struct PreviewSliderSpec
    {
        public readonly PreviewStatField Field;
        public readonly string Label;
        public readonly string ColorHex;
        public readonly float Min;
        public readonly float Max;
        public readonly float Step;
        public readonly bool AsPercent;

        public PreviewSliderSpec(
            PreviewStatField field,
            string label,
            string colorHex,
            float min,
            float max,
            float step,
            bool asPercent = false)
        {
            Field = field;
            Label = label;
            ColorHex = colorHex;
            Min = min;
            Max = max;
            Step = step;
            AsPercent = asPercent;
        }
    }

    /// <summary>
    /// Isolated, self-contained in-engine ability preview & interactive stat sandbox.
    /// Runs its own separate LaneMatch + LaneWorld (offset at y = -260) and disabled Camera + RenderTexture
    /// so it never touches active match state, multiplayer commands, or main camera rendering.
    /// </summary>
    public sealed class LaneAbilityPreview : IDisposable
    {
        private readonly LaneTuning baseTuning;
        private readonly bool enableRenderVisuals;
        private readonly Vector3 previewWorldOffset = new Vector3(0f, -260f, 0f);
        private readonly List<(float atSec, Action act)> timeline = new List<(float, Action)>();
        private int timelineCursor;

        private LaneMatch sim;
        private LaneWorld world;
        private Camera previewCam;
        private RenderTexture renderTexture;
        private LaneSnapshot latestSnap;

        private float simAccum;
        private float elapsedInLoop;
        private float loopDuration = 4.2f;
        private int redHeroId;

        public bool IsActive { get; private set; }
        public bool IsPaused { get; set; }
        public LaneHeroId SelectedHero { get; private set; } = LaneHeroId.Ogre;
        public LaneAbilitySlot SelectedSlot { get; private set; } = LaneAbilitySlot.Q;
        public int ExampleIndex { get; private set; }
        public PreviewStatOverrides Overrides = DefaultOverrides(LaneAbilitySlot.Q);
        public float ElapsedInLoop => elapsedInLoop;
        public float LoopDuration => loopDuration;
        public int SimulationTickCount { get; private set; }
        public Texture PreviewTexture => renderTexture;
        public LaneSnapshot LatestPreviewSnapshot => latestSnap;
        public string ScenarioCaption { get; private set; } = string.Empty;

        public LaneAbilityPreview(LaneTuning sourceTuning, bool enableRenderVisuals = true)
        {
            baseTuning = sourceTuning ?? new LaneTuning();
            this.enableRenderVisuals = enableRenderVisuals && !Application.isBatchMode;
        }

        public static PreviewStatOverrides DefaultOverrides(LaneAbilitySlot slot)
        {
            return new PreviewStatOverrides
            {
                enabled = true,
                level = slot == LaneAbilitySlot.R ? 4 : 1,
                abilityRank = 1,
            };
        }

        public static string[] GetExampleNames(LaneHeroId hero, LaneAbilitySlot slot)
        {
            if (hero == LaneHeroId.Ogre && slot == LaneAbilitySlot.Q)
            {
                return new[] { "Ex 1: Central Stun", "Ex 2: Outer Shockwave" };
            }
            if (hero == LaneHeroId.Miravel && slot == LaneAbilitySlot.Q)
            {
                return new[] { "Ex 1: Ally Orb Pulse", "Ex 2: Enemy Orb Deny" };
            }
            return new[] { "Ability Demo" };
        }

        public static List<PreviewSliderSpec> GetRelevantSliders(LaneHeroId hero, LaneAbilitySlot slot)
        {
            var list = new List<PreviewSliderSpec>();
            void Add(PreviewStatField f)
            {
                switch (f)
                {
                    case PreviewStatField.AttackDamage:
                        list.Add(new PreviewSliderSpec(f, "+Attack Damage", LaneHeroSheet.ColAd, 0f, 160f, 5f));
                        break;
                    case PreviewStatField.Strength:
                        list.Add(new PreviewSliderSpec(f, "+Strength", LaneHeroSheet.ColStr, 0f, 180f, 5f));
                        break;
                    case PreviewStatField.Agility:
                        list.Add(new PreviewSliderSpec(f, "+Agility", LaneHeroSheet.ColAgi, 0f, 180f, 5f));
                        break;
                    case PreviewStatField.Intelligence:
                        list.Add(new PreviewSliderSpec(f, "+Intelligence", LaneHeroSheet.ColInt, 0f, 180f, 5f));
                        break;
                    case PreviewStatField.AttackSpeed:
                        list.Add(new PreviewSliderSpec(f, "+Attack Speed", LaneHeroSheet.ColAgi, 0f, 3.0f, 0.15f, asPercent: true));
                        break;
                    case PreviewStatField.AbilityHaste:
                        list.Add(new PreviewSliderSpec(f, "+Ability Haste", LaneHeroSheet.ColInt, 0f, 100f, 5f));
                        break;
                    case PreviewStatField.HealPower:
                        list.Add(new PreviewSliderSpec(f, "+Heal Power", LaneHeroSheet.ColHeal, 0f, 1.5f, 0.10f, asPercent: true));
                        break;
                    case PreviewStatField.MaxHp:
                        list.Add(new PreviewSliderSpec(f, "+Max HP", LaneHeroSheet.ColHp, 0f, 1500f, 50f));
                        break;
                }
            }

            switch (hero)
            {
                case LaneHeroId.Ogre:
                    if (slot == LaneAbilitySlot.None)
                    {
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.AttackSpeed);
                    }
                    else
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.Strength);
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.AttackSpeed);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    break;
                case LaneHeroId.Miravel:
                    if (slot == LaneAbilitySlot.None)
                    {
                        Add(PreviewStatField.HealPower);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.AttackSpeed);
                    }
                    else if (slot == LaneAbilitySlot.Q)
                    {
                        Add(PreviewStatField.HealPower);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.W)
                    {
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.E)
                    {
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.R)
                    {
                        Add(PreviewStatField.HealPower);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    break;
                case LaneHeroId.Swordsman:
                    if (slot == LaneAbilitySlot.None)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.AttackSpeed);
                    }
                    else if (slot == LaneAbilitySlot.Q)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.W)
                    {
                        Add(PreviewStatField.Intelligence);
                    }
                    else if (slot == LaneAbilitySlot.E)
                    {
                        Add(PreviewStatField.Strength);
                        Add(PreviewStatField.HealPower);
                    }
                    else if (slot == LaneAbilitySlot.R)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.Strength);
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    break;
                case LaneHeroId.LaserShooter:
                    if (slot == LaneAbilitySlot.None)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.AttackSpeed);
                        Add(PreviewStatField.Intelligence);
                    }
                    else if (slot == LaneAbilitySlot.Q)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.W)
                    {
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.E)
                    {
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    else if (slot == LaneAbilitySlot.R)
                    {
                        Add(PreviewStatField.AttackDamage);
                        Add(PreviewStatField.AttackSpeed);
                        Add(PreviewStatField.Intelligence);
                        Add(PreviewStatField.Agility);
                        Add(PreviewStatField.AbilityHaste);
                    }
                    break;
            }
            return list;
        }

        public static float GetOverrideValue(in PreviewStatOverrides ov, PreviewStatField field)
        {
            switch (field)
            {
                case PreviewStatField.AttackDamage: return ov.bonusAttackDamage;
                case PreviewStatField.Strength: return ov.bonusStrength;
                case PreviewStatField.Agility: return ov.bonusAgility;
                case PreviewStatField.Intelligence: return ov.bonusIntelligence;
                case PreviewStatField.AttackSpeed: return ov.bonusAttackSpeed;
                case PreviewStatField.AbilityHaste: return ov.bonusAbilityHaste;
                case PreviewStatField.HealPower: return ov.bonusHealPower;
                case PreviewStatField.MaxHp: return ov.bonusMaxHp;
                case PreviewStatField.MaxMana: return ov.bonusMaxMana;
                case PreviewStatField.ManaRegen: return ov.bonusManaRegen;
                case PreviewStatField.ResourceCostReduction: return ov.bonusResourceCostReduction;
                default: return 0f;
            }
        }

        public static void SetOverrideValue(ref PreviewStatOverrides ov, PreviewStatField field, float value)
        {
            ov.enabled = true;
            switch (field)
            {
                case PreviewStatField.AttackDamage: ov.bonusAttackDamage = value; break;
                case PreviewStatField.Strength: ov.bonusStrength = value; break;
                case PreviewStatField.Agility: ov.bonusAgility = value; break;
                case PreviewStatField.Intelligence: ov.bonusIntelligence = value; break;
                case PreviewStatField.AttackSpeed: ov.bonusAttackSpeed = value; break;
                case PreviewStatField.AbilityHaste: ov.bonusAbilityHaste = value; break;
                case PreviewStatField.HealPower: ov.bonusHealPower = value; break;
                case PreviewStatField.MaxHp: ov.bonusMaxHp = value; break;
                case PreviewStatField.MaxMana: ov.bonusMaxMana = value; break;
                case PreviewStatField.ManaRegen: ov.bonusManaRegen = value; break;
                case PreviewStatField.ResourceCostReduction: ov.bonusResourceCostReduction = value; break;
            }
        }

        public void Select(LaneHeroId hero, LaneAbilitySlot slot, int exampleIndex = 0)
        {
            bool changedHeroOrSlot = SelectedHero != hero || SelectedSlot != slot;
            SelectedHero = hero;
            SelectedSlot = slot;
            string[] exNames = GetExampleNames(hero, slot);
            ExampleIndex = Mathf.Clamp(exampleIndex, 0, exNames.Length - 1);
            if (changedHeroOrSlot)
            {
                Overrides.enabled = true;
                int maxRank = slot == LaneAbilitySlot.R ? 2 : 3;
                Overrides.abilityRank = Mathf.Clamp(Overrides.abilityRank <= 0 ? 1 : Overrides.abilityRank, 1, maxRank);
                Overrides.level = slot == LaneAbilitySlot.R ? Mathf.Max(4, Overrides.level) : Mathf.Clamp(Overrides.level <= 0 ? 1 : Overrides.level, 1, baseTuning.maxLevel);
            }
            RestartScenario();
        }

        public void ResetOverrides()
        {
            Overrides = DefaultOverrides(SelectedSlot);
            RestartScenario();
        }

        public void ApplyLiveOverrides(PreviewStatOverrides next)
        {
            bool structuralChange = next.level != Overrides.level || next.abilityRank != Overrides.abilityRank;
            Overrides = next;
            Overrides.enabled = true;
            if (sim != null)
            {
                sim.SetPreviewStatOverrides(LaneTeam.Blue, Overrides);
                latestSnap = sim.Snapshot();
                if (enableRenderVisuals && world != null)
                {
                    world.ApplySnapshot(latestSnap, LaneTeam.Blue, baseTuning, hoveredEntityId: 0);
                }
            }
            if (structuralChange && IsActive)
            {
                RestartScenario();
            }
        }

        public void RestartScenario()
        {
            EnsureRenderInfrastructure();

            var previewTuning = new LaneTuning
            {
                waveFirstSpawnDelay = 99999f,
                passiveGoldPerSecond = 0f,
                startingGold = 0,
                attackSpeedCurveK = baseTuning.attackSpeedCurveK,
                ogreImmuneDuringRecovery = baseTuning.ogreImmuneDuringRecovery,
            };

            LaneHeroId redHero = SelectedHero == LaneHeroId.Miravel ? LaneHeroId.Swordsman : LaneHeroId.Miravel;
            if (SelectedHero == LaneHeroId.LaserShooter && SelectedSlot == LaneAbilitySlot.E)
            {
                redHero = LaneHeroId.Miravel;
            }
            sim = new LaneMatch(SelectedHero, redHero, previewTuning);
            sim.DisableTowersForPreview();
            sim.SetPreviewStatOverrides(LaneTeam.Blue, Overrides);

            elapsedInLoop = 0f;
            simAccum = 0f;
            timelineCursor = 0;
            timeline.Clear();

            BuildScenarioScript();

            latestSnap = sim.Snapshot();
            if (enableRenderVisuals && world != null)
            {
                world.ResetVisuals();
                world.ApplySnapshot(latestSnap, LaneTeam.Blue, previewTuning, hoveredEntityId: 0);
            }
        }

        public void Tick(float dt, bool shouldRun)
        {
            if (!shouldRun)
            {
                Deactivate();
                return;
            }

            IsActive = true;
            if (sim == null)
            {
                RestartScenario();
            }

            if (!IsPaused)
            {
                float clampedDt = Mathf.Min(0.05f, dt);
                simAccum += clampedDt;
                const float fixedStep = 1f / 30f;
                int guard = 0;
                while (simAccum >= fixedStep && guard < 4)
                {
                    simAccum -= fixedStep;
                    guard++;
                    elapsedInLoop += fixedStep;
                    while (timelineCursor < timeline.Count && elapsedInLoop >= timeline[timelineCursor].atSec)
                    {
                        timeline[timelineCursor].act?.Invoke();
                        timelineCursor++;
                    }
                    sim.Tick(fixedStep);
                    SimulationTickCount++;
                    latestSnap = sim.Snapshot();
                    if (enableRenderVisuals && world != null)
                    {
                        world.ApplySnapshot(latestSnap, LaneTeam.Blue, baseTuning, hoveredEntityId: 0);
                    }
                    if (elapsedInLoop >= loopDuration)
                    {
                        RestartScenario();
                        break;
                    }
                }

                if (enableRenderVisuals && world != null)
                {
                    world.TickFrame(null, clampedDt);
                }
            }

            if (enableRenderVisuals && previewCam != null && renderTexture != null)
            {
                PositionCameraForSelection();
                previewCam.Render();
            }
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            simAccum = 0f;
        }

        public void Dispose()
        {
            Deactivate();
            if (world != null)
            {
                world.Dispose();
                world = null;
            }
            if (previewCam != null)
            {
                UnityEngine.Object.Destroy(previewCam.gameObject);
                previewCam = null;
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                renderTexture = null;
            }
        }

        public void DrawOverlayBars(Rect texRect, GUIStyle barTextStyle)
        {
            if (!enableRenderVisuals || previewCam == null || latestSnap == null) return;
            for (int i = 0; i < latestSnap.heroes.Count; i++)
            {
                HeroSnap h = latestSnap.heroes[i];
                if (h.phase == LanePhase.Dead) continue;
                Vector3 wp = previewWorldOffset + new Vector3(h.x, 2.25f, h.y);
                Vector3 vp = previewCam.WorldToViewportPoint(wp);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;

                float sx = texRect.x + vp.x * texRect.width;
                float sy = texRect.y + (1f - vp.y) * texRect.height;
                float bw = 54f;
                var bg = new Rect(sx - bw * 0.5f, sy, bw, 5f);
                Color prev = GUI.color;
                GUI.color = new Color(0.08f, 0.10f, 0.12f, 0.9f);
                GUI.DrawTexture(bg, Texture2D.whiteTexture);
                float fill = h.maxHp > 0f ? Mathf.Clamp01(h.hp / h.maxHp) : 0f;
                GUI.color = h.team == LaneTeam.Blue ? new Color(0.28f, 0.88f, 0.42f) : new Color(0.92f, 0.30f, 0.28f);
                GUI.DrawTexture(new Rect(bg.x + 1f, bg.y + 1f, (bw - 2f) * fill, 3f), Texture2D.whiteTexture);
                GUI.color = prev;

                string tag = h.team == LaneTeam.Blue ? "You" : "Target";
                if (h.stunTimer > 0f) tag += " [STUN]";
                else if (h.curseTimer > 0f) tag += $" [CURSE x{h.curseStacks}]";
                else if (h.laserStacks > 0) tag += $" [x{h.laserStacks}]";
                else if (h.wardTimer > 0f) tag += " [WARD]";
                if (h.slowTimer > 0f) tag += " [SLOW]";
                GUI.Label(new Rect(sx - 50f, sy - 15f, 100f, 14f), $"<color=#e8eff7>{tag}</color>", barTextStyle);
            }

            for (int i = 0; i < latestSnap.minions.Count; i++)
            {
                MinionSnap m = latestSnap.minions[i];
                Vector3 wp = previewWorldOffset + new Vector3(m.x, 1.35f, m.y);
                Vector3 vp = previewCam.WorldToViewportPoint(wp);
                if (vp.z <= 0f || vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f) continue;

                float sx = texRect.x + vp.x * texRect.width;
                float sy = texRect.y + (1f - vp.y) * texRect.height;
                float bw = 28f;
                Color prev = GUI.color;
                GUI.color = new Color(0.08f, 0.10f, 0.12f, 0.85f);
                GUI.DrawTexture(new Rect(sx - bw * 0.5f, sy, bw, 4f), Texture2D.whiteTexture);
                float fill = m.maxHp > 0f ? Mathf.Clamp01(m.hp / m.maxHp) : 0f;
                GUI.color = m.team == LaneTeam.Blue ? new Color(0.45f, 0.82f, 1f) : new Color(0.95f, 0.42f, 0.35f);
                GUI.DrawTexture(new Rect(bgX(sx, bw) + 1f, sy + 1f, (bw - 2f) * fill, 2f), Texture2D.whiteTexture);
                GUI.color = prev;
            }
        }

        private static float bgX(float sx, float bw) => sx - bw * 0.5f;

        private void EnsureRenderInfrastructure()
        {
            if (!enableRenderVisuals) return;

            if (world == null)
            {
                world = new LaneWorld(new LaneMap(baseTuning), previewWorldOffset, buildEnvironment: false, enableAudio: false);
            }
            if (renderTexture == null)
            {
                renderTexture = new RenderTexture(440, 260, 16, RenderTextureFormat.ARGB32)
                {
                    name = "HeroGuidePreviewRT",
                    filterMode = FilterMode.Bilinear,
                };
                renderTexture.Create();
            }
            if (previewCam == null)
            {
                var camGo = new GameObject("HeroGuidePreviewCamera");
                previewCam = camGo.AddComponent<Camera>();
                previewCam.enabled = false;
                previewCam.targetTexture = renderTexture;
                previewCam.clearFlags = CameraClearFlags.SolidColor;
                previewCam.backgroundColor = new Color(0.06f, 0.08f, 0.11f, 1f);
                previewCam.fieldOfView = 42f;
                previewCam.nearClipPlane = 0.2f;
                previewCam.farClipPlane = 60f;
            }
        }

        private void PositionCameraForSelection()
        {
            Vector3 focus = previewWorldOffset;
            if (SelectedHero == LaneHeroId.Ogre && SelectedSlot == LaneAbilitySlot.W)
                focus += new Vector3(1.8f, 0f, 0f);
            else if (SelectedHero == LaneHeroId.LaserShooter && SelectedSlot == LaneAbilitySlot.R)
                focus += new Vector3(1.4f, 0f, 0f);
            previewCam.transform.position = focus + new Vector3(0f, 10.6f, -9.0f);
            previewCam.transform.LookAt(focus + new Vector3(0f, 0.4f, 0f));
        }

        private void At(float sec, Action act) => timeline.Add((sec, act));

        private void BuildScenarioScript()
        {
            switch (SelectedHero)
            {
                case LaneHeroId.Ogre: BuildOgreScript(); break;
                case LaneHeroId.Miravel: BuildMiravelScript(); break;
                case LaneHeroId.Swordsman: BuildSwordsmanScript(); break;
                case LaneHeroId.LaserShooter: BuildLaserScript(); break;
            }
        }

        private void BuildOgreScript()
        {
            sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.2f, 0f), 90f);
            redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f);

            switch (SelectedSlot)
            {
                case LaneAbilitySlot.None:
                    loopDuration = 3.8f;
                    ScenarioCaption = "Ogre winds up E while Miravel fires Q/W: CC-Immune animation absorbs stun; AGI speeds wind-up, AS speeds recovery.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(1.4f, 0f) }));
                    At(0.45f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = 1 }));
                    break;

                case LaneAbilitySlot.Q:
                    loopDuration = 3.6f;
                    if (ExampleIndex == 1)
                    {
                        redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.8f, 1.35f), 270f);
                        sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.8f, -1.40f), maxHp: 600f);
                        ScenarioCaption = "Outer Shockwave zone (STR widens): deals reduced damage with zero stun.";
                    }
                    else
                    {
                        redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.0f, 0f), 270f);
                        ScenarioCaption = "Central Club Slam (AD dmg, STR stun): full physical damage + central stun.";
                    }
                    At(0.30f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2.4f, 0f) }));
                    break;

                case LaneAbilitySlot.W:
                    loopDuration = 3.5f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.2f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(-1.7f, 0f), 270f);
                    ScenarioCaption = "Club Sweep (AD dmg, STR sqrt knockback): hurls enemy backward with no artificial push cap.";
                    At(0.30f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1.0f, 0f) }));
                    break;

                case LaneAbilitySlot.E:
                    loopDuration = 3.5f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(0f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.15f, 0f), 270f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(-1.8f, 1.1f), maxHp: 500f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(-1.4f, -1.4f), maxHp: 500f);
                    ScenarioCaption = "Quake Stomp (STR radius + stun): stuns uniformly across the entire scaled circle.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.right }));
                    break;

                case LaneAbilitySlot.R:
                    loopDuration = 5.4f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.2f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
                    ScenarioCaption = "Rampage Combo (STR size): grows Ogre & enlarges Q/W/E, executing queued Q→W→Q→W ending in 1.85x CRIT.";
                    At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = Vector2.right }));
                    At(0.55f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(1.0f, 0f) }));
                    At(0.95f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(2.0f, 0f) }));
                    At(1.35f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2.0f, 0f) }));
                    At(1.75f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(3.5f, 0f) }));
                    break;
            }
        }

        private void BuildMiravelScript()
        {
            switch (SelectedSlot)
            {
                case LaneAbilitySlot.None:
                    loopDuration = 4.4f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.72f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.2f, 0f), 270f, hpFraction: 0.35f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Blue, new Vector2(-3.2f, 1.4f), maxHp: 300f, hpFraction: 0.55f);
                    ScenarioCaption = "Vitality Drain Aura (INT radius, Heal Power strength): heals allies while pushing enemy regen negative.";
                    At(0.40f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    At(1.40f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Stop }));
                    break;

                case LaneAbilitySlot.Q:
                    loopDuration = 3.8f;
                    if (ExampleIndex == 1)
                    {
                        sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.2f, 0f), 90f);
                        redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.2f, 0f), 270f, hpFraction: 0.8f);
                        sim.SpawnPreviewOrb(LaneTeam.Blue, new Vector2(-0.5f, 0f));
                        ScenarioCaption = "Enemy Orb Deny: stepping onto an uncollected orb crushes it before Miravel arrives.";
                        At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(-0.5f, 0f) }));
                    }
                    else
                    {
                        sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-3.0f, 0f), 90f, hpFraction: 0.60f);
                        redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f, hpFraction: 0.85f);
                        sim.SpawnPreviewDummyMinion(LaneTeam.Blue, new Vector2(-0.2f, 1.6f), maxHp: 300f, hpFraction: 0.50f);
                        sim.SpawnPreviewOrb(LaneTeam.Blue, new Vector2(-0.4f, 0f));
                        ScenarioCaption = "Ally Orb Pulse (Heal Power heal/dmg, INT radius): touching orb pulses healing & magic damage.";
                        At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-0.2f, 0f) }));
                    }
                    break;

                case LaneAbilitySlot.W:
                    loopDuration = 4.4f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-3.2f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(3.6f, 0f), 270f);
                    ScenarioCaption = "Shimmer Decoy (fixed durations): turns Miravel invisible while sending decoy to cursor spot.";
                    At(0.30f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(2.2f, 0.2f) }));
                    At(0.75f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-1.6f, -2.2f) }));
                    break;

                case LaneAbilitySlot.E:
                    loopDuration = 4.6f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.0f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.8f, 0f), 270f);
                    ScenarioCaption = "Retribution Ward (INT Curse DPS): attacker becomes Cursed; casting enemy abilities escalates stacks & timer.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = Vector2.zero }));
                    At(0.65f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = 1 }));
                    At(1.55f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.CastQ, point = new Vector2(-2.0f, 0f) }));
                    At(2.35f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Stop }));
                    break;

                case LaneAbilitySlot.R:
                    loopDuration = 4.8f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.4f, 0f), 90f, hpFraction: 0.65f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.4f, 0f), 270f, hpFraction: 0.85f);
                    sim.SpawnPreviewOrb(LaneTeam.Blue, new Vector2(-2.2f, 0f));
                    ScenarioCaption = "Ramping Sanctum (INT radius, Bonus Heal Power caps): ramps ally heal amplification & enemy vulnerability + slow.";
                    At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(1.0f, 0f) }));
                    At(1.10f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(-2.0f, 0f) }));
                    break;
            }
        }

        private void BuildSwordsmanScript()
        {
            sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.72f);
            redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.45f, 0f), 270f);

            switch (SelectedSlot)
            {
                case LaneAbilitySlot.None:
                    loopDuration = 3.6f;
                    ScenarioCaption = "Twin-Blade (AS + AGI): 1st light sabre slash [W] followed by heavy greatsword cleave [E].";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    break;

                case LaneAbilitySlot.Q:
                    loopDuration = 3.5f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-2.0f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f);
                    ScenarioCaption = "Hamstring Arc (AD dmg, AGI wind-up, fixed 0.24s rec): cleaves arc, slows, and marks target for R.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(2.0f, 0f) }));
                    At(0.90f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.Move, point = new Vector2(4.2f, 0f) }));
                    break;

                case LaneAbilitySlot.W:
                    loopDuration = 3.8f;
                    ScenarioCaption = "Stance Temper (1st Light Hit): builds self Arm/MR & shreds enemy Arm/MR (INT scales separate timers).";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    break;

                case LaneAbilitySlot.E:
                    loopDuration = 3.8f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.4f, 0f), 90f, hpFraction: 0.45f);
                    ScenarioCaption = "Vital Draw (2nd Heavy Hit): STR % Max HP magic burst + Heal Power self healing halo.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    break;

                case LaneAbilitySlot.R:
                    loopDuration = 4.2f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-1.6f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(0.4f, 0f), 270f, hpFraction: 0.22f);
                    ScenarioCaption = "Finishing Cross (AD dmg, STR execute, AGI dash speed, INT mark): marks with Q then dashes through for reset.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(1.0f, 0f) }));
                    At(1.15f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(1.4f, 0f) }));
                    break;
            }
        }

        private void BuildLaserScript()
        {
            switch (SelectedSlot)
            {
                case LaneAbilitySlot.None:
                    loopDuration = 3.8f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.8f, 0f), 90f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(-0.5f, 0.2f), maxHp: 700f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(1.8f, 1.5f), maxHp: 700f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(3.6f, -0.4f), maxHp: 700f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(4.6f, 1.4f), 270f);
                    ScenarioCaption = "Chain Laser Passive (INT bounce distance & stack cap): endless non-repeating hops stack laser vulnerability.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    break;

                case LaneAbilitySlot.Q:
                    loopDuration = 3.6f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.4f, 0f), 90f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(-1.2f, 0f), maxHp: 45f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.8f, 0f), maxHp: 45f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.6f, 0f), 270f);
                    ScenarioCaption = "Extending Line Beam (AD dmg, INT sqrt extension speed): sweeps across line with escalating multi-kill bonus gold.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastQ, point = new Vector2(4.0f, 0f) }));
                    break;

                case LaneAbilitySlot.W:
                    loopDuration = 4.2f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.0f, 0f), 90f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.6f, 1.1f), maxHp: 700f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.6f, -1.1f), maxHp: 700f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(1.5f, 0f), 270f);
                    ScenarioCaption = "Resonant Stack Shock (INT dmg & radius, bounded+sqrt overlapping slows): chain basics then detonate W.";
                    At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.AttackHero, targetId = redHeroId }));
                    At(1.30f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastW, point = new Vector2(1.5f, 0f) }));
                    break;

                case LaneAbilitySlot.E:
                    loopDuration = 4.4f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-0.5f, 0f), 90f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.2f, 0f), 270f);
                    ScenarioCaption = "Hologram Reposition (AGI step speed, INT zap, rank-only stun): calm step-back spawns blocking stun decoy.";
                    At(0.25f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastE, point = new Vector2(2.2f, 0f) }));
                    At(0.75f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Red, kind = LaneOrderKind.AttackHero, targetId = 1 }));
                    break;

                case LaneAbilitySlot.R:
                    loopDuration = 5.0f;
                    sim.ConfigurePreviewHero(LaneTeam.Blue, new Vector2(-4.0f, -1.2f), 90f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(0.5f, 0.5f), maxHp: 900f);
                    sim.SpawnPreviewDummyMinion(LaneTeam.Red, new Vector2(2.0f, -0.6f), maxHp: 900f);
                    redHeroId = sim.ConfigurePreviewHero(LaneTeam.Red, new Vector2(2.8f, 0.6f), 270f);
                    ScenarioCaption = "Overdrive Prism Beam (INT dur, AS proc cadence, AGI move %): sustained moving beam fires independent chain procs.";
                    At(0.20f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.CastR, point = new Vector2(4.0f, 0.2f) }));
                    At(0.90f, () => sim.EnqueueOrApplyOrder(new LaneOrder { team = LaneTeam.Blue, kind = LaneOrderKind.Move, point = new Vector2(-2.2f, 1.4f) }));
                    break;
            }
        }
    }
}
