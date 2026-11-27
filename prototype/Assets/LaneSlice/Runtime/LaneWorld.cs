using System.Collections.Generic;
using Moba.Duel;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Stylized 3D lane renderer, telegraphs, heal/orb halos, smooth 30Hz interpolation,
    /// hover highlights, and toggleable Locked/Free MOBA camera.
    /// </summary>
    public sealed partial class LaneWorld
    {
        private readonly Transform root;
        private readonly Vector3 worldOffset;
        private readonly bool enableAudio;
        private readonly Material vertexMat;
        private readonly Shader spritesDefaultShader;
        private readonly Mesh meleeMinionMesh;
        private readonly Mesh rangedMinionMesh;
        private readonly Mesh orbMesh;
        private readonly Mesh projectileMesh;
        private readonly Mesh ringMesh;
        private readonly Mesh quadMesh;
        private readonly Mesh ogreWConeMesh;
        private readonly Mesh swordQConeMesh;
        private readonly List<Mesh> ownedMeshes = new List<Mesh>();
        private readonly List<Material> ownedMaterials = new List<Material>();
        private readonly List<AudioClip> ownedClips = new List<AudioClip>();

        private readonly Dictionary<int, EntityVisual> heroVisuals = new Dictionary<int, EntityVisual>();
        private readonly Dictionary<int, HeroTelegraph> heroTelegraphs = new Dictionary<int, HeroTelegraph>();
        private readonly Dictionary<int, EntityVisual> minionVisuals = new Dictionary<int, EntityVisual>();
        private readonly Dictionary<int, EntityVisual> illusionVisuals = new Dictionary<int, EntityVisual>();
        private readonly Dictionary<int, GameObject> projVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> orbVisuals = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, TowerVisual> towerVisuals = new Dictionary<int, TowerVisual>();
        private readonly List<GameObject> zoneVisuals = new List<GameObject>();
        private readonly List<GameObject> selectionRings = new List<GameObject>();
        private readonly List<TransientBurst> bursts = new List<TransientBurst>();
        private readonly List<TransientBeam> transientBeams = new List<TransientBeam>();
        private readonly Stack<TransientBurst> burstPool = new Stack<TransientBurst>();
        private readonly Stack<TransientBeam> beamPool = new Stack<TransientBeam>();

        private readonly GameObject hoverRing;
        private readonly GameObject swordMarkRing;

        private readonly AudioSource sfxSource;
        private readonly AudioClip sfxSwordLight;
        private readonly AudioClip sfxSwordHeavy;
        private readonly AudioClip sfxOgreCentral;
        private readonly AudioClip sfxOgreShockwave;

        private bool cameraLocked = true;
        private Vector3 camFocus = Vector3.zero;
        private Vector3 lastMouseDragPos;
        private bool middleDragging;
        private float camShakeAmp;
        private float camShakeTimer;
        private readonly Vector3 camOffset = new Vector3(0f, 17.5f, -13.2f);
        private readonly Vector3 camEuler = new Vector3(53f, 0f, 0f);

        private sealed class HeroTelegraph
        {
            public GameObject beam;
            public GameObject beamOuter;
            public GameObject laserBeam;
            public GameObject ogreW;
            public GameObject swordQ;
            public GameObject ring;
            public GameObject cowardHalo;
            public Material cowardHaloMat;
            public GameObject auraRing;
            public GameObject drainRing;
            public GameObject wardRing;
            public GameObject curseRing;
            public GameObject shieldRing;
            public readonly List<Material> ownedMats = new List<Material>();
        }

        private sealed class EntityVisual
        {
            public GameObject go;
            public MeshRenderer mr;
            public MeshRenderer[] renderers;
            public HeroRig rig;
            public LaneHeroId heroId;
            public MaterialPropertyBlock mpb;
            public Vector3 targetPos;
            public float targetYaw;
            public float scale = 1f;
            public float alpha = 1f;
            public bool initialized;

            public LanePhase phase;
            public LaneAbilitySlot activeSlot;
            public float phaseTimer;
            public float phaseDuration;
            public float simSpeed;
            public int rStrikeIndex;
            public bool rStrikeIsFinal;
            public bool isTwinSecondStrike;
            public float laserStepProgress;
            public int lastBasicAlt;
            public float basicPunchTimer;
            public float walkCycle;
        }

        private sealed class TowerVisual
        {
            public GameObject aliveObj;
            public GameObject ruinObj;
            public Transform crystal;
            public LineRenderer aggroBeam;
            public Material aggroBeamMat;
            public Color normalBeamColor;
        }

        private sealed class TransientBurst
        {
            public GameObject go;
            public Material mat;
            public float age;
            public float duration;
            public float startScale;
            public float endScale;
            public float riseSpeed;
            public Color baseColor;
        }

        private sealed class TransientBeam
        {
            public GameObject go;
            public LineRenderer lr;
            public Material mat;
            public float age;
            public float duration;
            public float startWidth;
            public Color baseColor;
        }

        public bool CameraLocked
        {
            get => cameraLocked;
            set => cameraLocked = value;
        }

        public Vector3 WorldOffset => worldOffset;

        private Vector3 ToWorld(float x, float z, float y = 0f) => worldOffset + new Vector3(x, y, z);

        public LaneWorld(LaneMap map) : this(map, Vector3.zero, buildEnvironment: true, enableAudio: true)
        {
        }

        public LaneWorld(LaneMap map, Vector3 worldOffset, bool buildEnvironment = true, bool enableAudio = true)
        {
            this.worldOffset = worldOffset;
            this.enableAudio = enableAudio;
            root = new GameObject(buildEnvironment ? "LaneWorldRoot" : "LanePreviewWorldRoot").transform;
            Shader fieldShader = Resources.Load<Shader>("DuelField") ?? Shader.Find("Sprites/Default");
            spritesDefaultShader = Shader.Find("Sprites/Default") ?? fieldShader;
            vertexMat = new Material(fieldShader);
            ownedMaterials.Add(vertexMat);

            meleeMinionMesh = BuildMinionMesh(ranged: false);
            rangedMinionMesh = BuildMinionMesh(ranged: true);
            orbMesh = BuildOrbMesh();
            projectileMesh = BuildProjectileMesh();
            ringMesh = BuildRingMesh(0.85f, 1.0f, 28);
            quadMesh = BuildForwardBeamMesh();
            ogreWConeMesh = BuildConeMesh(0.15f, 16);
            swordQConeMesh = BuildConeMesh(0.35f, 14);
            ownedMeshes.Add(meleeMinionMesh);
            ownedMeshes.Add(rangedMinionMesh);
            ownedMeshes.Add(orbMesh);
            ownedMeshes.Add(projectileMesh);
            ownedMeshes.Add(ringMesh);
            ownedMeshes.Add(quadMesh);
            ownedMeshes.Add(ogreWConeMesh);
            ownedMeshes.Add(swordQConeMesh);

            if (buildEnvironment)
            {
                BuildEnvironment(map);
                BuildTowers(map);
            }
            else
            {
                BuildPreviewFloor();
            }

            hoverRing = CreateUnlitMesh("HoverRing", ringMesh, new Color(1f, 0.92f, 0.35f, 0.92f));
            hoverRing.SetActive(false);

            swordMarkRing = CreateUnlitMesh("SwordMarkRing", ringMesh, new Color(0.95f, 0.22f, 0.25f, 0.90f));
            swordMarkRing.SetActive(false);

            if (enableAudio && !Application.isBatchMode)
            {
                sfxSource = root.gameObject.AddComponent<AudioSource>();
                sfxSource.spatialBlend = 0f;
                sfxSource.volume = 0.28f;
                sfxSwordLight = BuildToneClip("SwordLightSfx", 680f, 1180f, 0.12f, isImpactNoise: false);
                sfxSwordHeavy = BuildToneClip("SwordHeavySfx", 210f, 95f, 0.24f, isImpactNoise: true);
                sfxOgreCentral = BuildToneClip("OgreCentralSfx", 115f, 44f, 0.30f, isImpactNoise: true);
                sfxOgreShockwave = BuildToneClip("OgreShockwaveSfx", 260f, 120f, 0.22f, isImpactNoise: false);
                ownedClips.Add(sfxSwordLight);
                ownedClips.Add(sfxSwordHeavy);
                ownedClips.Add(sfxOgreCentral);
                ownedClips.Add(sfxOgreShockwave);
            }
        }

        public void SetRootActive(bool active)
        {
            if (root != null && root.gameObject.activeSelf != active)
            {
                root.gameObject.SetActive(active);
            }
        }

        public void Dispose()
        {
            ResetVisuals();
            while (burstPool.Count > 0)
            {
                TransientBurst b = burstPool.Pop();
                if (b.go) Object.Destroy(b.go);
                if (b.mat) Object.Destroy(b.mat);
            }
            while (beamPool.Count > 0)
            {
                TransientBeam tb = beamPool.Pop();
                if (tb.go) Object.Destroy(tb.go);
                if (tb.mat) Object.Destroy(tb.mat);
            }
            if (root != null) Object.Destroy(root.gameObject);
            for (int i = 0; i < ownedMeshes.Count; i++) if (ownedMeshes[i]) Object.Destroy(ownedMeshes[i]);
            for (int i = 0; i < ownedMaterials.Count; i++) if (ownedMaterials[i]) Object.Destroy(ownedMaterials[i]);
            for (int i = 0; i < ownedClips.Count; i++) if (ownedClips[i]) Object.Destroy(ownedClips[i]);
            ownedMeshes.Clear();
            ownedMaterials.Clear();
            ownedClips.Clear();
            heroRigMeshCache.Clear();
        }

        private static AudioClip BuildToneClip(string name, float freqStart, float freqEnd, float seconds, bool isImpactNoise)
        {
            const int sr = 22050;
            int samples = Mathf.Max(256, Mathf.RoundToInt(sr * seconds));
            var data = new float[samples];
            float phase = 0f;
            uint rng = (uint)Mathf.Abs(name.GetHashCode()) + 17u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = Mathf.Lerp(freqStart, freqEnd, t);
                phase += (2f * Mathf.PI * freq) / sr;
                float env = Mathf.Pow(1f - t, isImpactNoise ? 1.8f : 2.4f);
                rng = rng * 1664525u + 1013904223u;
                float noise = ((rng & 0xFFFFu) / 32768f - 1f) * (isImpactNoise ? 0.38f : 0.14f);
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.72f + noise) * env, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void SoftResetForPreviewLoop()
        {
            foreach (var kv in heroVisuals)
            {
                EntityVisual v = kv.Value;
                if (v.go) v.go.SetActive(false);
                v.initialized = false;
                v.phaseTimer = 0f;
                v.basicPunchTimer = 0f;
                v.walkCycle = 0f;
                v.lastBasicAlt = 0;
            }
            foreach (var kv in heroTelegraphs)
            {
                HeroTelegraph t = kv.Value;
                if (t.beam) t.beam.SetActive(false);
                if (t.beamOuter) t.beamOuter.SetActive(false);
                if (t.laserBeam) t.laserBeam.SetActive(false);
                if (t.ogreW) t.ogreW.SetActive(false);
                if (t.swordQ) t.swordQ.SetActive(false);
                if (t.ring) t.ring.SetActive(false);
                if (t.cowardHalo) t.cowardHalo.SetActive(false);
                if (t.auraRing) t.auraRing.SetActive(false);
                if (t.drainRing) t.drainRing.SetActive(false);
                if (t.wardRing) t.wardRing.SetActive(false);
                if (t.curseRing) t.curseRing.SetActive(false);
                if (t.shieldRing) t.shieldRing.SetActive(false);
            }
            foreach (var kv in minionVisuals) if (kv.Value.go) Object.Destroy(kv.Value.go);
            foreach (var kv in illusionVisuals) if (kv.Value.go) Object.Destroy(kv.Value.go);
            foreach (var kv in projVisuals) if (kv.Value) Object.Destroy(kv.Value);
            foreach (var kv in orbVisuals) if (kv.Value) Object.Destroy(kv.Value);
            minionVisuals.Clear();
            illusionVisuals.Clear();
            projVisuals.Clear();
            orbVisuals.Clear();

            for (int i = bursts.Count - 1; i >= 0; i--) RecycleBurst(bursts[i]);
            bursts.Clear();
            for (int i = transientBeams.Count - 1; i >= 0; i--) RecycleBeam(transientBeams[i]);
            transientBeams.Clear();
            for (int i = 0; i < zoneVisuals.Count; i++) if (zoneVisuals[i]) zoneVisuals[i].SetActive(false);
            for (int i = 0; i < selectionRings.Count; i++) if (selectionRings[i]) selectionRings[i].SetActive(false);

            camShakeAmp = 0f;
            camShakeTimer = 0f;
            if (hoverRing) hoverRing.SetActive(false);
            if (swordMarkRing) swordMarkRing.SetActive(false);
        }

        public void ResetVisuals()
        {
            foreach (var kv in heroVisuals) if (kv.Value.go) Object.Destroy(kv.Value.go);
            foreach (var kv in heroTelegraphs)
            {
                if (kv.Value.beam) Object.Destroy(kv.Value.beam);
                if (kv.Value.beamOuter) Object.Destroy(kv.Value.beamOuter);
                if (kv.Value.laserBeam) Object.Destroy(kv.Value.laserBeam);
                if (kv.Value.ogreW) Object.Destroy(kv.Value.ogreW);
                if (kv.Value.swordQ) Object.Destroy(kv.Value.swordQ);
                if (kv.Value.ring) Object.Destroy(kv.Value.ring);
                if (kv.Value.cowardHalo) Object.Destroy(kv.Value.cowardHalo);
                if (kv.Value.auraRing) Object.Destroy(kv.Value.auraRing);
                if (kv.Value.drainRing) Object.Destroy(kv.Value.drainRing);
                if (kv.Value.wardRing) Object.Destroy(kv.Value.wardRing);
                if (kv.Value.curseRing) Object.Destroy(kv.Value.curseRing);
                if (kv.Value.shieldRing) Object.Destroy(kv.Value.shieldRing);
                for (int m = 0; m < kv.Value.ownedMats.Count; m++)
                {
                    if (kv.Value.ownedMats[m]) Object.Destroy(kv.Value.ownedMats[m]);
                }
            }
            foreach (var kv in minionVisuals) if (kv.Value.go) Object.Destroy(kv.Value.go);
            foreach (var kv in illusionVisuals) if (kv.Value.go) Object.Destroy(kv.Value.go);
            foreach (var kv in projVisuals) if (kv.Value) Object.Destroy(kv.Value);
            foreach (var kv in orbVisuals) if (kv.Value) Object.Destroy(kv.Value);
            foreach (var b in bursts) RecycleBurst(b);
            foreach (var tb in transientBeams) RecycleBeam(tb);
            for (int i = 0; i < zoneVisuals.Count; i++) if (zoneVisuals[i]) Object.Destroy(zoneVisuals[i]);
            for (int i = 0; i < selectionRings.Count; i++) if (selectionRings[i]) Object.Destroy(selectionRings[i]);

            heroVisuals.Clear();
            heroTelegraphs.Clear();
            minionVisuals.Clear();
            illusionVisuals.Clear();
            projVisuals.Clear();
            orbVisuals.Clear();
            bursts.Clear();
            transientBeams.Clear();
            zoneVisuals.Clear();
            selectionRings.Clear();
            camShakeAmp = 0f;
            camShakeTimer = 0f;
            if (hoverRing) hoverRing.SetActive(false);
            if (swordMarkRing) swordMarkRing.SetActive(false);
        }

        public void ApplySnapshot(
            LaneSnapshot snap,
            LaneTeam localTeam,
            LaneTuning tuning,
            int hoveredEntityId,
            IReadOnlyList<int> selectedUnitIds = null)
        {
            if (snap == null) return;

            HeroSnap localHero = null;
            HeroSnap markSource = null;

            // Heroes
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                HeroSnap h = snap.heroes[i];
                if (h.team == localTeam) localHero = h;
                if (h.heroId == LaneHeroId.Swordsman && h.team == localTeam && h.swordMarkedTargetId > 0)
                {
                    markSource = h;
                }

                if (!heroVisuals.TryGetValue(h.id, out EntityVisual vis) || vis.heroId != h.heroId)
                {
                    if (vis != null && vis.go) Object.Destroy(vis.go);
                    HeroRig rig = BuildHeroRig(h.heroId, $"Hero_{h.heroId}_{h.team}", root, vertexMat);
                    vis = new EntityVisual
                    {
                        go = rig.root.gameObject,
                        rig = rig,
                        heroId = h.heroId,
                        renderers = rig.renderers,
                        mpb = new MaterialPropertyBlock(),
                    };
                    heroVisuals[h.id] = vis;
                }

                bool hiddenEnemy = h.invisible && h.team != localTeam;
                vis.go.SetActive(h.phase != LanePhase.Dead && !hiddenEnemy);
                vis.targetPos = ToWorld(h.x, h.y);
                vis.targetYaw = h.headingDeg;
                vis.scale = Mathf.Max(0.85f, h.scale);

                vis.phase = h.phase;
                vis.activeSlot = h.activeSlot;
                vis.phaseTimer = h.phaseTimer;
                vis.phaseDuration = Mathf.Max(0.01f, h.phaseDuration);
                vis.simSpeed = h.speed;
                vis.rStrikeIndex = h.rStrikeIndex;
                vis.rStrikeIsFinal = h.rStrikeIsFinal;
                vis.isTwinSecondStrike = h.isTwinSecondStrike;
                vis.laserStepProgress = h.laserStepProgress;
                if (h.basicSwingAlt != vis.lastBasicAlt)
                {
                    vis.lastBasicAlt = h.basicSwingAlt;
                    vis.basicPunchTimer = 0.24f;
                }

                Color tint = h.team == LaneTeam.Blue ? new Color(0.84f, 0.94f, 1f, 1f) : new Color(1f, 0.85f, 0.84f, 1f);
                if (h.stunTimer > 0f) tint = new Color(1f, 0.94f, 0.45f, 1f);
                else if (h.curseTimer > 0f) tint = new Color(0.75f, 0.50f, 1f, 1f);
                else if (h.wardTimer > 0f) tint = new Color(0.55f, 1f, 0.88f, 1f);
                if (h.invisible && h.team == localTeam) tint = new Color(0.50f, 0.88f, 0.96f, 1f);

                SetEntityTint(vis, tint);
                if (!vis.initialized)
                {
                    vis.go.transform.position = vis.targetPos;
                    vis.go.transform.rotation = Quaternion.Euler(0f, vis.targetYaw, 0f);
                    vis.initialized = true;
                }

                UpdateHeroTelegraph(h, tuning, visibleToLocal: h.phase != LanePhase.Dead && !hiddenEnemy);
            }

            // Illusions
            var activeIlls = new HashSet<int>();
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                IllusionSnap ill = snap.illusions[i];
                activeIlls.Add(ill.id);
                if (!illusionVisuals.TryGetValue(ill.id, out EntityVisual vis))
                {
                    LaneHeroId illHero = ill.isLaserDecoy ? LaneHeroId.LaserShooter : LaneHeroId.Miravel;
                    HeroRig rig = BuildHeroRig(illHero, $"Illusion_{ill.id}", root, vertexMat);
                    vis = new EntityVisual
                    {
                        go = rig.root.gameObject,
                        rig = rig,
                        heroId = illHero,
                        renderers = rig.renderers,
                        mpb = new MaterialPropertyBlock(),
                    };
                    illusionVisuals[ill.id] = vis;
                }
                vis.go.SetActive(true);
                vis.targetPos = ToWorld(ill.x, ill.y);
                vis.targetYaw = ill.headingDeg;
                vis.simSpeed = ill.speed;
                Color illTint = ill.isLaserDecoy
                    ? new Color(0.28f, 0.92f, 1f, 0.72f)
                    : (ill.team == localTeam ? new Color(0.55f, 0.95f, 1f, 0.65f) : Color.white);
                SetEntityTint(vis, illTint);
                if (!vis.initialized)
                {
                    vis.go.transform.position = vis.targetPos;
                    vis.go.transform.rotation = Quaternion.Euler(0f, vis.targetYaw, 0f);
                    vis.initialized = true;
                }
            }
            CleanupStaleVisuals(illusionVisuals, activeIlls);

            // Minions
            var activeMinions = new HashSet<int>();
            for (int i = 0; i < snap.minions.Count; i++)
            {
                MinionSnap m = snap.minions[i];
                activeMinions.Add(m.id);
                if (!minionVisuals.TryGetValue(m.id, out EntityVisual vis))
                {
                    vis = CreateEntityVisual($"Minion_{m.id}", m.ranged ? rangedMinionMesh : meleeMinionMesh);
                    minionVisuals[m.id] = vis;
                }
                vis.go.SetActive(true);
                vis.targetPos = ToWorld(m.x, m.y);
                vis.targetYaw = m.headingDeg;
                Color c = m.team == LaneTeam.Blue ? new Color(0.62f, 0.84f, 1f, 1f) : new Color(1f, 0.64f, 0.60f, 1f);
                vis.mpb.SetColor("_Color", c);
                vis.mr.SetPropertyBlock(vis.mpb);
                if (!vis.initialized)
                {
                    vis.go.transform.position = vis.targetPos;
                    vis.go.transform.rotation = Quaternion.Euler(0f, vis.targetYaw, 0f);
                    vis.initialized = true;
                }
            }
            CleanupStaleVisuals(minionVisuals, activeMinions);

            // Projectiles
            var activeProjs = new HashSet<int>();
            for (int i = 0; i < snap.projectiles.Count; i++)
            {
                ProjectileSnap p = snap.projectiles[i];
                activeProjs.Add(p.id);
                if (!projVisuals.TryGetValue(p.id, out GameObject go))
                {
                    Color c = p.kind == LaneProjKind.TowerShell
                        ? new Color(1f, 0.72f, 0.25f, 1f)
                        : (p.team == LaneTeam.Blue ? new Color(0.45f, 0.92f, 1f, 1f) : new Color(1f, 0.52f, 0.42f, 1f));
                    go = CreateUnlitMesh($"Proj_{p.id}", projectileMesh, c);
                    float s = p.kind == LaneProjKind.TowerShell ? 1.55f : (p.kind == LaneProjKind.LuisBolt ? 1.05f : 0.72f);
                    go.transform.localScale = Vector3.one * s;
                    projVisuals[p.id] = go;
                }
                go.transform.position = ToWorld(p.x, p.y, 0.85f);
            }
            var rmProj = new List<int>();
            foreach (var kv in projVisuals) if (!activeProjs.Contains(kv.Key)) rmProj.Add(kv.Key);
            for (int i = 0; i < rmProj.Count; i++) { Object.Destroy(projVisuals[rmProj[i]]); projVisuals.Remove(rmProj[i]); }

            // Orbs
            var activeOrbs = new HashSet<int>();
            for (int i = 0; i < snap.orbs.Count; i++)
            {
                OrbSnap o = snap.orbs[i];
                activeOrbs.Add(o.id);
                if (!orbVisuals.TryGetValue(o.id, out GameObject go))
                {
                    Color c = o.ownerTeam == LaneTeam.Blue ? new Color(0.35f, 1f, 0.65f, 0.95f) : new Color(1f, 0.45f, 0.35f, 0.95f);
                    go = CreateUnlitMesh($"Orb_{o.id}", orbMesh, c);
                    orbVisuals[o.id] = go;
                }
                go.transform.position = ToWorld(o.x, o.y, 0.55f + Mathf.Sin(Time.time * 3.5f) * 0.14f);
                go.transform.Rotate(0f, 65f * Time.deltaTime, 0f);
            }
            var rmOrb = new List<int>();
            foreach (var kv in orbVisuals) if (!activeOrbs.Contains(kv.Key)) rmOrb.Add(kv.Key);
            for (int i = 0; i < rmOrb.Count; i++) { Object.Destroy(orbVisuals[rmOrb[i]]); orbVisuals.Remove(rmOrb[i]); }

            // Zones (Miravel R sanctums)
            while (zoneVisuals.Count < snap.zones.Count)
            {
                zoneVisuals.Add(CreateUnlitMesh("SanctumZone", ringMesh, new Color(0.45f, 0.98f, 0.72f, 0.68f)));
            }
            for (int i = 0; i < zoneVisuals.Count; i++)
            {
                bool active = i < snap.zones.Count;
                zoneVisuals[i].SetActive(active);
                if (active)
                {
                    ZoneSnap z = snap.zones[i];
                    zoneVisuals[i].transform.position = ToWorld(z.x, z.y, 0.08f);
                    zoneVisuals[i].transform.localScale = new Vector3(z.radius, 1f, z.radius);
                }
            }

            // Towers + aggro beam
            for (int i = 0; i < snap.towers.Count; i++)
            {
                TowerSnap t = snap.towers[i];
                if (!towerVisuals.TryGetValue(t.id, out TowerVisual tv)) continue;
                tv.aliveObj.SetActive(t.alive);
                tv.ruinObj.SetActive(!t.alive);
                if (tv.crystal) tv.crystal.Rotate(0f, 45f * Time.deltaTime, 0f);

                Vector3 tp = Vector3.zero;
                bool hasTarget = t.alive && t.targetId > 0 && TryFindEntityPos(snap, t.targetId, out tp);
                tv.aggroBeam.enabled = hasTarget;
                if (hasTarget)
                {
                    if (tv.aggroBeamMat != null)
                    {
                        if (t.aggroLockedOnHero)
                        {
                            tv.aggroBeamMat.color = new Color(1f, 0.16f, 0.14f, 1f);
                            tv.aggroBeam.startWidth = 0.15f;
                            tv.aggroBeam.endWidth = 0.08f;
                        }
                        else
                        {
                            tv.aggroBeamMat.color = tv.normalBeamColor;
                            tv.aggroBeam.startWidth = 0.09f;
                            tv.aggroBeam.endWidth = 0.04f;
                        }
                    }
                    tv.aggroBeam.SetPosition(0, ToWorld(t.x, t.y, 3.25f));
                    tv.aggroBeam.SetPosition(1, tp + Vector3.up * 0.65f);
                }
            }

            // Hover & Swordsman R mark highlights
            if (hoveredEntityId > 0 && TryFindEntityPos(snap, hoveredEntityId, out Vector3 hoverPos))
            {
                hoverRing.SetActive(true);
                hoverRing.transform.position = hoverPos + new Vector3(0f, 0.06f, 0f);
                hoverRing.transform.localScale = Vector3.one * 0.95f;
            }
            else
            {
                hoverRing.SetActive(false);
            }

            if (markSource != null && TryFindEntityPos(snap, markSource.swordMarkedTargetId, out Vector3 markPos))
            {
                swordMarkRing.SetActive(true);
                swordMarkRing.transform.position = markPos + new Vector3(0f, 0.08f, 0f);
                swordMarkRing.transform.localScale = Vector3.one * (0.85f + Mathf.Sin(Time.time * 6f) * 0.08f);
            }
            else
            {
                swordMarkRing.SetActive(false);
            }

            // RTS selection rings under selected friendly units (empty unless RTS active)
            UpdateSelectionRings(snap, selectedUnitIds);

            // Transient combat & spell bursts
            for (int i = 0; i < snap.events.Count; i++)
            {
                SpawnEventBurst(snap.events[i]);
            }

            if (snap.beams != null)
            {
                for (int i = 0; i < snap.beams.Count; i++)
                {
                    SpawnLaserBeamVisual(snap.beams[i]);
                }
            }

            if (cameraLocked && localHero != null)
            {
                camFocus = Vector3.Lerp(camFocus, new Vector3(localHero.x, 0f, localHero.y), 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
        }

        private void RecycleBurst(TransientBurst b)
        {
            if (b == null) return;
            if (b.go && burstPool.Count < 48)
            {
                b.go.SetActive(false);
                burstPool.Push(b);
            }
            else
            {
                if (b.go) Object.Destroy(b.go);
                if (b.mat) Object.Destroy(b.mat);
            }
        }

        private void RecycleBeam(TransientBeam tb)
        {
            if (tb == null) return;
            if (tb.go && beamPool.Count < 32)
            {
                tb.go.SetActive(false);
                beamPool.Push(tb);
            }
            else
            {
                if (tb.go) Object.Destroy(tb.go);
                if (tb.mat) Object.Destroy(tb.mat);
            }
        }

        public void TickFrame(Camera cam, float dt)
        {
            InterpolateDictionary(heroVisuals, dt);
            InterpolateDictionary(minionVisuals, dt);
            InterpolateDictionary(illusionVisuals, dt);

            foreach (var kv in heroVisuals) AnimateHeroRig(kv.Value, dt);
            foreach (var kv in illusionVisuals) AnimateHeroRig(kv.Value, dt);

            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                TransientBurst b = bursts[i];
                b.age += dt;
                if (b.age >= b.duration)
                {
                    RecycleBurst(b);
                    bursts.RemoveAt(i);
                    continue;
                }
                float t = b.age / b.duration;
                float s = Mathf.Lerp(b.startScale, b.endScale, t);
                b.go.transform.localScale = new Vector3(s, Mathf.Max(0.4f, b.go.transform.localScale.y), s);
                if (b.riseSpeed > 0f)
                {
                    b.go.transform.position += Vector3.up * (b.riseSpeed * dt);
                }
                if (b.mat != null)
                {
                    Color c = b.baseColor;
                    c.a = Mathf.Lerp(b.baseColor.a, 0f, t);
                    b.mat.color = c;
                }
            }

            for (int i = transientBeams.Count - 1; i >= 0; i--)
            {
                TransientBeam tb = transientBeams[i];
                tb.age += dt;
                if (tb.age >= tb.duration)
                {
                    RecycleBeam(tb);
                    transientBeams.RemoveAt(i);
                    continue;
                }
                float t = tb.age / tb.duration;
                float w = Mathf.Lerp(tb.startWidth, tb.startWidth * 0.15f, t);
                tb.lr.startWidth = w;
                tb.lr.endWidth = w * 0.72f;
                Color c = tb.baseColor;
                c.a = Mathf.Lerp(tb.baseColor.a, 0f, t);
                tb.mat.color = c;
            }

            UpdateCamera(cam, dt);
        }

        public void CenterCameraOnHero(HeroSnap localHero)
        {
            if (localHero == null) return;
            camFocus = new Vector3(localHero.x, 0f, localHero.y);
        }

        public void CenterCameraAt(Vector2 worldPos)
        {
            camFocus = new Vector3(worldPos.x, 0f, worldPos.y);
        }

        public void UpdateSelectionRings(LaneSnapshot snap, IReadOnlyList<int> selectedUnitIds)
        {
            int activeSelRingCount = 0;
            if (snap != null && selectedUnitIds != null)
            {
                for (int i = 0; i < selectedUnitIds.Count; i++)
                {
                    if (!TryFindEntityPos(snap, selectedUnitIds[i], out Vector3 selPos)) continue;
                    if (activeSelRingCount >= selectionRings.Count)
                    {
                        selectionRings.Add(CreateUnlitMesh($"SelectionRing_{activeSelRingCount}", ringMesh,
                            new Color(0.26f, 1f, 0.45f, 0.92f)));
                    }
                    GameObject ringGo = selectionRings[activeSelRingCount];
                    ringGo.SetActive(true);
                    ringGo.transform.position = selPos + new Vector3(0f, 0.055f, 0f);
                    ringGo.transform.localScale = Vector3.one * (0.94f + Mathf.Sin(Time.time * 5f) * 0.04f);
                    activeSelRingCount++;
                }
            }
            for (int i = activeSelRingCount; i < selectionRings.Count; i++)
            {
                selectionRings[i].SetActive(false);
            }
        }

        private void UpdateCamera(Camera cam, float dt)
        {
            if (cam == null) return;

            if (!cameraLocked)
            {
                Vector3 pan = Vector3.zero;
                if (Input.GetKey(KeyCode.UpArrow)) pan.z += 1f;
                if (Input.GetKey(KeyCode.DownArrow)) pan.z -= 1f;
                if (Input.GetKey(KeyCode.LeftArrow)) pan.x -= 1f;
                if (Input.GetKey(KeyCode.RightArrow)) pan.x += 1f;

                if (Application.isFocused)
                {
                    if (Input.GetMouseButtonDown(2))
                    {
                        middleDragging = true;
                        lastMouseDragPos = Input.mousePosition;
                    }
                    else if (Input.GetMouseButtonUp(2))
                    {
                        middleDragging = false;
                    }
                    if (middleDragging && Input.GetMouseButton(2))
                    {
                        Vector3 delta = Input.mousePosition - lastMouseDragPos;
                        lastMouseDragPos = Input.mousePosition;
                        camFocus -= new Vector3(delta.x, 0f, delta.y) * 0.032f;
                    }

                    Vector3 mp = Input.mousePosition;
                    if (mp.x >= 0 && mp.x <= Screen.width && mp.y >= 0 && mp.y <= Screen.height)
                    {
                        if (mp.x < 18f) pan.x -= 1f;
                        else if (mp.x > Screen.width - 18f) pan.x += 1f;
                        if (mp.y < 18f) pan.z -= 1f;
                        else if (mp.y > Screen.height - 18f) pan.z += 1f;
                    }
                }

                if (pan.sqrMagnitude > 0f)
                {
                    camFocus += pan.normalized * (18f * dt);
                }
                camFocus.x = Mathf.Clamp(camFocus.x, -26f, 26f);
                camFocus.z = Mathf.Clamp(camFocus.z, -11f, 11f);
            }
            else
            {
                middleDragging = false;
            }

            Vector3 camPos = camFocus + camOffset;
            if (camShakeTimer > 0f)
            {
                camShakeTimer = Mathf.Max(0f, camShakeTimer - dt);
                float fade = Mathf.Clamp01(camShakeTimer * 5f);
                camPos += new Vector3(
                    Mathf.Sin(Time.unscaledTime * 59f) * 0.7f,
                    Mathf.Cos(Time.unscaledTime * 71f) * 0.35f,
                    Mathf.Sin(Time.unscaledTime * 67f) * 0.7f) * (camShakeAmp * fade);
            }

            cam.transform.position = camPos;
            cam.transform.rotation = Quaternion.Euler(camEuler);
        }

        private void UpdateHeroTelegraph(HeroSnap h, LaneTuning tuning, bool visibleToLocal)
        {
            if (!heroTelegraphs.TryGetValue(h.id, out HeroTelegraph tel))
            {
                tel = new HeroTelegraph();
                GameObject MakeTel(string n, Mesh m, Color c, out Material outMat) => CreateUnlitMesh(n, m, c, tel.ownedMats, out outMat);
                tel.beam = MakeTel($"TelBeam_{h.id}", quadMesh, new Color(1f, 0.32f, 0.16f, 0.52f), out _);
                tel.beamOuter = MakeTel($"TelBeamOuter_{h.id}", quadMesh, new Color(1f, 0.82f, 0.30f, 0.30f), out _);
                tel.laserBeam = MakeTel($"TelLaserBeam_{h.id}", quadMesh, new Color(0.24f, 0.92f, 1f, 0.52f), out _);
                tel.ogreW = MakeTel($"TelOgreW_{h.id}", ogreWConeMesh, new Color(1f, 0.78f, 0.22f, 0.44f), out _);
                tel.swordQ = MakeTel($"TelSwordQ_{h.id}", swordQConeMesh, new Color(1f, 0.88f, 0.36f, 0.44f), out _);
                tel.ring = MakeTel($"TelRing_{h.id}", ringMesh, new Color(1f, 0.48f, 0.18f, 0.72f), out _);
                tel.cowardHalo = MakeTel($"CowardHalo_{h.id}", ringMesh, new Color(0.36f, 0.90f, 1f, 0.82f), out tel.cowardHaloMat);
                tel.auraRing = MakeTel($"AuraRing_{h.id}", ringMesh, new Color(0.32f, 0.95f, 0.65f, 0.24f), out _);
                tel.drainRing = MakeTel($"DrainRing_{h.id}", ringMesh, new Color(0.76f, 0.30f, 1f, 0.62f), out _);
                tel.wardRing = MakeTel($"WardRing_{h.id}", ringMesh, new Color(0.28f, 0.96f, 0.88f, 0.76f), out _);
                tel.curseRing = MakeTel($"CurseRing_{h.id}", ringMesh, new Color(0.84f, 0.28f, 1f, 0.78f), out _);
                tel.shieldRing = MakeTel($"ShieldRing_{h.id}", ringMesh, new Color(0.76f, 0.92f, 1f, 0.84f), out _);
                heroTelegraphs[h.id] = tel;
            }

            bool showBeam = false, showBeamOuter = false, showLaserBeam = false, showOgreW = false, showSwordQ = false, showRing = false;
            bool showAura = false, showDrain = false, showWard = false, showCurse = false, showShield = false, showCoward = false;

            if (visibleToLocal && tuning != null)
            {
                Vector3 pos = ToWorld(h.x, h.y, 0.05f);
                float aimDeg = Mathf.Atan2(h.aimDirX, h.aimDirY) * Mathf.Rad2Deg;
                Quaternion aimRot = Quaternion.Euler(0f, aimDeg, 0f);
                float hScale = Mathf.Max(0.85f, h.scale);

                bool isCastWindup = h.phase == LanePhase.Casting || h.phase == LanePhase.ComboSeq;
                if (isCastWindup && h.heroId == LaneHeroId.Ogre)
                {
                    if (h.activeSlot == LaneAbilitySlot.Q)
                    {
                        float qRange = tuning.ogreQRange * hScale;
                        float cHalfW = h.ogreQCentralHalfWidth > 0.1f ? h.ogreQCentralHalfWidth : tuning.ogreQWidth * hScale;
                        float oExtra = h.ogreQOuterHalfWidth > 0.05f ? h.ogreQOuterHalfWidth : tuning.ogreQOuterWidthFormula.Evaluate(Mathf.Max(1, h.rankQ), h.strength);
                        float tHalfW = cHalfW + oExtra;
                        float oRange = qRange + oExtra * 0.45f;

                        showBeam = true;
                        tel.beam.transform.position = pos + new Vector3(0f, 0.01f, 0f);
                        tel.beam.transform.rotation = aimRot;
                        tel.beam.transform.localScale = new Vector3(cHalfW * 2f, 1f, qRange);

                        showBeamOuter = true;
                        tel.beamOuter.transform.position = pos;
                        tel.beamOuter.transform.rotation = aimRot;
                        tel.beamOuter.transform.localScale = new Vector3(tHalfW * 2f, 1f, oRange);
                    }
                    else if (h.activeSlot == LaneAbilitySlot.W)
                    {
                        float wRange = h.ogreWRange > 0.2f ? h.ogreWRange : tuning.ogreWRange * hScale;
                        showOgreW = true;
                        tel.ogreW.transform.position = pos;
                        tel.ogreW.transform.rotation = aimRot;
                        tel.ogreW.transform.localScale = new Vector3(wRange, 1f, wRange);
                    }
                    else if (h.activeSlot == LaneAbilitySlot.E)
                    {
                        float eRad = h.ogreERadius > 0.2f ? h.ogreERadius : tuning.ogreERadiusFormula.Evaluate(Mathf.Max(1, h.rankE), h.strength) * hScale;
                        showRing = true;
                        tel.ring.transform.position = pos;
                        tel.ring.transform.localScale = new Vector3(eRad, 1f, eRad);
                    }
                }
                else if (h.heroId == LaneHeroId.Ogre && h.phase == LanePhase.Channeling && h.activeSlot == LaneAbilitySlot.R)
                {
                    showRing = true;
                    tel.ring.transform.position = pos;
                    tel.ring.transform.localScale = Vector3.one * (1.30f * hScale + Mathf.Sin(Time.time * 8f) * 0.12f);
                }
                else if (h.heroId == LaneHeroId.Swordsman && h.phase == LanePhase.Casting && h.activeSlot == LaneAbilitySlot.Q)
                {
                    showSwordQ = true;
                    tel.swordQ.transform.position = pos;
                    tel.swordQ.transform.rotation = aimRot;
                    tel.swordQ.transform.localScale = new Vector3(tuning.swordsmanQRange, 1f, tuning.swordsmanQRange);
                }
                else if (h.heroId == LaneHeroId.LaserShooter && h.phase == LanePhase.Casting && h.activeSlot == LaneAbilitySlot.Q && h.laserBeamRange > 0.1f)
                {
                    showLaserBeam = true;
                    tel.laserBeam.transform.position = pos;
                    tel.laserBeam.transform.rotation = aimRot;
                    tel.laserBeam.transform.localScale = new Vector3(tuning.laserQHalfWidth * 2f, 1f, h.laserBeamRange);
                }
                else if (h.heroId == LaneHeroId.LaserShooter && (h.phase == LanePhase.Casting || h.phase == LanePhase.Channeling) && h.activeSlot == LaneAbilitySlot.R)
                {
                    showLaserBeam = true;
                    tel.laserBeam.transform.position = ToWorld(h.x, h.y, 0.08f);
                    tel.laserBeam.transform.rotation = aimRot;
                    float widthPulse = h.phase == LanePhase.Channeling ? 1f + 0.08f * Mathf.Sin(Time.time * 24f) : 0.75f;
                    tel.laserBeam.transform.localScale = new Vector3(tuning.laserRHalfWidth * 2f * widthPulse, 1f, tuning.laserRRange);
                }

                showCoward = h.cowardShieldBonus > 0.5f || h.cowardShieldState > 0;
                if (showCoward)
                {
                    tel.cowardHalo.transform.position = ToWorld(h.x, h.y, 0.09f);
                    float f = Mathf.Clamp01(h.cowardShieldFactor);
                    float haloScale = h.cowardShieldState == 1 ? 0.76f + 0.24f * f
                        : (h.cowardShieldState == 2 ? 1.00f + 0.05f * Mathf.Sin(Time.time * 6f) : 0.78f + 0.18f * f);
                    tel.cowardHalo.transform.localScale = Vector3.one * haloScale;
                    if (tel.cowardHaloMat != null)
                    {
                        Color haloCol = h.cowardShieldState == 2 ? new Color(0.32f, 0.96f, 1f, 0.92f)
                            : (h.cowardShieldState == 1 ? new Color(1f, 0.85f, 0.34f, 0.45f + 0.40f * f)
                            : (h.cowardShieldState == 3 ? new Color(0.75f, 0.90f, 1f, 0.70f)
                            : new Color(0.95f, 0.52f, 0.28f, 0.25f + 0.55f * f)));
                        tel.cowardHaloMat.color = haloCol;
                    }
                }

                showAura = h.heroId == LaneHeroId.Miravel;
                if (showAura)
                {
                    float aRad = h.miravelAuraRadius > 1f ? h.miravelAuraRadius : tuning.luisAuraRadiusFormula.Evaluate(1, h.intelligence);
                    tel.auraRing.transform.position = ToWorld(h.x, h.y, 0.03f);
                    tel.auraRing.transform.localScale = new Vector3(aRad, 1f, aRad);
                }

                showDrain = h.miravelDrainAura > 0.05f;
                if (showDrain)
                {
                    tel.drainRing.transform.position = ToWorld(h.x, h.y, 0.055f);
                    float pulse = 0.95f + 0.12f * Mathf.Sin(Time.time * 5.5f);
                    tel.drainRing.transform.localScale = Vector3.one * pulse;
                }

                showWard = h.wardTimer > 0f;
                if (showWard)
                {
                    tel.wardRing.transform.position = ToWorld(h.x, h.y, 0.07f);
                    tel.wardRing.transform.localScale = Vector3.one * (1.14f + 0.05f * Mathf.Sin(Time.time * 5f));
                }

                showCurse = h.curseTimer > 0f;
                if (showCurse)
                {
                    tel.curseRing.transform.position = ToWorld(h.x, h.y, 0.08f);
                    float cs = 0.90f + 0.08f * h.curseStacks + 0.06f * Mathf.Sin(Time.time * 8f);
                    tel.curseRing.transform.localScale = Vector3.one * cs;
                }

                showShield = h.shieldHp > 1f;
                if (showShield)
                {
                    tel.shieldRing.transform.position = ToWorld(h.x, h.y, 0.06f);
                    tel.shieldRing.transform.localScale = Vector3.one * 1.04f;
                }
            }

            tel.beam.SetActive(showBeam);
            tel.beamOuter.SetActive(showBeamOuter);
            tel.laserBeam.SetActive(showLaserBeam);
            tel.ogreW.SetActive(showOgreW);
            tel.swordQ.SetActive(showSwordQ);
            tel.ring.SetActive(showRing);
            tel.cowardHalo.SetActive(showCoward);
            tel.auraRing.SetActive(showAura);
            tel.drainRing.SetActive(showDrain);
            tel.wardRing.SetActive(showWard);
            tel.curseRing.SetActive(showCurse);
            tel.shieldRing.SetActive(showShield);
        }

        private void AnimateHeroRig(EntityVisual v, float dt)
        {
            if (v.rig == null || !v.go.activeSelf) return;
            v.phaseTimer = Mathf.Max(0f, v.phaseTimer - dt);
            v.basicPunchTimer = Mathf.Max(0f, v.basicPunchTimer - dt);

            HeroRig r = v.rig;
            Vector3 bodyPos = DefaultBodyPos(v.heroId);
            Vector3 bodyEuler = Vector3.zero;
            Vector3 rightArmEuler = Vector3.zero;
            Vector3 leftArmEuler = Vector3.zero;
            Vector3 leftLegEuler = Vector3.zero;
            Vector3 rightLegEuler = Vector3.zero;

            bool walking = v.simSpeed > 0.25f &&
                (v.phase == LanePhase.Idle || v.phase == LanePhase.Recover ||
                 (v.heroId == LaneHeroId.LaserShooter && v.phase == LanePhase.Channeling && v.activeSlot == LaneAbilitySlot.R));
            if (walking)
            {
                v.walkCycle += dt * (4.5f + v.simSpeed * 1.4f);
                float legSwing = Mathf.Sin(v.walkCycle) * 26f;
                leftLegEuler = new Vector3(legSwing, 0f, 0f);
                rightLegEuler = new Vector3(-legSwing, 0f, 0f);
                bodyPos.y += Mathf.Abs(Mathf.Sin(v.walkCycle)) * 0.06f;
                bodyEuler = new Vector3(5f, 0f, Mathf.Cos(v.walkCycle) * 2.5f);
                rightArmEuler = new Vector3(-legSwing * 0.45f, 0f, 0f);
                leftArmEuler = new Vector3(legSwing * 0.45f, 0f, 0f);
            }

            float p = 1f - Mathf.Clamp01(v.phaseTimer / Mathf.Max(0.01f, v.phaseDuration));

            if (v.heroId == LaneHeroId.Ogre)
            {
                if ((v.phase == LanePhase.Casting || v.phase == LanePhase.ComboSeq) && v.activeSlot == LaneAbilitySlot.Q)
                {
                    if (p < 0.78f)
                    {
                        float t = Mathf.Clamp01(p / 0.78f);
                        float lift = v.rStrikeIsFinal ? -136f : -120f;
                        bodyEuler = new Vector3(-14f * t, 0f, 0f);
                        rightArmEuler = new Vector3(lift * t, -8f, 14f * t);
                        leftArmEuler = new Vector3(-45f * t, 0f, -15f * t);
                    }
                    else
                    {
                        float t = Mathf.Clamp01((p - 0.78f) / 0.22f);
                        bodyEuler = new Vector3(18f * t, 0f, 0f);
                        rightArmEuler = new Vector3(Mathf.Lerp(-120f, 60f, t), 6f, -8f);
                    }
                }
                else if ((v.phase == LanePhase.Casting || v.phase == LanePhase.ComboSeq) && v.activeSlot == LaneAbilitySlot.W)
                {
                    float dirSign = (v.rStrikeIndex % 2 == 1) ? -1f : 1f;
                    if (p < 0.75f)
                    {
                        float t = Mathf.Clamp01(p / 0.75f);
                        bodyEuler = new Vector3(6f * t, 44f * dirSign * t, 0f);
                        rightArmEuler = new Vector3(-22f, 62f * dirSign * t, -20f * dirSign * t);
                        leftArmEuler = new Vector3(-18f, -20f * dirSign, 0f);
                    }
                    else
                    {
                        float t = Mathf.Clamp01((p - 0.75f) / 0.25f);
                        bodyEuler = new Vector3(12f, Mathf.Lerp(44f, -42f, t) * dirSign, 0f);
                        rightArmEuler = new Vector3(22f, Mathf.Lerp(62f, -72f, t) * dirSign, 0f);
                    }
                }
                else if (v.phase == LanePhase.Casting && v.activeSlot == LaneAbilitySlot.E)
                {
                    float hop = Mathf.Sin(p * Mathf.PI);
                    bodyPos.y += hop * 0.56f;
                    bodyEuler = new Vector3(p > 0.75f ? 16f : -10f * hop, 0f, 0f);
                    rightArmEuler = new Vector3(-52f * hop, 32f * hop, -32f * hop);
                    leftArmEuler = new Vector3(-52f * hop, -32f * hop, 32f * hop);
                    leftLegEuler = new Vector3(-24f * hop, 0f, 0f);
                    rightLegEuler = new Vector3(-24f * hop, 0f, 0f);
                }
                else if (v.phase == LanePhase.Channeling && v.activeSlot == LaneAbilitySlot.R)
                {
                    float rumble = Mathf.Sin(Time.time * 28f) * 3.5f;
                    bodyPos.y -= 0.12f;
                    bodyEuler = new Vector3(18f, rumble, 0f);
                    rightArmEuler = new Vector3(-68f, -34f, 20f);
                    leftArmEuler = new Vector3(-48f, 34f, -20f);
                }
                else if (v.phase == LanePhase.Recover && v.activeSlot != LaneAbilitySlot.None)
                {
                    float fade = 1f - p;
                    if (v.activeSlot == LaneAbilitySlot.Q)
                    {
                        bodyEuler = new Vector3(16f * fade, 0f, 0f);
                        rightArmEuler = new Vector3(56f * fade, 0f, 0f);
                    }
                    else if (v.activeSlot == LaneAbilitySlot.W)
                    {
                        float dirSign = ((v.rStrikeIndex - 1) % 2 == 1) ? -1f : 1f;
                        bodyEuler = new Vector3(10f * fade, -38f * dirSign * fade, 0f);
                        rightArmEuler = new Vector3(18f * fade, -68f * dirSign * fade, 0f);
                    }
                    else
                    {
                        bodyEuler = new Vector3(14f * fade, 0f, 0f);
                    }
                }
            }
            else if (v.heroId == LaneHeroId.Swordsman)
            {
                if (v.phase == LanePhase.ExecuteDash)
                {
                    bodyEuler = new Vector3(28f, 0f, 0f);
                    rightArmEuler = new Vector3(-44f, 34f, -28f);
                    leftArmEuler = new Vector3(-44f, -34f, 28f);
                }
                else if (v.phase == LanePhase.Casting && v.activeSlot == LaneAbilitySlot.Q)
                {
                    float arc = Mathf.SmoothStep(0f, 1f, p);
                    bodyEuler = new Vector3(14f * arc, 0f, 0f);
                    rightArmEuler = new Vector3(32f * arc, Mathf.Lerp(55f, -72f, arc), 0f);
                    leftArmEuler = new Vector3(32f * arc, Mathf.Lerp(-55f, 72f, arc), 0f);
                }
                else if (v.phase == LanePhase.Windup)
                {
                    if (!v.isTwinSecondStrike)
                    {
                        rightArmEuler = new Vector3(-24f * p, 64f * p, -16f * p);
                        bodyEuler = new Vector3(4f, 20f * p, 0f);
                    }
                    else
                    {
                        leftArmEuler = new Vector3(-24f * p, -64f * p, 16f * p);
                        bodyEuler = new Vector3(8f * p, -28f * p, -6f * p);
                        if (v.basicPunchTimer > 0f)
                        {
                            float s = 1f - v.basicPunchTimer / 0.24f;
                            rightArmEuler = new Vector3(32f * (1f - s), Mathf.Lerp(58f, -72f, s), 0f);
                        }
                    }
                }
                else if (v.basicPunchTimer > 0f)
                {
                    float s = 1f - v.basicPunchTimer / 0.24f;
                    if ((v.lastBasicAlt & 1) == 0)
                    {
                        // Light 1st right-hand sabre flick
                        rightArmEuler = new Vector3(28f * (1f - s), Mathf.Lerp(54f, -74f, s), 0f);
                        bodyEuler = new Vector3(8f, Mathf.Lerp(16f, -22f, s), 0f);
                    }
                    else
                    {
                        // Heavy 2nd left-hand greatsword cleave
                        leftArmEuler = new Vector3(62f * (1f - s), Mathf.Lerp(-70f, 88f, s), -18f * (1f - s));
                        bodyEuler = new Vector3(16f, Mathf.Lerp(-26f, 32f, s), 0f);
                    }
                }
            }
            else if (v.heroId == LaneHeroId.Miravel)
            {
                if (v.phase == LanePhase.Channeling && v.activeSlot == LaneAbilitySlot.R)
                {
                    bodyPos.y += 0.24f + 0.06f * Mathf.Sin(Time.time * 5f);
                    bodyEuler = new Vector3(-6f, 0f, 0f);
                    rightArmEuler = new Vector3(-56f, 12f, -26f);
                    leftArmEuler = new Vector3(-58f, -12f, 26f);
                }
                else if (v.phase == LanePhase.Windup)
                {
                    rightArmEuler = new Vector3(32f * p, 0f, 0f);
                    leftArmEuler = new Vector3(-16f * p, 0f, 0f);
                }
                else if (v.basicPunchTimer > 0f)
                {
                    float s = v.basicPunchTimer / 0.24f;
                    rightArmEuler = new Vector3(38f * s, 0f, 0f);
                }
            }
            else if (v.heroId == LaneHeroId.LaserShooter)
            {
                if (v.phase == LanePhase.Channeling && v.activeSlot == LaneAbilitySlot.R)
                {
                    bodyEuler.x = 6f;
                    rightArmEuler = new Vector3(-2f + Mathf.Sin(Time.time * 28f) * 2.5f, 0f, 0f);
                    leftArmEuler = new Vector3(-6f, 28f, 0f);
                }
                else if (v.phase == LanePhase.Casting && v.activeSlot == LaneAbilitySlot.R)
                {
                    rightArmEuler = new Vector3(-18f * p, -10f, 0f);
                    leftArmEuler = new Vector3(-12f * p, 24f, 0f);
                }
                else if (v.heroId == LaneHeroId.LaserShooter && v.phase == LanePhase.Casting && v.activeSlot == LaneAbilitySlot.E)
                {
                    // Comical calm upright single step-back while keeping gun aimed forward
                    float stride = Mathf.Sin(v.laserStepProgress * Mathf.PI);
                    bodyPos.y += stride * 0.05f;
                    bodyEuler = new Vector3(-3f, 0f, 0f);
                    leftLegEuler = new Vector3(-28f * stride, 0f, 0f);
                    rightLegEuler = new Vector3(24f * stride, 0f, 0f);
                    rightArmEuler = new Vector3(0f, 0f, 0f);
                    leftArmEuler = new Vector3(6f, 20f, 0f);
                }
                else if (v.phase == LanePhase.Casting && v.activeSlot == LaneAbilitySlot.Q)
                {
                    rightArmEuler = Vector3.zero;
                    leftArmEuler = new Vector3(-4f, 26f, 0f);
                }
                else if (v.phase == LanePhase.Windup)
                {
                    rightArmEuler = new Vector3(-8f * p, 0f, 0f);
                    leftArmEuler = new Vector3(-6f * p, 20f * p, 0f);
                }
                else if (v.basicPunchTimer > 0f)
                {
                    float s = v.basicPunchTimer / 0.24f;
                    rightArmEuler = new Vector3(-14f * s, 0f, 0f);
                    bodyEuler.x -= 4f * s;
                }
                else if (!walking)
                {
                    rightArmEuler = new Vector3(6f, 0f, 0f);
                    leftArmEuler = new Vector3(8f, 18f, 0f);
                }
            }

            float blend = 1f - Mathf.Exp(-20f * dt);
            if (r.bodyPivot)
            {
                r.bodyPivot.localPosition = Vector3.Lerp(r.bodyPivot.localPosition, bodyPos, blend);
                r.bodyPivot.localRotation = Quaternion.Slerp(r.bodyPivot.localRotation, Quaternion.Euler(bodyEuler), blend);
            }
            if (r.rightArmPivot) r.rightArmPivot.localRotation = Quaternion.Slerp(r.rightArmPivot.localRotation, Quaternion.Euler(rightArmEuler), blend);
            if (r.leftArmPivot) r.leftArmPivot.localRotation = Quaternion.Slerp(r.leftArmPivot.localRotation, Quaternion.Euler(leftArmEuler), blend);
            if (r.leftLegPivot) r.leftLegPivot.localRotation = Quaternion.Slerp(r.leftLegPivot.localRotation, Quaternion.Euler(leftLegEuler), blend);
            if (r.rightLegPivot) r.rightLegPivot.localRotation = Quaternion.Slerp(r.rightLegPivot.localRotation, Quaternion.Euler(rightLegEuler), blend);
        }

        private static Vector3 DefaultBodyPos(LaneHeroId heroId)
        {
            if (heroId == LaneHeroId.Ogre) return new Vector3(0f, 0.66f, 0f);
            if (heroId == LaneHeroId.Swordsman) return new Vector3(0f, 0.50f, 0f);
            if (heroId == LaneHeroId.LaserShooter) return new Vector3(0f, 0.54f, 0f);
            return Vector3.zero;
        }

        private static void SetEntityTint(EntityVisual vis, Color tint)
        {
            vis.mpb.SetColor("_Color", tint);
            if (vis.renderers != null)
            {
                for (int i = 0; i < vis.renderers.Length; i++)
                {
                    if (vis.renderers[i]) vis.renderers[i].SetPropertyBlock(vis.mpb);
                }
            }
            else if (vis.mr != null)
            {
                vis.mr.SetPropertyBlock(vis.mpb);
            }
        }

        private void TriggerShake(float amp, float duration)
        {
            camShakeAmp = Mathf.Max(camShakeAmp, amp);
            camShakeTimer = Mathf.Max(camShakeTimer, duration);
        }

        private static void InterpolateDictionary(Dictionary<int, EntityVisual> dict, float dt)
        {
            float alpha = 1f - Mathf.Exp(-24f * dt);
            foreach (var kv in dict)
            {
                EntityVisual v = kv.Value;
                if (!v.go.activeSelf) continue;
                v.go.transform.position = Vector3.Lerp(v.go.transform.position, v.targetPos, alpha);
                Quaternion wantRot = Quaternion.Euler(0f, v.targetYaw, 0f);
                v.go.transform.rotation = Quaternion.Slerp(v.go.transform.rotation, wantRot, alpha);
                v.go.transform.localScale = Vector3.one * v.scale;
            }
        }

        private static void CleanupStaleVisuals(Dictionary<int, EntityVisual> dict, HashSet<int> alive)
        {
            var stale = new List<int>();
            foreach (var kv in dict) if (!alive.Contains(kv.Key)) stale.Add(kv.Key);
            for (int i = 0; i < stale.Count; i++)
            {
                Object.Destroy(dict[stale[i]].go);
                dict.Remove(stale[i]);
            }
        }

        private bool TryFindEntityPos(LaneSnapshot snap, int id, out Vector3 pos)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].id == id && snap.heroes[i].phase != LanePhase.Dead)
                {
                    pos = ToWorld(snap.heroes[i].x, snap.heroes[i].y);
                    return true;
                }
            }
            for (int i = 0; i < snap.minions.Count; i++)
            {
                if (snap.minions[i].id == id)
                {
                    pos = ToWorld(snap.minions[i].x, snap.minions[i].y);
                    return true;
                }
            }
            for (int i = 0; i < snap.illusions.Count; i++)
            {
                if (snap.illusions[i].id == id)
                {
                    pos = ToWorld(snap.illusions[i].x, snap.illusions[i].y);
                    return true;
                }
            }
            for (int i = 0; i < snap.towers.Count; i++)
            {
                if (snap.towers[i].id == id && snap.towers[i].alive)
                {
                    pos = ToWorld(snap.towers[i].x, snap.towers[i].y);
                    return true;
                }
            }
            pos = Vector3.zero;
            return false;
        }

        private void SpawnEventBurst(VisualEventSnap ev)
        {
            Color c;
            float duration = 0.42f;
            float startS = 0.7f;
            float endS = Mathf.Max(1.0f, ev.radius);
            float rise = 0f;
            Mesh mesh = ringMesh;

            switch (ev.kind)
            {
                case LaneEventKind.AttackMoveRing:
                    c = new Color(1f, 0.38f, 0.32f, 0.92f);
                    startS = 0.8f; endS = 0.15f; duration = 0.35f;
                    break;
                case LaneEventKind.OgreSlam:
                    c = new Color(1f, 0.36f, 0.14f, 0.94f);
                    startS = 0.85f; endS = ev.radius > 1f ? ev.radius * 0.55f : 2.3f; duration = 0.52f;
                    TriggerShake(0.24f, 0.20f);
                    if (sfxSource && sfxOgreCentral) sfxSource.PlayOneShot(sfxOgreCentral, 0.90f);
                    break;
                case LaneEventKind.OgreQShockwave:
                    c = new Color(0.96f, 0.84f, 0.44f, 0.62f);
                    startS = Mathf.Max(0.9f, ev.radius * 0.6f); endS = Mathf.Max(1.6f, ev.radius * 1.15f); duration = 0.44f;
                    if (sfxSource && sfxOgreShockwave) sfxSource.PlayOneShot(sfxOgreShockwave, 0.60f);
                    break;
                case LaneEventKind.OgreStrike:
                    c = new Color(1f, 0.62f, 0.22f, 0.90f);
                    startS = 0.9f; endS = ev.radius > 1f ? ev.radius * 0.45f : 2.1f; duration = 0.50f;
                    bool isCrit = !string.IsNullOrEmpty(ev.label) && ev.label.Contains("CRIT");
                    TriggerShake(isCrit ? 0.32f : 0.18f, isCrit ? 0.24f : 0.16f);
                    if (sfxSource && sfxOgreCentral) sfxSource.PlayOneShot(sfxOgreCentral, isCrit ? 1.0f : 0.8f);
                    break;
                case LaneEventKind.OgreSweep:
                    c = new Color(0.95f, 0.80f, 0.30f, 0.85f);
                    startS = 1.0f; endS = 2.8f; duration = 0.42f;
                    break;
                case LaneEventKind.OgreStomp:
                    c = new Color(1f, 0.45f, 0.18f, 0.92f);
                    startS = 0.8f; endS = ev.radius; duration = 0.50f;
                    TriggerShake(0.20f, 0.18f);
                    break;
                case LaneEventKind.LuisOrbPulse:
                case LaneEventKind.SwordsmanWHeal:
                    c = new Color(0.32f, 1f, 0.68f, 0.90f); // Signature rising mint heal halo!
                    startS = 0.8f; endS = ev.kind == LaneEventKind.LuisOrbPulse ? ev.radius : 1.7f;
                    rise = 1.4f; duration = 0.62f;
                    break;
                case LaneEventKind.LuisCurseProc:
                    c = new Color(0.74f, 0.34f, 1f, 0.85f);
                    startS = 0.5f; endS = 1.25f; rise = 0.8f;
                    break;
                case LaneEventKind.SwordsmanSlash:
                {
                    bool isHeavy = ev.label == "HEAVY";
                    c = isHeavy ? new Color(1f, 0.48f, 0.22f, 0.94f) : new Color(0.68f, 0.92f, 1f, 0.88f);
                    startS = isHeavy ? 0.85f : 0.55f;
                    endS = isHeavy ? 1.85f : 1.15f;
                    duration = isHeavy ? 0.38f : 0.24f;
                    if (isHeavy) TriggerShake(0.10f, 0.10f);
                    if (sfxSource)
                    {
                        AudioClip clip = isHeavy ? sfxSwordHeavy : sfxSwordLight;
                        if (clip) sfxSource.PlayOneShot(clip, isHeavy ? 0.85f : 0.65f);
                    }
                    break;
                }
                case LaneEventKind.SwordsmanQSlash:
                    c = new Color(1f, 0.88f, 0.52f, 0.90f);
                    startS = 0.65f; endS = Mathf.Max(1.3f, ev.radius * 0.65f); duration = 0.34f;
                    break;
                case LaneEventKind.SwordsmanRSlash:
                    c = new Color(1f, 0.88f, 0.52f, 0.90f);
                    startS = 0.65f; endS = Mathf.Max(1.3f, ev.radius * 0.65f); duration = 0.34f;
                    TriggerShake(0.22f, 0.20f);
                    break;
                case LaneEventKind.TowerAggro:
                    c = new Color(1f, 0.14f, 0.14f, 0.95f);
                    startS = 0.6f; endS = 2.1f; duration = 0.45f;
                    TriggerShake(0.14f, 0.16f);
                    break;
                case LaneEventKind.LaserShock:
                    c = new Color(0.28f, 0.94f, 1f, 0.92f);
                    startS = 0.6f; endS = Mathf.Max(1.6f, ev.radius); duration = 0.45f;
                    break;
                case LaneEventKind.LaserDecoy:
                    c = new Color(0.24f, 0.88f, 1f, 0.86f);
                    startS = 0.4f; endS = Mathf.Max(1.1f, ev.radius * 1.4f); duration = 0.36f;
                    break;
                case LaneEventKind.LaserGoldBonus:
                    c = new Color(1f, 0.86f, 0.24f, 0.94f);
                    startS = 0.55f; endS = 1.65f; rise = 1.5f; duration = 0.55f;
                    break;
                case LaneEventKind.BananaCrescent:
                    SpawnBananaCrescentVisual(ev);
                    return;
                default:
                    c = new Color(0.90f, 0.95f, 1f, 0.72f);
                    startS = 0.45f; endS = 1.1f;
                    break;
            }

            TransientBurst b;
            if (burstPool.Count > 0)
            {
                b = burstPool.Pop();
                b.go.name = $"Burst_{ev.kind}";
                b.go.transform.position = ToWorld(ev.x, ev.y, 0.12f);
                b.go.transform.localScale = new Vector3(startS, 1f, startS);
                b.mat.color = c;
                b.go.SetActive(true);
            }
            else
            {
                var mat = new Material(spritesDefaultShader);
                mat.color = c;
                var go = new GameObject($"Burst_{ev.kind}");
                go.transform.SetParent(root, false);
                go.transform.position = ToWorld(ev.x, ev.y, 0.12f);
                go.transform.localScale = new Vector3(startS, 1f, startS);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                b = new TransientBurst { go = go, mat = mat };
            }
            b.age = 0f;
            b.duration = duration;
            b.startScale = startS;
            b.endScale = endS;
            b.riseSpeed = rise;
            b.baseColor = c;
            bursts.Add(b);
        }

        private TransientBeam AcquirePooledBeam(string goName, Color c)
        {
            TransientBeam tb;
            if (beamPool.Count > 0)
            {
                tb = beamPool.Pop();
                tb.go.name = goName;
                tb.mat.color = c;
                tb.go.SetActive(true);
                return tb;
            }
            var mat = new Material(spritesDefaultShader);
            mat.color = c;
            var go = new GameObject(goName);
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return new TransientBeam { go = go, lr = lr, mat = mat };
        }

        private void SpawnLaserBeamVisual(LaserBeamSnap b)
        {
            if (b == null) return;
            Color c = b.isUlt
                ? new Color(0.75f, 1f, 1f, 0.98f)
                : (b.team == LaneTeam.Blue ? new Color(0.26f, 0.94f, 1f, 0.95f) : new Color(0.45f, 0.88f, 1f, 0.95f));
            TransientBeam tb = AcquirePooledBeam("LaserBeamFX", c);
            tb.lr.positionCount = 2;
            tb.lr.SetPosition(0, ToWorld(b.x0, b.y0, 0.92f));
            tb.lr.SetPosition(1, ToWorld(b.x1, b.y1, 0.92f));
            float w = Mathf.Max(0.08f, b.width);
            tb.lr.startWidth = w;
            tb.lr.endWidth = w * 0.72f;
            tb.age = 0f;
            tb.duration = b.isUlt ? 0.22f : 0.24f;
            tb.startWidth = w;
            tb.baseColor = c;
            transientBeams.Add(tb);
        }

        private void SpawnBananaCrescentVisual(VisualEventSnap ev)
        {
            Vector2 fwd = new Vector2(ev.dirX, ev.dirY);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector2.right;
            else fwd.Normalize();
            Vector2 right = new Vector2(-fwd.y, fwd.x);
            float outerR = Mathf.Max(1.4f, ev.radius);
            Vector2 primary = new Vector2(ev.x, ev.y);
            Vector2 center = primary + fwd * (outerR * 0.28f);
            float arcRadius = outerR * 0.88f;

            const int segs = 14;
            Color c = new Color(1f, 0.88f, 0.28f, 0.96f);
            TransientBeam tb = AcquirePooledBeam("BananaCrescentFX", c);
            tb.lr.positionCount = segs + 1;
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                float angle = Mathf.Lerp(-1.35f, 1.35f, t);
                Vector2 dir = fwd * Mathf.Cos(angle) + right * Mathf.Sin(angle);
                Vector2 p = center + dir * arcRadius;
                tb.lr.SetPosition(i, ToWorld(p.x, p.y, 0.65f));
            }
            float w = 0.26f;
            tb.lr.startWidth = w * 0.35f;
            tb.lr.endWidth = w * 0.35f;
            tb.age = 0f;
            tb.duration = 0.28f;
            tb.startWidth = w;
            tb.baseColor = c;
            transientBeams.Add(tb);
        }

        private EntityVisual CreateEntityVisual(string name, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = vertexMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return new EntityVisual
            {
                go = go,
                mr = mr,
                mpb = new MaterialPropertyBlock(),
            };
        }

        private GameObject CreateUnlitMesh(string name, Mesh mesh, Color color) =>
            CreateUnlitMesh(name, mesh, color, ownedMaterials, out _);

        private GameObject CreateUnlitMesh(string name, Mesh mesh, Color color, List<Material> matTracker, out Material outMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(spritesDefaultShader);
            mat.color = color;
            matTracker?.Add(mat);
            outMat = mat;
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
