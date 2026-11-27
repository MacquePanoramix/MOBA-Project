using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public struct RecipeTreeInteraction
    {
        public enum ActionKind : byte
        {
            None = 0,
            Inspect = 1,
            SmartBuy = 2,
        }

        public ActionKind Kind;
        public int ItemId;
    }

    /// <summary>
    /// Shared IMGUI drawing helpers, cached solid textures, stat badges, passive cards,
    /// practical tips, and the interactive hierarchical Recipe Build Tree shared across
    /// the Fountain Shop and Item Compendium.
    /// </summary>
    public static class LaneUiCommon
    {
        public static readonly Color ColBgDark = new Color(0.08f, 0.10f, 0.13f, 0.98f);
        public static readonly Color ColPanel = new Color(0.12f, 0.15f, 0.19f, 0.97f);
        public static readonly Color ColPanelAlt = new Color(0.15f, 0.19f, 0.24f, 0.97f);
        public static readonly Color ColBorderSubtle = new Color(0.28f, 0.34f, 0.42f, 0.95f);
        public static readonly Color ColBorderFocus = new Color(0.95f, 0.78f, 0.32f, 1.0f);
        public static readonly Color ColOwnedBorder = new Color(0.28f, 0.88f, 0.52f, 1.0f);
        public static readonly Color ColOwnedFill = new Color(0.11f, 0.25f, 0.18f, 0.98f);
        public static readonly Color ColPartialAffBorder = new Color(0.28f, 0.84f, 0.78f, 1.0f);
        public static readonly Color ColPartialAffFill = new Color(0.11f, 0.22f, 0.25f, 0.98f);
        public static readonly Color ColPartialUnaffBorder = new Color(0.70f, 0.56f, 0.26f, 0.95f);
        public static readonly Color ColPartialUnaffFill = new Color(0.17f, 0.17f, 0.14f, 0.97f);
        public static readonly Color ColAffordBorder = new Color(0.94f, 0.76f, 0.30f, 0.98f);
        public static readonly Color ColAffordFill = new Color(0.18f, 0.18f, 0.14f, 0.97f);
        public static readonly Color ColUnaffordBorder = new Color(0.32f, 0.36f, 0.42f, 0.90f);
        public static readonly Color ColUnaffordFill = new Color(0.11f, 0.12f, 0.15f, 0.95f);

        private static readonly Dictionary<int, Texture2D> SolidTexCache = new Dictionary<int, Texture2D>();
        private static GUIStyle cachedNodeNameStyle;
        private static GUIStyle cachedNodeMetaStyle;
        private static GUIStyle cachedBadgeStyle;
        private static GUIStyle cachedChipStyle;

        public static Texture2D GetSolidTex(Color col)
        {
            Color32 c32 = (Color32)col;
            int key = (c32.r << 24) | (c32.g << 16) | (c32.b << 8) | c32.a;
            if (SolidTexCache.TryGetValue(key, out Texture2D existing) && existing != null)
            {
                return existing;
            }
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            t.SetPixel(0, 0, col);
            t.Apply(false, true);
            SolidTexCache[key] = t;
            return t;
        }

        public static void DisposeAll()
        {
            foreach (KeyValuePair<int, Texture2D> kv in SolidTexCache)
            {
                if (kv.Value != null)
                {
                    UnityEngine.Object.DestroyImmediate(kv.Value);
                }
            }
            SolidTexCache.Clear();
        }

        public static void DrawRect(Rect r, Color col)
        {
            Color prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawBorderedPanel(Rect r, Color fill, Color border, float borderW = 1f)
        {
            DrawRect(r, fill);
            DrawRect(new Rect(r.x, r.y, r.width, borderW), border);
            DrawRect(new Rect(r.x, r.yMax - borderW, r.width, borderW), border);
            DrawRect(new Rect(r.x, r.y, borderW, r.height), border);
            DrawRect(new Rect(r.xMax - borderW, r.y, borderW, r.height), border);
        }

        public static void DrawOrthoConnector(Vector2 parentBottom, Vector2 childTop, Color col, float thickness = 2f)
        {
            float midY = Mathf.Round((parentBottom.y + childTop.y) * 0.5f);
            float hTop = Mathf.Max(1f, midY - parentBottom.y);
            DrawRect(new Rect(Mathf.Round(parentBottom.x - thickness * 0.5f), parentBottom.y, thickness, hTop), col);

            float minX = Mathf.Min(parentBottom.x, childTop.x);
            float maxX = Mathf.Max(parentBottom.x, childTop.x);
            if (maxX - minX >= 0.5f)
            {
                DrawRect(new Rect(Mathf.Round(minX - thickness * 0.5f), midY - thickness * 0.5f, (maxX - minX) + thickness, thickness), col);
            }

            float hBot = Mathf.Max(1f, childTop.y - midY);
            DrawRect(new Rect(Mathf.Round(childTop.x - thickness * 0.5f), midY, thickness, hBot), col);
        }

        public static void CollectDirectStatChips(in LaneItemDef item, List<(string label, string colorHex)> dest)
        {
            dest.Clear();
            if (item.AddMaxHp != 0f) dest.Add(($"+{item.AddMaxHp:0} Max HP", LaneHeroSheet.ColHp));
            if (item.AddAttackDamage != 0f) dest.Add(($"+{item.AddAttackDamage:0} Attack Damage", LaneHeroSheet.ColAd));
            if (item.AddAttackSpeed != 0f) dest.Add(($"+{item.AddAttackSpeed * 100f:0}% Attack Speed", LaneHeroSheet.ColAgi));
            if (item.AddArmor != 0f) dest.Add(($"+{item.AddArmor:0} Armor", LaneHeroSheet.ColArm));
            if (item.AddMagicResist != 0f) dest.Add(($"+{item.AddMagicResist:0} Magic Resist", LaneHeroSheet.ColMr));
            if (item.AddMoveSpeed != 0f) dest.Add(($"+{item.AddMoveSpeed:0.##} Move Speed", LaneHeroSheet.ColCc));
            if (item.AddRegen != 0f) dest.Add(($"+{item.AddRegen:0.#} HP/s Regen", LaneHeroSheet.ColLive));
            if (item.AddHealPower != 0f) dest.Add(($"+{item.AddHealPower * 100f:0}% Heal Power", LaneHeroSheet.ColHeal));
            if (item.AddAlliedAuraRegen != 0f) dest.Add(($"+{item.AddAlliedAuraRegen:0.#} Allied Aura HP/s", LaneHeroSheet.ColHeal));
            if (item.AddAbilityHaste != 0f) dest.Add(($"+{item.AddAbilityHaste:0} Ability Haste", LaneHeroSheet.ColInt));
            if (item.AddStrength != 0f) dest.Add(($"+{item.AddStrength:0} STR", LaneHeroSheet.ColStr));
            if (item.AddAgility != 0f) dest.Add(($"+{item.AddAgility:0} AGI", LaneHeroSheet.ColAgi));
            if (item.AddIntelligence != 0f) dest.Add(($"+{item.AddIntelligence:0} INT", LaneHeroSheet.ColInt));
            if (item.AddMaxMana != 0f) dest.Add(($"+{item.AddMaxMana:0} Max Mana", LaneHeroSheet.ColMana));
            if (item.AddManaRegen != 0f) dest.Add(($"+{item.AddManaRegen:0.#} Mana/s", LaneHeroSheet.ColMana));
            if (item.AddPercentManaRegen != 0f) dest.Add(($"+{item.AddPercentManaRegen * 100f:0.##}% Max Mana/s", LaneHeroSheet.ColMana));
            if (item.AddResourceCostReduction != 0f) dest.Add(($"+{item.AddResourceCostReduction:0} RCR", LaneHeroSheet.ColMana));
        }

        private static readonly List<(string label, string colorHex)> ScratchChips = new List<(string, string)>(16);

        public static void DrawDirectStatChips(in LaneItemDef item, float availWidth)
        {
            EnsureInternalStyles();
            CollectDirectStatChips(in item, ScratchChips);
            if (ScratchChips.Count == 0) return;

            float pad = 4f;
            float rowH = 22f;
            float rowGap = 4f;
            float x = 0f;
            int rows = 1;
            for (int i = 0; i < ScratchChips.Count; i++)
            {
                float w = Mathf.Min(availWidth, cachedChipStyle.CalcSize(new GUIContent(ScratchChips[i].label)).x + 14f);
                if (x > 0f && x + w > availWidth)
                {
                    rows++;
                    x = 0f;
                }
                x += w + pad;
            }

            float totalH = rows * rowH + Mathf.Max(0, rows - 1) * rowGap;
            Rect area = GUILayoutUtility.GetRect(availWidth, totalH, GUILayout.ExpandWidth(true));
            float cx = area.x;
            float cy = area.y;
            for (int i = 0; i < ScratchChips.Count; i++)
            {
                string lbl = ScratchChips[i].label;
                string hex = ScratchChips[i].colorHex;
                float w = Mathf.Min(area.width, cachedChipStyle.CalcSize(new GUIContent(lbl)).x + 14f);
                if (cx > area.x && cx + w > area.xMax)
                {
                    cx = area.x;
                    cy += rowH + rowGap;
                }
                var chipR = new Rect(cx, cy, w, rowH);
                Color accent = ParseHex(hex, new Color(0.6f, 0.7f, 0.85f));
                DrawBorderedPanel(chipR, new Color(0.08f, 0.11f, 0.15f, 0.96f), new Color(accent.r, accent.g, accent.b, 0.55f), 1f);
                GUI.Label(chipR, $"<color={hex}><b>{lbl}</b></color>", cachedChipStyle);
                cx += w + pad;
            }
        }

        public static void DrawPassiveCard(
            ItemPassiveDoc doc,
            bool showMath,
            GUIStyle pillStyle,
            GUIStyle textStyle,
            GUIStyle bodyStyle,
            GUIStyle mathBoxStyle,
            string practicalTip = null,
            GUIStyle tipBoxStyle = null)
        {
            if (doc == null) return;
            GUILayout.BeginVertical(pillStyle);
            GUILayout.Label($"<color={LaneHeroSheet.ColCc}><b>Passive — {doc.Name}</b></color>", textStyle);
            GUILayout.Label(doc.PlainSummary, bodyStyle);
            if (doc.ScalingArrows != null && doc.ScalingArrows.Length > 0)
            {
                GUILayout.Space(2);
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}><b>What Scales:</b></color>", bodyStyle);
                for (int i = 0; i < doc.ScalingArrows.Length; i++)
                {
                    GUILayout.Label($"  • {doc.ScalingArrows[i]}", bodyStyle);
                }
            }
            if (!string.IsNullOrEmpty(practicalTip) && tipBoxStyle != null)
            {
                GUILayout.Space(3);
                GUILayout.Label($"<color=#b5efb5><b>Tip:</b></color> {practicalTip}", tipBoxStyle);
            }
            if (showMath && !string.IsNullOrEmpty(doc.MathLine))
            {
                GUILayout.Space(3);
                GUILayout.BeginVertical(mathBoxStyle);
                GUILayout.Label($"<color=#b6c4d4>{doc.MathLine}</color>", bodyStyle);
                GUILayout.EndVertical();
            }
            GUILayout.EndVertical();
        }

        private sealed class TreeSlotLayout
        {
            public RecipeTreeNode Node;
            public RecipeTreeNode Parent;
            public Rect Rect;
            public int ParentLayoutIdx;
        }

        private static readonly List<TreeSlotLayout> Depth1Layout = new List<TreeSlotLayout>(8);
        private static readonly List<TreeSlotLayout> Depth2Layout = new List<TreeSlotLayout>(16);

        /// <summary>
        /// Draws the interactive hierarchical recipe tree for a target item.
        /// Single left-click returns Inspect; right-click or double-left-click (<= 0.35s) in shop mode returns SmartBuy.
        /// Never creates or shows a purchasable recipe-scroll node.
        /// </summary>
        public static RecipeTreeInteraction DrawInteractiveRecipeTree(
            RecipeTreeNode root,
            int focusedItemId,
            int[] inventory,
            int currentGold,
            bool shopMode,
            bool canBuyNow,
            float availWidth,
            ref int lastClickItemId,
            ref float lastClickTime,
            Action<string, Rect> setTooltip)
        {
            var interaction = default(RecipeTreeInteraction);
            if (root == null) return interaction;
            EnsureInternalStyles();

            bool hasDepth1 = root.Children.Count > 0;
            bool hasDepth2 = false;
            Depth1Layout.Clear();
            Depth2Layout.Clear();

            float w = Mathf.Max(280f, availWidth);
            float rootW = Mathf.Min(164f, w - 16f);
            float nodeH = 44f;
            float rowGap = 20f;
            float topPad = 6f;

            var rootRect = new Rect(Mathf.Round((w - rootW) * 0.5f), topPad, rootW, nodeH);

            if (hasDepth1)
            {
                int c1 = root.Children.Count;
                float gap1 = c1 >= 4 ? 6f : 10f;
                float nW1 = Mathf.Clamp((w - 12f - gap1 * (c1 - 1)) / c1, 86f, 128f);
                float totalW1 = c1 * nW1 + (c1 - 1) * gap1;
                float x1Start = (w - totalW1) * 0.5f;
                float y1 = topPad + nodeH + rowGap;

                for (int i = 0; i < c1; i++)
                {
                    RecipeTreeNode child = root.Children[i];
                    var r1 = new Rect(Mathf.Round(x1Start + i * (nW1 + gap1)), y1, nW1, nodeH);
                    Depth1Layout.Add(new TreeSlotLayout
                    {
                        Node = child,
                        Parent = root,
                        ParentLayoutIdx = -1,
                        Rect = r1,
                    });

                    for (int g = 0; g < child.Children.Count; g++)
                    {
                        hasDepth2 = true;
                        Depth2Layout.Add(new TreeSlotLayout
                        {
                            Node = child.Children[g],
                            Parent = child,
                            ParentLayoutIdx = i,
                        });
                    }
                }

                if (hasDepth2)
                {
                    int c2 = Depth2Layout.Count;
                    float gap2 = 6f;
                    float nW2 = Mathf.Clamp((w - 12f - gap2 * (c2 - 1)) / c2, 84f, 118f);
                    float y2 = y1 + nodeH + rowGap;

                    // Ideal X centered below each grandchild's parent, then relax horizontally to eliminate overlap
                    float[] xPos = new float[c2];
                    for (int i = 0; i < c2; i++)
                    {
                        TreeSlotLayout sl = Depth2Layout[i];
                        Rect pRect = Depth1Layout[sl.ParentLayoutIdx].Rect;
                        int sibCount = sl.Parent.Children.Count;
                        int sibIdx = 0;
                        for (int s = 0; s < sibCount; s++)
                        {
                            if (ReferenceEquals(sl.Parent.Children[s], sl.Node))
                            {
                                sibIdx = s;
                                break;
                            }
                        }
                        float offset = (sibIdx - (sibCount - 1) * 0.5f) * (nW2 + gap2);
                        xPos[i] = Mathf.Clamp(pRect.center.x - nW2 * 0.5f + offset, 4f, w - nW2 - 4f);
                    }
                    for (int pass = 0; pass < 4; pass++)
                    {
                        for (int i = 1; i < c2; i++)
                        {
                            float minAllowed = xPos[i - 1] + nW2 + gap2;
                            if (xPos[i] < minAllowed) xPos[i] = minAllowed;
                        }
                        if (xPos[c2 - 1] + nW2 > w - 4f)
                        {
                            xPos[c2 - 1] = w - 4f - nW2;
                            for (int i = c2 - 2; i >= 0; i--)
                            {
                                float maxAllowed = xPos[i + 1] - nW2 - gap2;
                                if (xPos[i] > maxAllowed) xPos[i] = maxAllowed;
                            }
                        }
                    }
                    for (int i = 0; i < c2; i++)
                    {
                        TreeSlotLayout sl = Depth2Layout[i];
                        sl.Rect = new Rect(Mathf.Round(xPos[i]), y2, nW2, nodeH);
                    }
                }
            }

            int rowCount = 1 + (hasDepth1 ? 1 : 0) + (hasDepth2 ? 1 : 0);
            float treeAreaH = topPad * 2f + rowCount * nodeH + Mathf.Max(0, rowCount - 1) * rowGap;
            Rect area = GUILayoutUtility.GetRect(w, treeAreaH, GUILayout.ExpandWidth(true));
            Vector2 off = area.position;

            // 1. Draw orthogonal connector lines underneath nodes
            if (Event.current.type == EventType.Repaint)
            {
                for (int i = 0; i < Depth1Layout.Count; i++)
                {
                    TreeSlotLayout sl = Depth1Layout[i];
                    Color lineCol = sl.Node.IsSatisfied
                        ? new Color(0.28f, 0.84f, 0.52f, 0.85f)
                        : new Color(0.45f, 0.54f, 0.66f, 0.85f);
                    DrawOrthoConnector(
                        new Vector2(off.x + rootRect.center.x, off.y + rootRect.yMax),
                        new Vector2(off.x + sl.Rect.center.x, off.y + sl.Rect.y),
                        lineCol);
                }
                for (int i = 0; i < Depth2Layout.Count; i++)
                {
                    TreeSlotLayout sl = Depth2Layout[i];
                    Rect pR = Depth1Layout[sl.ParentLayoutIdx].Rect;
                    Color lineCol = sl.Node.IsSatisfied
                        ? new Color(0.28f, 0.84f, 0.52f, 0.85f)
                        : new Color(0.45f, 0.54f, 0.66f, 0.85f);
                    DrawOrthoConnector(
                        new Vector2(off.x + pR.center.x, off.y + pR.yMax),
                        new Vector2(off.x + sl.Rect.center.x, off.y + sl.Rect.y),
                        lineCol);
                }
            }

            // 2. Draw interactive node boxes & process clicks cleanly
            HandleTreeNode(root, OffsetRect(rootRect, off), isRoot: true, focusedItemId, shopMode, canBuyNow,
                ref lastClickItemId, ref lastClickTime, ref interaction, setTooltip);
            for (int i = 0; i < Depth1Layout.Count; i++)
            {
                HandleTreeNode(Depth1Layout[i].Node, OffsetRect(Depth1Layout[i].Rect, off), isRoot: false, focusedItemId, shopMode, canBuyNow,
                    ref lastClickItemId, ref lastClickTime, ref interaction, setTooltip);
            }
            for (int i = 0; i < Depth2Layout.Count; i++)
            {
                HandleTreeNode(Depth2Layout[i].Node, OffsetRect(Depth2Layout[i].Rect, off), isRoot: false, focusedItemId, shopMode, canBuyNow,
                    ref lastClickItemId, ref lastClickTime, ref interaction, setTooltip);
            }

            // 3. Plain recipe summary line (zero recipe-scroll item objects!)
            string footer = hasDepth1
                ? (shopMode
                    ? $"<color={LaneHeroSheet.ColMuted}>Combine Fee: <b>{root.CombineCost}g</b>  •  Total: <b>{root.Item.Cost}g</b>  •  Remaining: <color=#63e6be><b>{root.DirectRemainingCost}g</b></color>  (Click: Focus • Right/2x Click: Buy)</color>"
                    : $"<color={LaneHeroSheet.ColMuted}>Combine Fee: <b>{root.CombineCost}g</b>  •  Total Cost: <b>{root.Item.Cost}g</b>  (Click any node to open its page)</color>")
                : (shopMode
                    ? $"<color={LaneHeroSheet.ColMuted}>Base Item • Total Cost: <b>{root.Item.Cost}g</b>  (Click: Focus • Right/2x Click: Buy)</color>"
                    : $"<color={LaneHeroSheet.ColMuted}>Base Item • Total Cost: <b>{root.Item.Cost}g</b></color>");
            GUILayout.Label(footer, cachedNodeMetaStyle);

            return interaction;
        }

        private static void HandleTreeNode(
            RecipeTreeNode node,
            Rect r,
            bool isRoot,
            int focusedItemId,
            bool shopMode,
            bool canBuyNow,
            ref int lastClickItemId,
            ref float lastClickTime,
            ref RecipeTreeInteraction result,
            Action<string, Rect> setTooltip)
        {
            bool focused = focusedItemId == node.Item.Id;
            bool hovered = r.Contains(Event.current.mousePosition);

            GetNodeColors(node.State, shopMode, out Color fill, out Color border);
            if (focused) border = ColBorderFocus;
            if (hovered) fill = Color.Lerp(fill, Color.white, 0.08f);

            DrawBorderedPanel(r, fill, border, focused ? 2f : 1.3f);

            float iconSz = r.width < 100f ? 26f : 30f;
            var iconR = new Rect(r.x + 5f, r.y + (r.height - iconSz) * 0.5f, iconSz, iconSz);
            GUI.DrawTexture(iconR, LaneItemIcons.GetIcon(node.Item.Id), ScaleMode.ScaleToFit);

            // Top-right corner quantity / check pill (compute before name truncation so text never collides)
            string corner = null;
            Color cornerBg = new Color(0.08f, 0.11f, 0.14f, 0.92f);
            if (!isRoot && node.RequiredQuantity > 1)
            {
                corner = shopMode ? $"{node.OwnedDirectCount}/{node.RequiredQuantity}" : $"×{node.RequiredQuantity}";
                cornerBg = node.OwnedDirectCount >= node.RequiredQuantity
                    ? new Color(0.14f, 0.56f, 0.30f, 0.95f)
                    : new Color(0.24f, 0.28f, 0.34f, 0.95f);
            }
            else if (shopMode && node.IsSatisfied)
            {
                corner = "✓";
                cornerBg = new Color(0.14f, 0.56f, 0.30f, 0.95f);
            }
            float cw = !string.IsNullOrEmpty(corner) ? (corner.Length > 2 ? 26f : 18f) : 0f;

            float textX = iconR.xMax + 5f;
            float nameW = Mathf.Max(20f, r.xMax - textX - 4f - (cw > 0f ? cw + 2f : 0f));
            float metaW = Mathf.Max(20f, r.xMax - textX - 4f);
            int maxNameChars = nameW < 62f ? 10 : (nameW < 78f ? 13 : 17);
            string shortName = AbbreviateNodeName(node.Item.Name, maxNameChars);
            GUI.Label(new Rect(textX, r.y + 4f, nameW, 20f), $"<b>{shortName}</b>", cachedNodeNameStyle);

            string priceTxt;
            if (!shopMode)
            {
                priceTxt = node.RequiredQuantity > 1
                    ? $"<color=#ffdf5c>{node.Item.Cost}g</color> <color=#9fb0c2>×{node.RequiredQuantity}</color>"
                    : $"<color=#ffdf5c>{node.Item.Cost}g</color>";
            }
            else if (node.IsSatisfied)
            {
                priceTxt = "<color=#78ff9e><b>OWNED ✓</b></color>";
            }
            else if (node.DirectRemainingCost < node.Item.Cost)
            {
                priceTxt = $"<color=#63e6be><b>{node.DirectRemainingCost}g</b></color> <color=#8898a8>({node.Item.Cost})</color>";
            }
            else
            {
                string col = node.DirectCanPurchase ? "#ffdf5c" : "#a8b4c0";
                priceTxt = $"<color={col}>{node.Item.Cost}g</color>";
            }
            GUI.Label(new Rect(textX, r.y + 22f, metaW, 18f), priceTxt, cachedNodeMetaStyle);

            if (!string.IsNullOrEmpty(corner))
            {
                var cR = new Rect(r.xMax - cw - 2f, r.y + 2f, cw, 14f);
                DrawRect(cR, cornerBg);
                GUI.Label(cR, $"<b>{corner}</b>", cachedBadgeStyle);
            }

            if (hovered && Event.current.type == EventType.Repaint && setTooltip != null)
            {
                string tip = LaneHeroSheet.BuildItemTooltip(node.Item, shopMode && canBuyNow, shopMode ? node.DirectRemainingCost : -1);
                setTooltip(tip, r);
            }

            if (hovered && Event.current.type == EventType.MouseDown)
            {
                if (shopMode && Event.current.button == 1)
                {
                    result.Kind = RecipeTreeInteraction.ActionKind.SmartBuy;
                    result.ItemId = node.Item.Id;
                    lastClickItemId = node.Item.Id;
                    lastClickTime = Time.unscaledTime;
                    Event.current.Use();
                }
                else if (Event.current.button == 0)
                {
                    float now = Time.unscaledTime;
                    if (shopMode && lastClickItemId == node.Item.Id && (now - lastClickTime) <= 0.35f)
                    {
                        result.Kind = RecipeTreeInteraction.ActionKind.SmartBuy;
                        result.ItemId = node.Item.Id;
                        lastClickItemId = 0;
                    }
                    else
                    {
                        result.Kind = RecipeTreeInteraction.ActionKind.Inspect;
                        result.ItemId = node.Item.Id;
                        lastClickItemId = node.Item.Id;
                        lastClickTime = now;
                    }
                    Event.current.Use();
                }
            }
        }

        private static void GetNodeColors(RecipeNodeState state, bool shopMode, out Color fill, out Color border)
        {
            if (!shopMode)
            {
                fill = new Color(0.13f, 0.16f, 0.21f, 0.96f);
                border = new Color(0.36f, 0.48f, 0.62f, 0.95f);
                return;
            }
            switch (state)
            {
                case RecipeNodeState.Owned:
                    fill = ColOwnedFill;
                    border = ColOwnedBorder;
                    break;
                case RecipeNodeState.PartialAffordable:
                    fill = ColPartialAffFill;
                    border = ColPartialAffBorder;
                    break;
                case RecipeNodeState.PartialUnaffordable:
                    fill = ColPartialUnaffFill;
                    border = ColPartialUnaffBorder;
                    break;
                case RecipeNodeState.MissingAffordable:
                    fill = ColAffordFill;
                    border = ColAffordBorder;
                    break;
                default:
                    fill = ColUnaffordFill;
                    border = ColUnaffordBorder;
                    break;
            }
        }

        private static Rect OffsetRect(Rect r, Vector2 off) => new Rect(r.x + off.x, r.y + off.y, r.width, r.height);

        private static string AbbreviateNodeName(string name, int maxChars)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= maxChars) return name ?? string.Empty;
            return name.Substring(0, Mathf.Max(4, maxChars - 1)) + "…";
        }

        public static string GetPracticalTip(int itemId, int passiveIndex)
        {
            switch (itemId)
            {
                case LaneItemIds.Bloodstone:
                case LaneItemIds.ExecutionersAxe:
                    return "Save burst combos or Ogre [R] follow-ups until the foe drops below ~50% HP where missing-HP amplification peaks.";
                case LaneItemIds.CowardsShield:
                    return "Kite or disengage straight away from the attacker to lock in full bonus Armor & MR; turning back to chase decays protection twice as fast.";
                case LaneItemIds.ChaliceOfLife:
                    return passiveIndex == 0
                        ? "Hold self-heals until low HP to multiply restoration—especially strong paired with Lingering Restoration and Heal Power."
                        : "Big non-overheal heals create the largest 3.5s echo; avoid burning heal cooldowns at full Health.";
                case LaneItemIds.HealingElixir:
                    return "Actual Health restored triggers the 3.5s echo, so casting heals while hurt maximizes total sustain.";
                case LaneItemIds.DraconicBlade:
                    if (passiveIndex == 0) return "Sustained basic attacks stack Heat (up to 8); mix INT with Attack Speed to scale burn DPS and uptime.";
                    if (passiveIndex == 1) return "Keep hitting the frontliner to keep shared Heat stacks shredding both Armor and Magic Resist for your team.";
                    if (passiveIndex == 2) return "Direct hits build Adaptive Scales per enemy hero separately—DoTs do not grant stacks.";
                    return "Missing Health boosts your baseline HP/s even while disengaging.";
                case LaneItemIds.GiantsHeart:
                    if (passiveIndex == 0) return "Ancient Vitality smooths burst hits evenly over time so your HP regeneration and heals can catch up.";
                    if (passiveIndex == 1) return "Stacking bonus Max Health directly converts into genuine Attack Damage (boosting both basics and AD skills).";
                    if (passiveIndex == 2) return "High Strength noticeably shortens knockback/pull distance, though stun duration itself is unchanged.";
                    return "Missing Health ramps natural regeneration further while staggered damage pays out.";
                case LaneItemIds.BananaBlade:
                    if (passiveIndex == 0) return "Position in front of waves or clustered heroes so Crescent Strike's swept arc splashes behind your primary target.";
                    if (passiveIndex == 1) return "Hero damage triggers the burst of speed (14s fixed CD), letting agile skirmishers reposition cleanly.";
                    if (passiveIndex == 2) return "Hold basic attacks for 3.0s between skirmishes (casting spells is fine) to snap out an empowered opening strike.";
                    return "Weightlessness trims incoming slow severity by 25%—helpful against Ogre [W] or slow effects—while keeping stun/slow timers intact.";
                case LaneItemIds.NimbleDagger:
                    return "Only beginning a basic attack resets the 3.0s First Strike timer—abilities and movement never interrupt it.";
                case LaneItemIds.WingedCharm:
                    return "Reduces slow magnitude only (not slow duration or stuns), helping you slip out of heavy chills.";
                case LaneItemIds.DragonScale:
                    return "Tracks up to 6 direct hits per enemy hero independently, rewarding brawls against single attackers.";
                case LaneItemIds.PrimordialBlood:
                    return "Strongest in drawn-out low-HP scrambles; grants natural regen without directly magnifying ally heals.";
                case LaneItemIds.WoodlandSalve:
                case LaneItemIds.ManaDewVial:
                    return "Step out of hero damage range for 5 seconds to activate the full out-of-combat sustain pulse.";
                case LaneItemIds.KindredCenser:
                    return "Works on any hero near allies; Heal Power increases the shared aura regeneration.";
                case LaneItemIds.BlightedThorn:
                    return "Reapply any hero damage every 3.5s against heavy-sustain foes to keep -35% Grievous Wounds active.";
                case LaneItemIds.WellspringFlask:
                case LaneItemIds.AstralReservoir:
                    return "Percentage Max Mana regeneration scales naturally when combined with Max Mana items (only the stronger Astral Surge applies if upgraded).";
                case LaneItemIds.TidecallerChalice:
                    return "Provides up to +6.0 extra Mana/s as your Mana pool gets low, stabilizing sustained harass.";
                case LaneItemIds.EconomistsTome:
                    return "You still need enough Mana upfront to cast flat-cost spells; 12% of what you spent refunds right after.";
                case LaneItemIds.MomentumGreaves:
                    return "Avoid taking hero damage while rotating between waves to keep the Agile speed buildup maxed.";
                case LaneItemIds.ColossusPauldron:
                    return "Triggering a Stun/Knockback—or dipping below 45% HP—raises an immediate STR-scaled shield (12s CD).";
                default:
                    return string.Empty;
            }
        }

        private static void EnsureInternalStyles()
        {
            if (cachedNodeNameStyle == null)
            {
                cachedNodeNameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true,
                    clipping = TextClipping.Clip,
                    alignment = TextAnchor.UpperLeft,
                };
                cachedNodeNameStyle.normal.textColor = new Color(0.94f, 0.96f, 0.99f);
            }
            if (cachedNodeMetaStyle == null)
            {
                cachedNodeMetaStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    richText = true,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft,
                };
                cachedNodeMetaStyle.normal.textColor = new Color(0.84f, 0.89f, 0.94f);
            }
            if (cachedBadgeStyle == null)
            {
                cachedBadgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    richText = true,
                    alignment = TextAnchor.MiddleCenter,
                };
                cachedBadgeStyle.normal.textColor = Color.white;
            }
            if (cachedChipStyle == null)
            {
                cachedChipStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    richText = true,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(4, 4, 2, 2),
                };
                cachedChipStyle.normal.textColor = Color.white;
            }
        }

        public static Color ParseHex(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                return parsed;
            }
            return fallback;
        }
    }
}
