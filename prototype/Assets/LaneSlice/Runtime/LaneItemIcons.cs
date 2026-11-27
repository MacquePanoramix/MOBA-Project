using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    /// <summary>
    /// Cached procedural 64x64 iconography for all 44 items in Ultimate Free MOBA.
    /// Zero proprietary assets; each texture uses deterministic SDF shapes with tier borders
    /// and distinct fantasy silhouettes, and is cleaned up on teardown via DisposeAll().
    /// </summary>
    public static class LaneItemIcons
    {
        public const int IconSize = 64;
        private static readonly Dictionary<int, Texture2D> Cache = new Dictionary<int, Texture2D>();

        public static Texture2D GetIcon(int rawItemId)
        {
            int id = LaneItems.MigrateItemId(rawItemId);
            if (id <= 0) id = rawItemId;
            if (Cache.TryGetValue(id, out Texture2D existing) && existing != null)
            {
                return existing;
            }

            LaneItems.TryGet(id, out LaneItemDef def);
            Texture2D tex = GenerateIconTexture(id, def);
            Cache[id] = tex;
            return tex;
        }

        public static void DisposeAll()
        {
            foreach (KeyValuePair<int, Texture2D> kv in Cache)
            {
                if (kv.Value != null)
                {
                    UnityEngine.Object.DestroyImmediate(kv.Value);
                }
            }
            Cache.Clear();
        }

        private static Texture2D GenerateIconTexture(int id, LaneItemDef def)
        {
            var buf = new Color[IconSize * IconSize];

            Color bgTop;
            Color bgBot;
            Color border;
            GetTierPalette(def, out bgTop, out bgBot, out border);
            FillBackground(buf, bgTop, bgBot, border);
            DrawMotif(buf, id, def);
            ReapplyBevel(buf, border);

            var pixels = new Color32[IconSize * IconSize];
            for (int i = 0; i < buf.Length; i++)
            {
                Color c = buf[i];
                c.r = Mathf.Clamp01(c.r);
                c.g = Mathf.Clamp01(c.g);
                c.b = Mathf.Clamp01(c.b);
                c.a = Mathf.Clamp01(c.a);
                pixels[i] = (Color32)c;
            }

            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, mipChain: false)
            {
                name = $"ItemIcon_{id}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return tex;
        }

        private static void GetTierPalette(LaneItemDef def, out Color bgTop, out Color bgBot, out Color border)
        {
            if (def.Id == 0)
            {
                bgTop = new Color(0.14f, 0.16f, 0.20f);
                bgBot = new Color(0.08f, 0.10f, 0.13f);
                border = new Color(0.45f, 0.50f, 0.58f);
                return;
            }

            if (!def.IsApproved)
            {
                if (def.Category == LaneItemCategory.ProvisionalAdvanced)
                {
                    bgTop = new Color(0.18f, 0.17f, 0.23f);
                    bgBot = new Color(0.09f, 0.09f, 0.14f);
                    border = new Color(0.68f, 0.58f, 0.36f);
                }
                else
                {
                    bgTop = new Color(0.15f, 0.17f, 0.21f);
                    bgBot = new Color(0.09f, 0.10f, 0.13f);
                    border = new Color(0.48f, 0.53f, 0.60f);
                }
                return;
            }

            switch (def.Category)
            {
                case LaneItemCategory.Advanced:
                    bgTop = new Color(0.22f, 0.18f, 0.12f);
                    bgBot = new Color(0.10f, 0.09f, 0.08f);
                    border = new Color(0.95f, 0.74f, 0.30f);
                    break;
                case LaneItemCategory.SpecializedComponent:
                    bgTop = new Color(0.11f, 0.21f, 0.20f);
                    bgBot = new Color(0.06f, 0.11f, 0.12f);
                    border = new Color(0.32f, 0.85f, 0.72f);
                    break;
                default:
                    bgTop = new Color(0.14f, 0.18f, 0.25f);
                    bgBot = new Color(0.08f, 0.10f, 0.15f);
                    border = new Color(0.56f, 0.72f, 0.88f);
                    break;
            }
        }

        private static void FillBackground(Color[] buf, Color top, Color bot, Color border)
        {
            for (int y = 0; y < IconSize; y++)
            {
                float ny = y / (float)(IconSize - 1);
                Color grad = Color.Lerp(bot, top, ny);
                for (int x = 0; x < IconSize; x++)
                {
                    float dx = (x - 31.5f) / 31.5f;
                    float dy = (y - 31.5f) / 31.5f;
                    float radial = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 0.55f);
                    Color c = Color.Lerp(grad * 0.75f, grad * 1.18f, radial);
                    c.a = 1f;
                    buf[y * IconSize + x] = c;
                }
            }
            AddRadialGlow(buf, 32f, 32f, 26f, border, 0.16f);
        }

        private static void ReapplyBevel(Color[] buf, Color border)
        {
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    int dEdge = Mathf.Min(Mathf.Min(x, IconSize - 1 - x), Mathf.Min(y, IconSize - 1 - y));
                    int idx = y * IconSize + x;
                    if (dEdge == 0)
                    {
                        buf[idx] = border * 0.65f;
                        buf[idx].a = 1f;
                    }
                    else if (dEdge == 1)
                    {
                        buf[idx] = border;
                        buf[idx].a = 1f;
                    }
                    else if (dEdge == 2)
                    {
                        buf[idx] = Color.Lerp(buf[idx], border * 0.45f, 0.55f);
                    }
                }
            }
        }

        private static void DrawMotif(Color[] buf, int id, LaneItemDef def)
        {
            switch (id)
            {
                // ---- APPROVED STARTERS / COMPONENTS ----
                case LaneItemIds.SmallAxe:
                    DrawAxe(buf, new Color(0.68f, 0.74f, 0.80f), new Color(0.55f, 0.36f, 0.22f), doubleHead: false, crimsonTint: false);
                    break;
                case LaneItemIds.HeavyAxe:
                    DrawAxe(buf, new Color(0.76f, 0.82f, 0.88f), new Color(0.45f, 0.30f, 0.20f), doubleHead: true, crimsonTint: false);
                    break;
                case LaneItemIds.ShortSword:
                    DrawStraightSword(buf, new Color(0.74f, 0.80f, 0.86f), new Color(0.62f, 0.46f, 0.25f), length: 30f, wide: false, molten: false);
                    break;
                case LaneItemIds.SteelSword:
                    DrawStraightSword(buf, new Color(0.84f, 0.90f, 0.96f), new Color(0.72f, 0.58f, 0.30f), length: 38f, wide: true, molten: false);
                    break;
                case LaneItemIds.Titanstone:
                    DrawMonolith(buf, new Color(0.24f, 0.23f, 0.27f), new Color(0.98f, 0.52f, 0.16f), amberHeart: false);
                    break;
                case LaneItemIds.Windfeather:
                    DrawFeather(buf, new Color(0.38f, 0.90f, 0.72f), new Color(0.82f, 0.98f, 0.92f));
                    break;
                case LaneItemIds.SagesEye:
                    DrawSagesEye(buf);
                    break;
                case LaneItemIds.LifeCrystal:
                    DrawGemCrystal(buf, new Color(0.25f, 0.88f, 0.38f), new Color(0.78f, 1.0f, 0.82f), jagged: false);
                    break;
                case LaneItemIds.Heartstone:
                    DrawHeart(buf, new Color(0.86f, 0.22f, 0.30f), new Color(1.0f, 0.68f, 0.72f), cracks: false);
                    break;
                case LaneItemIds.Bloodstone:
                    DrawGemCrystal(buf, new Color(0.78f, 0.12f, 0.18f), new Color(1.0f, 0.42f, 0.44f), jagged: true);
                    break;
                case LaneItemIds.WoodenShield:
                    DrawShield(buf, new Color(0.56f, 0.37f, 0.20f), new Color(0.66f, 0.70f, 0.75f), splitSilver: false, winged: false);
                    break;
                case LaneItemIds.SilverAmulet:
                    DrawAmulet(buf, new Color(0.76f, 0.84f, 0.94f), new Color(0.45f, 0.78f, 1.0f));
                    break;
                case LaneItemIds.ReinforcedShield:
                    DrawShield(buf, new Color(0.52f, 0.36f, 0.20f), new Color(0.72f, 0.84f, 0.98f), splitSilver: true, winged: false);
                    break;
                case LaneItemIds.SpringWater:
                    DrawFlask(buf, new Color(0.22f, 0.74f, 0.94f), new Color(0.72f, 0.95f, 1.0f), swirl: false, echoRings: false);
                    break;
                case LaneItemIds.SacredPearl:
                    DrawPearl(buf);
                    break;
                case LaneItemIds.HealingElixir:
                    DrawFlask(buf, new Color(0.24f, 0.92f, 0.74f), new Color(0.95f, 0.85f, 0.44f), swirl: false, echoRings: true);
                    break;
                case LaneItemIds.DragonScale:
                    DrawDragonScale(buf);
                    break;
                case LaneItemIds.PrimordialBlood:
                    DrawPrimordialBlood(buf);
                    break;
                case LaneItemIds.IronDagger:
                    DrawDagger(buf, new Color(0.70f, 0.76f, 0.82f), new Color(0.52f, 0.40f, 0.24f), speedArcs: false);
                    break;
                case LaneItemIds.NimbleDagger:
                    DrawDagger(buf, new Color(0.66f, 0.94f, 0.86f), new Color(0.84f, 0.70f, 0.30f), speedArcs: true);
                    break;
                case LaneItemIds.WingedCharm:
                    DrawWings(buf, new Color(0.96f, 0.82f, 0.34f), new Color(0.44f, 0.90f, 0.96f));
                    break;

                // ---- 6 APPROVED ADVANCED ITEMS ----
                case LaneItemIds.ExecutionersAxe:
                    DrawAxe(buf, new Color(0.78f, 0.82f, 0.88f), new Color(0.32f, 0.20f, 0.18f), doubleHead: true, crimsonTint: true);
                    break;
                case LaneItemIds.CowardsShield:
                    DrawShield(buf, new Color(0.28f, 0.42f, 0.56f), new Color(0.62f, 0.92f, 0.78f), splitSilver: true, winged: true);
                    break;
                case LaneItemIds.ChaliceOfLife:
                    DrawChalice(buf, new Color(0.94f, 0.78f, 0.30f), new Color(0.28f, 0.95f, 0.68f), tidal: false);
                    break;
                case LaneItemIds.DraconicBlade:
                    DrawStraightSword(buf, new Color(0.24f, 0.22f, 0.26f), new Color(0.78f, 0.48f, 0.22f), length: 42f, wide: true, molten: true);
                    break;
                case LaneItemIds.GiantsHeart:
                    DrawMonolith(buf, new Color(0.30f, 0.26f, 0.24f), new Color(1.0f, 0.66f, 0.20f), amberHeart: true);
                    break;
                case LaneItemIds.BananaBlade:
                    DrawBananaBlade(buf);
                    break;

                // ---- PROVISIONAL STARTERS / COMPONENTS / CAPSTONES ----
                case LaneItemIds.WoodlandSalve:
                    DrawJar(buf, new Color(0.52f, 0.36f, 0.22f), new Color(0.36f, 0.84f, 0.38f));
                    break;
                case LaneItemIds.MinersHatchet:
                    DrawAxe(buf, new Color(0.60f, 0.64f, 0.68f), new Color(0.62f, 0.44f, 0.24f), doubleHead: false, crimsonTint: false);
                    DrawLineGlow(buf, new Vector2(18, 44), new Vector2(40, 22), new Color(0.95f, 0.78f, 0.35f), 1.4f);
                    break;
                case LaneItemIds.BrawlersBuckler:
                    DrawRoundBuckler(buf, new Color(0.55f, 0.42f, 0.28f), new Color(0.78f, 0.82f, 0.88f));
                    break;
                case LaneItemIds.ManaDewVial:
                    DrawFlask(buf, new Color(0.22f, 0.56f, 0.98f), new Color(0.64f, 0.88f, 1.0f), swirl: false, echoRings: false);
                    break;
                case LaneItemIds.VitalityVest:
                    DrawVest(buf, new Color(0.46f, 0.34f, 0.22f), new Color(0.36f, 0.82f, 0.44f));
                    break;
                case LaneItemIds.SapphireShard:
                    DrawGemCrystal(buf, new Color(0.20f, 0.52f, 0.98f), new Color(0.68f, 0.90f, 1.0f), jagged: false);
                    break;
                case LaneItemIds.DewdropCharm:
                    DrawAmulet(buf, new Color(0.45f, 0.76f, 0.98f), new Color(0.28f, 0.90f, 1.0f));
                    break;
                case LaneItemIds.RunedSigil:
                    DrawRunedSigil(buf);
                    break;
                case LaneItemIds.ClockworkPin:
                    DrawGear(buf, new Color(0.86f, 0.68f, 0.28f), new Color(0.32f, 0.86f, 0.92f));
                    break;
                case LaneItemIds.WellspringFlask:
                    DrawFlask(buf, new Color(0.20f, 0.56f, 0.98f), new Color(0.48f, 0.94f, 1.0f), swirl: true, echoRings: false);
                    break;
                case LaneItemIds.KindredCenser:
                    DrawCenser(buf);
                    break;
                case LaneItemIds.BlightedThorn:
                    DrawBlightedThorn(buf);
                    break;
                case LaneItemIds.AstralReservoir:
                    DrawChalice(buf, new Color(0.36f, 0.44f, 0.88f), new Color(0.62f, 0.90f, 1.0f), tidal: false);
                    DrawSparkle(buf, 32f, 44f, 6f, Color.white);
                    break;
                case LaneItemIds.TidecallerChalice:
                    DrawChalice(buf, new Color(0.24f, 0.74f, 0.78f), new Color(0.44f, 0.96f, 0.88f), tidal: true);
                    break;
                case LaneItemIds.EconomistsTome:
                    DrawTome(buf);
                    break;
                case LaneItemIds.MomentumGreaves:
                    DrawBoot(buf);
                    break;
                case LaneItemIds.ColossusPauldron:
                    DrawPauldron(buf);
                    break;
                default:
                    DrawGemCrystal(buf, new Color(0.55f, 0.70f, 0.88f), Color.white, jagged: false);
                    break;
            }
        }

        // ---- Bespoke item silhouettes ----

        private static void DrawBananaBlade(Color[] buf)
        {
            // Curved metallic steel sabre shaped like a sleek crescent (NOT literal fruit!)
            AddRadialGlow(buf, 33f, 34f, 24f, new Color(0.58f, 0.82f, 0.98f), 0.24f);

            // Outer sharp crescent blade (metallic cool steel -> bright specular rim -> darker fuller)
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    // Blade spine & edge defined by offset overlapping circles forming a sleek crescent arc
                    // Inner spine circle centered at (16, 44) r=29; outer edge circle centered at (22, 41) r=31
                    float dOuter = Vector2.Distance(new Vector2(px, py), new Vector2(20f, 42f));
                    float dInner = Vector2.Distance(new Vector2(px, py), new Vector2(14f, 46f));

                    // Constrain to upper-right / bottom arc from hilt (16,16) sweeping up-right to needle tip (49, 52)
                    float angle = Mathf.Atan2(py - 44f, px - 17f) * Mathf.Rad2Deg;
                    bool inArc = angle >= -85f && angle <= 26f && px >= 16f && py >= 15f;

                    if (inArc && dOuter <= 31.5f && dInner >= 24.5f)
                    {
                        float tWidth = Mathf.Clamp01((dOuter - dInner + 7f) / 14f);
                        float edgeHighlight = Mathf.Clamp01(1f - Mathf.Abs(dOuter - 30.8f) * 0.9f);
                        float fullerDark = Mathf.Clamp01(1f - Mathf.Abs(dOuter - 27.6f) * 0.7f);
                        Color steel = Color.Lerp(new Color(0.46f, 0.54f, 0.64f), new Color(0.88f, 0.93f, 0.99f), tWidth);
                        steel = Color.Lerp(steel, new Color(0.24f, 0.30f, 0.38f), fullerDark * 0.45f);
                        steel = Color.Lerp(steel, Color.white, edgeHighlight * 0.75f);
                        // Subtle golden-brass gleam in fuller line
                        float goldGleam = Mathf.Clamp01(1f - Mathf.Abs(dOuter - 28.4f) * 1.8f) * 0.28f;
                        steel = Color.Lerp(steel, new Color(0.95f, 0.78f, 0.34f), goldGleam);
                        BlendPixel(buf, x, y, steel, 1f);
                    }
                }
            }

            // Sharp metallic blade tip refinement
            FillTri(buf, new Vector2(45f, 47f), new Vector2(51f, 54f), new Vector2(48f, 44f), new Color(0.92f, 0.96f, 1.0f));
            // Bronze-gold crossguard, grip, and counterweight pommel at bottom-left
            DrawSegment(buf, new Vector2(11f, 11f), new Vector2(19f, 18f), 2.3f, new Color(0.32f, 0.22f, 0.14f));
            DrawSegment(buf, new Vector2(14f, 22f), new Vector2(24f, 13f), 2.1f, new Color(0.92f, 0.74f, 0.30f));
            FillCircle(buf, 11f, 11f, 3.0f, new Color(0.96f, 0.80f, 0.34f));
            AddRadialGlow(buf, 46f, 49f, 7f, new Color(0.70f, 0.94f, 1.0f), 0.45f);
        }

        private static void DrawMonolith(Color[] buf, Color stoneCol, Color glowCol, bool amberHeart)
        {
            AddRadialGlow(buf, 32f, 31f, 25f, glowCol, amberHeart ? 0.42f : 0.32f);
            // Massive towering stone silhouette
            FillQuad(buf, new Vector2(17f, 11f), new Vector2(47f, 11f), new Vector2(43f, 52f), new Vector2(21f, 52f), stoneCol);
            FillQuad(buf, new Vector2(21f, 52f), new Vector2(43f, 52f), new Vector2(36f, 57f), new Vector2(28f, 57f), stoneCol * 1.18f);
            // Bevel highlight on left stone face
            FillQuad(buf, new Vector2(18f, 13f), new Vector2(28f, 13f), new Vector2(28f, 53f), new Vector2(22f, 51f), stoneCol * 1.24f);

            // Cracks / seismic glowing fissures
            DrawLineGlow(buf, new Vector2(32f, 51f), new Vector2(27f, 35f), glowCol, 1.5f);
            DrawLineGlow(buf, new Vector2(27f, 35f), new Vector2(36f, 24f), glowCol, 1.5f);
            DrawLineGlow(buf, new Vector2(36f, 24f), new Vector2(31f, 13f), glowCol, 1.5f);
            DrawLineGlow(buf, new Vector2(27f, 35f), new Vector2(19f, 27f), glowCol, 1.1f);
            DrawLineGlow(buf, new Vector2(36f, 24f), new Vector2(44f, 32f), glowCol, 1.1f);

            if (amberHeart)
            {
                // Glowing petrified amber giant heart core
                FillCircle(buf, 32f, 33f, 7.5f, new Color(0.95f, 0.56f, 0.14f));
                FillCircle(buf, 32f, 33f, 4.5f, new Color(1.0f, 0.88f, 0.45f));
                AddRadialGlow(buf, 32f, 33f, 14f, new Color(1.0f, 0.68f, 0.18f), 0.5f);
            }
            else
            {
                FillCircle(buf, 32f, 32f, 3.6f, new Color(1.0f, 0.78f, 0.34f));
            }
        }

        private static void DrawSagesEye(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 32f, 24f, new Color(0.32f, 0.78f, 1.0f), 0.40f);
            // Outer silver rune ring
            DrawRing(buf, 32f, 32f, 21f, 1.5f, new Color(0.64f, 0.82f, 0.98f) * 0.85f);
            // Almond eye outer bezel
            for (int y = 14; y < 50; y++)
            {
                for (int x = 8; x < 56; x++)
                {
                    float dx = (x - 32f) / 21f;
                    float dy = (y - 32f) / 13f;
                    float lens = dx * dx + dy * dy * (1.25f + Mathf.Abs(dx) * 0.65f);
                    if (lens <= 1f)
                    {
                        Color col = Color.Lerp(new Color(0.14f, 0.36f, 0.74f), new Color(0.54f, 0.90f, 1.0f), 1f - lens);
                        BlendPixel(buf, x, y, col, 1f);
                    }
                }
            }
            // Luminous iris + wise white glint
            FillCircle(buf, 32f, 32f, 8.5f, new Color(0.35f, 0.88f, 1.0f));
            FillCircle(buf, 32f, 32f, 4.2f, new Color(0.06f, 0.14f, 0.34f));
            FillCircle(buf, 35f, 35f, 2.2f, Color.white);
        }

        private static void DrawPrimordialBlood(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 30f, 24f, new Color(0.92f, 0.16f, 0.24f), 0.42f);
            // Teardrop ruby-crimson vessel / swirling primal blood
            for (int y = 11; y < 55; y++)
            {
                for (int x = 14; x < 50; x++)
                {
                    float ny = (y - 14f) / 38f; // 0 bottom .. 1 tip
                    float radius = Mathf.Lerp(14f, 1.5f, Mathf.Pow(ny, 1.15f)) * (ny < 0.3f ? Mathf.Sqrt(Mathf.Clamp01(ny / 0.3f)) * 0.45f + 0.55f : 1f);
                    float dx = Mathf.Abs(x - 32f);
                    if (dx <= radius)
                    {
                        float swirl = 0.5f + 0.5f * Mathf.Sin((x - 32f) * 0.35f + (y - 26f) * 0.28f);
                        Color blood = Color.Lerp(new Color(0.45f, 0.04f, 0.09f), new Color(0.96f, 0.18f, 0.26f), swirl);
                        if (dx < radius * 0.45f && ny > 0.2f && ny < 0.6f)
                        {
                            blood = Color.Lerp(blood, new Color(1.0f, 0.65f, 0.55f), 0.45f);
                        }
                        BlendPixel(buf, x, y, blood, 1f);
                    }
                }
            }
            // Gold collar filigree around neck
            DrawSegment(buf, new Vector2(27f, 45f), new Vector2(37f, 45f), 2.0f, new Color(0.92f, 0.74f, 0.30f));
            FillCircle(buf, 27f, 28f, 2.5f, new Color(1.0f, 0.72f, 0.72f));
        }

        private static void DrawAxe(Color[] buf, Color bladeCol, Color haftCol, bool doubleHead, bool crimsonTint)
        {
            DrawSegment(buf, new Vector2(15f, 12f), new Vector2(46f, 47f), 2.6f, haftCol);
            // Main forward crescent axe blade
            FillQuad(buf, new Vector2(31f, 34f), new Vector2(49f, 52f), new Vector2(54f, 37f), new Vector2(37f, 26f), bladeCol);
            DrawSegment(buf, new Vector2(49f, 52f), new Vector2(54f, 37f), 1.6f, crimsonTint ? new Color(1.0f, 0.36f, 0.36f) : Color.white);
            if (doubleHead)
            {
                FillQuad(buf, new Vector2(28f, 39f), new Vector2(16f, 49f), new Vector2(24f, 55f), new Vector2(36f, 45f), bladeCol * 0.92f);
            }
            if (crimsonTint)
            {
                AddRadialGlow(buf, 41f, 41f, 16f, new Color(0.95f, 0.18f, 0.22f), 0.36f);
                FillCircle(buf, 38f, 39f, 4.2f, new Color(0.92f, 0.14f, 0.20f));
            }
        }

        private static void DrawStraightSword(Color[] buf, Color bladeCol, Color hiltCol, float length, bool wide, bool molten)
        {
            Vector2 p0 = new Vector2(14f, 14f);
            Vector2 dir = new Vector2(0.707f, 0.707f);
            Vector2 bladeStart = p0 + dir * 10f;
            Vector2 tip = p0 + dir * length;
            float hw = wide ? 3.4f : 2.3f;
            if (molten) AddRadialGlow(buf, 36f, 36f, 22f, new Color(1.0f, 0.42f, 0.12f), 0.38f);
            DrawSegment(buf, p0, bladeStart, 2.0f, hiltCol * 0.75f);
            DrawSegment(buf, bladeStart, tip, hw, bladeCol);
            DrawSegment(buf, bladeStart, tip, 1.0f, molten ? new Color(1.0f, 0.56f, 0.16f) : Color.white * 0.95f);
            DrawSegment(buf, bladeStart + new Vector2(-7f, 7f), bladeStart + new Vector2(7f, -7f), 2.1f, hiltCol);
            FillCircle(buf, p0.x, p0.y, 2.8f, hiltCol);
        }

        private static void DrawFeather(Color[] buf, Color baseCol, Color tipCol)
        {
            AddRadialGlow(buf, 32f, 34f, 20f, baseCol, 0.28f);
            FillEllipseRot(buf, new Vector2(34f, 34f), 19f, 7.5f, 42f, baseCol, tipCol);
            DrawSegment(buf, new Vector2(14f, 14f), new Vector2(49f, 49f), 1.3f, Color.white);
        }

        private static void DrawGemCrystal(Color[] buf, Color c1, Color c2, bool jagged)
        {
            AddRadialGlow(buf, 32f, 32f, 22f, c1, 0.38f);
            FillTri(buf, new Vector2(32f, 54f), new Vector2(18f, 34f), new Vector2(32f, 30f), c2);
            FillTri(buf, new Vector2(32f, 54f), new Vector2(46f, 34f), new Vector2(32f, 30f), c1 * 1.1f);
            FillTri(buf, new Vector2(32f, 11f), new Vector2(18f, 34f), new Vector2(32f, 30f), c1);
            FillTri(buf, new Vector2(32f, 11f), new Vector2(46f, 34f), new Vector2(32f, 30f), c1 * 0.72f);
            if (jagged)
            {
                FillTri(buf, new Vector2(15f, 18f), new Vector2(24f, 38f), new Vector2(11f, 36f), c1 * 0.85f);
                FillTri(buf, new Vector2(49f, 19f), new Vector2(53f, 38f), new Vector2(40f, 35f), c1 * 0.85f);
            }
        }

        private static void DrawHeart(Color[] buf, Color deep, Color gleam, bool cracks)
        {
            AddRadialGlow(buf, 32f, 32f, 22f, deep, 0.36f);
            FillCircle(buf, 24f, 38f, 10.5f, deep);
            FillCircle(buf, 40f, 38f, 10.5f, deep);
            FillTri(buf, new Vector2(14f, 35f), new Vector2(50f, 35f), new Vector2(32f, 13f), deep);
            FillCircle(buf, 26f, 39f, 4.2f, gleam);
            if (cracks) DrawLineGlow(buf, new Vector2(32f, 42f), new Vector2(32f, 18f), new Color(1.0f, 0.78f, 0.30f), 1.4f);
        }

        private static void DrawShield(Color[] buf, Color leftCol, Color rightCol, bool splitSilver, bool winged)
        {
            if (winged)
            {
                FillTri(buf, new Vector2(18f, 44f), new Vector2(7f, 55f), new Vector2(20f, 28f), new Color(0.55f, 0.94f, 0.82f));
                FillTri(buf, new Vector2(46f, 44f), new Vector2(57f, 55f), new Vector2(44f, 28f), new Color(0.55f, 0.94f, 0.82f));
            }
            Color r = splitSilver ? rightCol : leftCol;
            FillQuad(buf, new Vector2(16f, 48f), new Vector2(32f, 51f), new Vector2(32f, 13f), new Vector2(18f, 26f), leftCol);
            FillQuad(buf, new Vector2(32f, 51f), new Vector2(48f, 48f), new Vector2(46f, 26f), new Vector2(32f, 13f), r);
            DrawSegment(buf, new Vector2(32f, 14f), new Vector2(32f, 50f), 1.4f, rightCol);
            FillCircle(buf, 32f, 35f, 4.5f, rightCol);
        }

        private static void DrawAmulet(Color[] buf, Color metal, Color core)
        {
            DrawRing(buf, 32f, 41f, 10f, 1.5f, metal * 0.85f);
            FillCircle(buf, 32f, 25f, 11f, metal);
            FillCircle(buf, 32f, 25f, 7.5f, core);
            AddRadialGlow(buf, 32f, 25f, 14f, core, 0.35f);
        }

        private static void DrawFlask(Color[] buf, Color liquid, Color rim, bool swirl, bool echoRings)
        {
            if (echoRings) DrawRing(buf, 32f, 28f, 21f, 1.3f, new Color(0.45f, 0.96f, 0.82f, 0.7f));
            FillCircle(buf, 32f, 26f, 15f, new Color(0.22f, 0.28f, 0.34f));
            FillCircle(buf, 32f, 25f, 12.5f, liquid);
            FillQuad(buf, new Vector2(27f, 36f), new Vector2(37f, 36f), new Vector2(36f, 50f), new Vector2(28f, 50f), rim * 0.85f);
            if (swirl) DrawRing(buf, 32f, 25f, 7.5f, 1.4f, Color.white);
            FillCircle(buf, 27f, 29f, 3.0f, Color.white * 0.8f);
        }

        private static void DrawPearl(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 32f, 24f, new Color(0.64f, 1.0f, 0.88f), 0.36f);
            for (int y = 14; y < 50; y++)
            {
                for (int x = 14; x < 50; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f));
                    if (d <= 15.5f)
                    {
                        float spec = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(27f, 37f)) / 9f);
                        Color p = Color.Lerp(new Color(0.68f, 0.76f, 0.82f), new Color(1.0f, 0.96f, 0.92f), spec * 0.85f + 0.25f);
                        BlendPixel(buf, x, y, p, 1f);
                    }
                }
            }
        }

        private static void DrawDragonScale(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 32f, 22f, new Color(0.95f, 0.45f, 0.18f), 0.30f);
            FillTri(buf, new Vector2(14f, 44f), new Vector2(50f, 44f), new Vector2(32f, 13f), new Color(0.68f, 0.20f, 0.14f));
            FillTri(buf, new Vector2(19f, 42f), new Vector2(45f, 42f), new Vector2(32f, 21f), new Color(0.92f, 0.44f, 0.18f));
            FillTri(buf, new Vector2(25f, 41f), new Vector2(39f, 41f), new Vector2(32f, 30f), new Color(1.0f, 0.72f, 0.28f));
        }

        private static void DrawDagger(Color[] buf, Color blade, Color hilt, bool speedArcs)
        {
            if (speedArcs)
            {
                DrawRing(buf, 30f, 30f, 21f, 1.3f, new Color(0.40f, 0.96f, 0.82f, 0.65f));
            }
            DrawSegment(buf, new Vector2(18f, 18f), new Vector2(25f, 25f), 1.8f, hilt);
            DrawSegment(buf, new Vector2(21f, 28f), new Vector2(28f, 21f), 1.8f, hilt);
            FillTri(buf, new Vector2(22f, 28f), new Vector2(28f, 22f), new Vector2(50f, 50f), blade);
        }

        private static void DrawWings(Color[] buf, Color wingCol, Color coreCol)
        {
            FillTri(buf, new Vector2(30f, 28f), new Vector2(10f, 48f), new Vector2(16f, 22f), wingCol);
            FillTri(buf, new Vector2(34f, 28f), new Vector2(54f, 48f), new Vector2(48f, 22f), wingCol);
            FillCircle(buf, 32f, 30f, 6.5f, coreCol);
        }

        private static void DrawChalice(Color[] buf, Color metal, Color fluid, bool tidal)
        {
            AddRadialGlow(buf, 32f, 38f, 21f, fluid, 0.34f);
            FillQuad(buf, new Vector2(17f, 46f), new Vector2(47f, 46f), new Vector2(40f, 28f), new Vector2(24f, 28f), metal);
            FillEllipseRot(buf, new Vector2(32f, 46f), 13f, 4.5f, 0f, fluid, fluid * 1.2f);
            DrawSegment(buf, new Vector2(32f, 28f), new Vector2(32f, 15f), 2.5f, metal);
            DrawSegment(buf, new Vector2(22f, 14f), new Vector2(42f, 14f), 2.2f, metal);
            if (tidal) DrawRing(buf, 32f, 46f, 18f, 1.4f, fluid);
        }

        private static void DrawJar(Color[] buf, Color clay, Color moss)
        {
            FillCircle(buf, 32f, 27f, 14f, clay);
            FillEllipseRot(buf, new Vector2(32f, 40f), 12f, 6f, 0f, moss, moss * 1.2f);
            FillCircle(buf, 27f, 42f, 4.5f, moss * 1.15f);
        }

        private static void DrawRoundBuckler(Color[] buf, Color rim, Color steel)
        {
            FillCircle(buf, 32f, 32f, 18f, rim);
            FillCircle(buf, 32f, 32f, 14f, steel * 0.72f);
            FillCircle(buf, 32f, 32f, 5.5f, steel);
        }

        private static void DrawVest(Color[] buf, Color leather, Color trim)
        {
            FillQuad(buf, new Vector2(18f, 48f), new Vector2(46f, 48f), new Vector2(43f, 15f), new Vector2(21f, 15f), leather);
            DrawSegment(buf, new Vector2(32f, 16f), new Vector2(32f, 48f), 2.0f, trim);
        }

        private static void DrawRunedSigil(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 32f, 22f, new Color(0.45f, 0.56f, 1.0f), 0.34f);
            DrawRing(buf, 32f, 32f, 18f, 2.0f, new Color(0.48f, 0.62f, 1.0f));
            FillTri(buf, new Vector2(32f, 47f), new Vector2(19f, 23f), new Vector2(45f, 23f), new Color(0.32f, 0.85f, 1.0f));
            FillCircle(buf, 32f, 31f, 4.5f, Color.white);
        }

        private static void DrawGear(Color[] buf, Color brass, Color core)
        {
            DrawRing(buf, 32f, 32f, 13f, 3.8f, brass);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector2 p = new Vector2(32f + Mathf.Cos(a) * 18f, 32f + Mathf.Sin(a) * 18f);
                FillCircle(buf, p.x, p.y, 3.2f, brass);
            }
            FillCircle(buf, 32f, 32f, 6f, core);
        }

        private static void DrawCenser(Color[] buf)
        {
            DrawSegment(buf, new Vector2(32f, 52f), new Vector2(32f, 36f), 1.4f, new Color(0.82f, 0.72f, 0.34f));
            FillCircle(buf, 32f, 27f, 11f, new Color(0.86f, 0.72f, 0.30f));
            AddRadialGlow(buf, 32f, 34f, 18f, new Color(0.45f, 0.96f, 0.68f), 0.36f);
            FillCircle(buf, 32f, 27f, 5.5f, new Color(0.42f, 0.96f, 0.64f));
        }

        private static void DrawBlightedThorn(Color[] buf)
        {
            DrawStraightSword(buf, new Color(0.46f, 0.56f, 0.48f), new Color(0.45f, 0.28f, 0.52f), 40f, wide: true, molten: false);
            FillCircle(buf, 29f, 34f, 3.2f, new Color(0.68f, 0.28f, 0.88f));
            FillCircle(buf, 39f, 40f, 3.0f, new Color(0.55f, 0.92f, 0.35f));
        }

        private static void DrawTome(Color[] buf)
        {
            FillQuad(buf, new Vector2(18f, 15f), new Vector2(46f, 17f), new Vector2(44f, 49f), new Vector2(16f, 47f), new Color(0.32f, 0.26f, 0.48f));
            DrawSegment(buf, new Vector2(21f, 16f), new Vector2(19f, 47f), 2.2f, new Color(0.88f, 0.70f, 0.28f));
            FillCircle(buf, 33f, 33f, 6f, new Color(0.40f, 0.88f, 1.0f));
        }

        private static void DrawBoot(Color[] buf)
        {
            FillQuad(buf, new Vector2(22f, 48f), new Vector2(36f, 48f), new Vector2(36f, 20f), new Vector2(20f, 20f), new Color(0.52f, 0.64f, 0.78f));
            FillQuad(buf, new Vector2(20f, 20f), new Vector2(48f, 22f), new Vector2(46f, 14f), new Vector2(20f, 14f), new Color(0.36f, 0.46f, 0.58f));
            FillTri(buf, new Vector2(19f, 42f), new Vector2(10f, 52f), new Vector2(20f, 30f), new Color(0.52f, 0.94f, 0.78f));
        }

        private static void DrawPauldron(Color[] buf)
        {
            AddRadialGlow(buf, 32f, 32f, 22f, new Color(0.95f, 0.72f, 0.26f), 0.28f);
            FillCircle(buf, 32f, 30f, 16f, new Color(0.46f, 0.50f, 0.58f));
            FillCircle(buf, 34f, 34f, 11f, new Color(0.68f, 0.72f, 0.80f));
            DrawRing(buf, 32f, 30f, 19f, 1.5f, new Color(0.96f, 0.74f, 0.28f));
        }

        // ---- Rasterization Primitives ----

        private static void BlendPixel(Color[] buf, int x, int y, Color col, float alpha)
        {
            if (x < 0 || x >= IconSize || y < 0 || y >= IconSize) return;
            int idx = y * IconSize + x;
            buf[idx] = Color.Lerp(buf[idx], col, Mathf.Clamp01(alpha));
            buf[idx].a = 1f;
        }

        private static void AddRadialGlow(Color[] buf, float cx, float cy, float radius, Color glow, float intensity)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int x1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cx + radius));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int y1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cy + radius));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d < radius)
                    {
                        float k = Mathf.Pow(1f - d / radius, 1.7f) * intensity;
                        int idx = y * IconSize + x;
                        buf[idx] = buf[idx] + glow * k;
                        buf[idx].a = 1f;
                    }
                }
            }
        }

        private static void FillCircle(Color[] buf, float cx, float cy, float r, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - 1f));
            int x1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cx + r + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - 1f));
            int y1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cy + r + 1f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    if (d <= r + 0.8f)
                    {
                        float a = Mathf.Clamp01(r + 0.8f - d);
                        BlendPixel(buf, x, y, col, a);
                    }
                }
            }
        }

        private static void DrawRing(Color[] buf, float cx, float cy, float r, float thickness, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r - thickness - 2f));
            int x1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cx + r + thickness + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r - thickness - 2f));
            int y1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(cy + r + thickness + 2f));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dist = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r);
                    if (dist <= thickness + 0.7f)
                    {
                        float a = Mathf.Clamp01((thickness + 0.7f - dist) * 1.2f) * col.a;
                        BlendPixel(buf, x, y, col, a);
                    }
                }
            }
        }

        private static void FillEllipseRot(Color[] buf, Vector2 c, float rx, float ry, float deg, Color c1, Color c2)
        {
            float rad = deg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            for (int y = 0; y < IconSize; y++)
            {
                for (int x = 0; x < IconSize; x++)
                {
                    float dx = x - c.x;
                    float dy = y - c.y;
                    float lx = dx * cos + dy * sin;
                    float ly = -dx * sin + dy * cos;
                    float q = (lx * lx) / (rx * rx) + (ly * ly) / (ry * ry);
                    if (q <= 1.0f)
                    {
                        float t = Mathf.Clamp01((lx / rx + 1f) * 0.5f);
                        BlendPixel(buf, x, y, Color.Lerp(c1, c2, t), 1f);
                    }
                }
            }
        }

        private static void DrawSegment(Color[] buf, Vector2 a, Vector2 b, float halfW, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - halfW - 2f));
            int x1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + halfW + 2f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - halfW - 2f));
            int y1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + halfW + 2f));
            Vector2 ab = b - a;
            float len2 = Vector2.Dot(ab, ab);
            if (len2 < 0.0001f) return;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    Vector2 p = new Vector2(x, y);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float d = Vector2.Distance(p, a + ab * t);
                    if (d <= halfW + 0.8f)
                    {
                        float alpha = Mathf.Clamp01(halfW + 0.8f - d);
                        BlendPixel(buf, x, y, col, alpha);
                    }
                }
            }
        }

        private static void DrawLineGlow(Color[] buf, Vector2 a, Vector2 b, Color col, float halfW)
        {
            DrawSegment(buf, a, b, halfW + 1.1f, new Color(col.r, col.g, col.b, 0.35f));
            DrawSegment(buf, a, b, halfW * 0.65f, col);
        }

        private static void FillQuad(Color[] buf, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color col)
        {
            FillTri(buf, p0, p1, p2, col);
            FillTri(buf, p0, p2, p3, col);
        }

        private static void FillTri(Color[] buf, Vector2 a, Vector2 b, Vector2 c, Color col)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int x1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int y1 = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (PointInTri(new Vector2(x + 0.5f, y + 0.5f), a, b, c))
                    {
                        BlendPixel(buf, x, y, col, 1f);
                    }
                }
            }
        }

        private static void DrawSparkle(Color[] buf, float cx, float cy, float r, Color col)
        {
            DrawSegment(buf, new Vector2(cx - r, cy), new Vector2(cx + r, cy), 0.8f, col);
            DrawSegment(buf, new Vector2(cx, cy - r), new Vector2(cx, cy + r), 0.8f, col);
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float s1 = Cross(p - a, b - a);
            float s2 = Cross(p - b, c - b);
            float s3 = Cross(p - c, a - c);
            bool hasNeg = s1 < 0f || s2 < 0f || s3 < 0f;
            bool hasPos = s1 > 0f || s2 > 0f || s3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }
}
