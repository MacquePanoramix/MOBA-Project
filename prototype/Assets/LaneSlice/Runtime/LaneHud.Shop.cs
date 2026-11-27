using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneHud
    {
        private string shopSearch = string.Empty;
        private ItemBrowseTab shopTab = ItemBrowseTab.All;
        private ItemStatFilter shopStatFilter = ItemStatFilter.None;
        private int focusedShopTreeItemId = LaneItemIds.ExecutionersAxe;
        private int shopLastGridClickId;
        private float shopLastGridClickTime;
        private int shopLastTreeClickId;
        private float shopLastTreeClickTime;
        private readonly List<LaneItemDef> shopFilteredItems = new List<LaneItemDef>(48);
        private static readonly ItemBrowseTab[] ShopTabOrder =
        {
            ItemBrowseTab.All,
            ItemBrowseTab.Starters,
            ItemBrowseTab.Basic,
            ItemBrowseTab.IntermediateSpec,
            ItemBrowseTab.Advanced,
            ItemBrowseTab.Provisional,
        };

        internal void BenchSelectShopItem(int itemId, ItemBrowseTab tab = ItemBrowseTab.All, ItemStatFilter filter = ItemStatFilter.None, string search = "")
        {
            shopTab = tab;
            shopStatFilter = filter;
            shopSearch = search ?? string.Empty;
            if (LaneItems.TryGet(itemId, out LaneItemDef d))
            {
                selectedShopItemId = d.Id;
                focusedShopTreeItemId = d.Id;
            }
            shopScroll = Vector2.zero;
            shopDetailScroll = Vector2.zero;
        }

        private void DrawShopDrawer(HeroSnap h)
        {
            EnsureStyles();
            if (blurTextInputRequested)
            {
                GUI.FocusControl(null);
                blurTextInputRequested = false;
            }

            float sw = Mathf.Clamp(Screen.width - 24f, 960f, 1240f);
            float sh = Mathf.Clamp(Screen.height - 186f, 470f, 675f);
            var r = new Rect((Screen.width - sw) * 0.5f, 48f, sw, sh);
            TrackPointer(r);
            LaneUiCommon.DrawBorderedPanel(r, LaneUiCommon.ColBgDark, new Color(0.32f, 0.42f, 0.55f, 0.96f), 1.5f);

            bool canShopHere = h.phase == LanePhase.Dead ||
                (new Vector2(h.x, h.y) - (localTeam == LaneTeam.Blue ? tuning.blueFountainPos : tuning.redFountainPos)).magnitude <= tuning.fountainRadius;

            float pad = 12f;
            float innerW = r.width - pad * 2f;
            GUILayout.BeginArea(new Rect(r.x + pad, r.y + 8f, innerW, r.height - 16f));

            // ---- TOP HEADER BAR ----
            GUILayout.BeginHorizontal(GUILayout.Height(28f));
            GUILayout.Label("<b><size=15>FOUNTAIN SHOP</size></b>", sheetTextStyle, GUILayout.Width(134f));
            GUILayout.Label($"Gold: <color=#ffdf5c><b>{h.gold}g</b></color>", sheetTextStyle, GUILayout.Width(104f));
            string availBadge = canShopHere
                ? "<color=#78ff9e><b>[Buying Active]</b></color>"
                : "<color=#ffae68><b>[Fountain Only — Browsing]</b></color>";
            GUILayout.Label(availBadge, sheetTextStyle, GUILayout.Width(196f));

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search:", sheetSectionStyle, GUILayout.Width(48f));
            GUI.SetNextControlName("ShopSearchInput");
            string newSearch = GUILayout.TextField(shopSearch ?? string.Empty, 48, GUILayout.Width(175f), GUILayout.Height(22f));
            if (newSearch != shopSearch)
            {
                shopSearch = newSearch;
                shopScroll = Vector2.zero;
            }
            if (!string.IsNullOrEmpty(shopSearch) && GUILayout.Button("×", GUILayout.Width(22f), GUILayout.Height(22f)))
            {
                shopSearch = string.Empty;
                shopScroll = Vector2.zero;
            }
            GUILayout.Space(6f);
            if (GUILayout.Button("Compendium", GUILayout.Width(96f), GUILayout.Height(24f)))
            {
                int openId = selectedShopItemId;
                ShopOpen = false;
                OpenItemCompendium(openId);
            }
            if (GUILayout.Button("X", GUILayout.Width(28f), GUILayout.Height(24f)))
            {
                ShopOpen = false;
                GUI.FocusControl(null);
            }
            GUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(shopFeedback))
            {
                GUILayout.Label($"<color=#9bf0b8><b>{shopFeedback}</b></color>", sheetTextStyle);
            }

            GUILayout.Space(4f);
            float leftW = Mathf.Floor(innerW * 0.54f);
            float rightW = innerW - leftW - 10f;
            float bodyH = r.height - (string.IsNullOrEmpty(shopFeedback) ? 138f : 158f);
            bodyH = Mathf.Max(250f, bodyH);

            GUILayout.BeginHorizontal(GUILayout.Height(bodyH));

            // ---- LEFT COLUMN: CATEGORIES + 15-STAT FILTERS + GROUPED ICON GRID ----
            GUILayout.BeginVertical(GUILayout.Width(leftW), GUILayout.Height(bodyH));
            DrawShopCategoryTabs();
            DrawShopStatFilterBar();
            GUILayout.Space(3f);
            DrawShopCatalogGrid(h, leftW - 22f, canShopHere);
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            // ---- RIGHT COLUMN: BUILD-INTO + HEADER + AUTO-BUY + RECIPE TREE + DIRECT STATS vs PASSIVES ----
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(rightW), GUILayout.Height(bodyH));
            DrawShopDetailInspector(h, rightW - 44f, canShopHere);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // ---- BOTTOM PINNED INVENTORY & SELL BAR ----
            GUILayout.Space(5f);
            DrawShopInventoryBar(h, innerW, canShopHere);

            GUILayout.EndArea();
        }

        private void DrawShopCategoryTabs()
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < ShopTabOrder.Length; i++)
            {
                ItemBrowseTab tab = ShopTabOrder[i];
                bool active = shopTab == tab;
                GUI.backgroundColor = active ? new Color(0.32f, 0.72f, 0.54f) : new Color(0.84f, 0.88f, 0.92f);
                if (GUILayout.Button(LaneItemCatalogQuery.TabLabel(tab), GUILayout.Height(22f)))
                {
                    shopTab = tab;
                    shopScroll = Vector2.zero;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
        }

        private void DrawShopStatFilterBar()
        {
            StatFilterMeta[] metas = LaneItemCatalogQuery.AllStatFilters;
            // 2 compact rows of 8 + 7 stat chips (+ Reset)
            for (int row = 0; row < 2; row++)
            {
                int start = row * 8;
                int end = Mathf.Min(metas.Length, start + 8);
                GUILayout.BeginHorizontal();
                for (int i = start; i < end; i++)
                {
                    StatFilterMeta m = metas[i];
                    bool active = (shopStatFilter & m.Flag) != 0;
                    GUI.backgroundColor = active ? LaneUiCommon.ParseHex(m.ColorHex, new Color(0.35f, 0.78f, 0.65f)) : new Color(0.75f, 0.80f, 0.86f);
                    if (GUILayout.Button(m.ShortLabel, GUILayout.Height(19f)))
                    {
                        shopStatFilter = active ? (shopStatFilter & ~m.Flag) : (shopStatFilter | m.Flag);
                        shopScroll = Vector2.zero;
                    }
                    Rect cr = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.Repaint && cr.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip($"Filter: <b>{m.FullLabel}</b> (direct stat; click to toggle multi-filter)",
                            GUIUtility.GUIToScreenPoint(cr.position), cr.size);
                    }
                }
                if (row == 1)
                {
                    bool hasAny = shopStatFilter != ItemStatFilter.None || !string.IsNullOrEmpty(shopSearch) || shopTab != ItemBrowseTab.All;
                    GUI.enabled = hasAny;
                    GUI.backgroundColor = new Color(0.88f, 0.64f, 0.58f);
                    if (GUILayout.Button("Reset", GUILayout.Height(19f), GUILayout.Width(48f)))
                    {
                        shopTab = ItemBrowseTab.All;
                        shopStatFilter = ItemStatFilter.None;
                        shopSearch = string.Empty;
                        shopScroll = Vector2.zero;
                    }
                    GUI.enabled = true;
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }
        }

        private void DrawShopCatalogGrid(HeroSnap h, float gridW, bool canShopHere)
        {
            LaneItemCatalogQuery.FilterItems(shopTab, shopStatFilter, shopSearch, shopFilteredItems);
            shopScroll = GUILayout.BeginScrollView(shopScroll);

            if (shopFilteredItems.Count == 0)
            {
                GUILayout.Space(18f);
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>No items match the current search / stat filters. Click <b>Reset</b> to view all 44 items.</color>", sheetBodyStyle);
                GUILayout.EndScrollView();
                return;
            }

            int cols = gridW >= 575f ? 5 : 4;
            float tileGap = 5f;
            float tileW = Mathf.Floor((gridW - tileGap * (cols - 1)) / cols);
            float tileH = 68f;

            for (int gi = 0; gi < LaneItemCatalogQuery.GroupOrder.Length; gi++)
            {
                ItemDisplayGroup grp = LaneItemCatalogQuery.GroupOrder[gi];
                int groupCount = 0;
                for (int i = 0; i < shopFilteredItems.Count; i++)
                {
                    if (LaneItemCatalogQuery.GetGroup(shopFilteredItems[i]) == grp) groupCount++;
                }
                if (groupCount == 0) continue;

                GUILayout.Label($"<color=#b7c6d8><b>{LaneItemCatalogQuery.GroupLabel(grp)}</b> ({groupCount})</color>", sheetSectionStyle);

                int rows = Mathf.CeilToInt(groupCount / (float)cols);
                float gridH = rows * tileH + Mathf.Max(0, rows - 1) * tileGap + 4f;
                Rect area = GUILayoutUtility.GetRect(gridW, gridH, GUILayout.ExpandWidth(true));

                int slotIdx = 0;
                for (int i = 0; i < shopFilteredItems.Count; i++)
                {
                    LaneItemDef item = shopFilteredItems[i];
                    if (LaneItemCatalogQuery.GetGroup(in item) != grp) continue;

                    int col = slotIdx % cols;
                    int row = slotIdx / cols;
                    slotIdx++;

                    var tileRect = new Rect(
                        area.x + col * (tileW + tileGap),
                        area.y + row * (tileH + tileGap),
                        tileW,
                        tileH);
                    DrawShopItemTile(tileRect, item, h, canShopHere);
                }
                GUILayout.Space(4f);
            }

            GUILayout.EndScrollView();
        }

        private void DrawShopItemTile(Rect r, LaneItemDef item, HeroSnap h, bool canShopHere)
        {
            AutoPurchasePlan auto = LaneItems.ResolveAutoPurchase(item.Id, h.items, h.gold);
            int directRem = auto.InitialRemainingCost;
            bool selected = selectedShopItemId == item.Id;
            bool hovered = r.Contains(Event.current.mousePosition);
            int ownedCount = CountOwnedDirect(h.items, item.Id);

            Color fill;
            Color border;
            if (auto.BuysCompleteTarget && canShopHere)
            {
                fill = new Color(0.13f, 0.21f, 0.16f, 0.97f);
                border = new Color(0.35f, 0.86f, 0.54f, 0.95f);
            }
            else if (auto.CanBuyAnything && canShopHere)
            {
                fill = new Color(0.12f, 0.19f, 0.23f, 0.97f);
                border = new Color(0.32f, 0.78f, 0.82f, 0.92f);
            }
            else if (h.gold >= directRem)
            {
                fill = new Color(0.16f, 0.17f, 0.15f, 0.96f);
                border = new Color(0.70f, 0.62f, 0.34f, 0.85f);
            }
            else
            {
                fill = new Color(0.10f, 0.12f, 0.15f, 0.95f);
                border = new Color(0.26f, 0.31f, 0.38f, 0.90f);
            }

            if (selected) border = LaneUiCommon.ColBorderFocus;
            if (hovered) fill = Color.Lerp(fill, Color.white, 0.08f);

            LaneUiCommon.DrawBorderedPanel(r, fill, border, selected ? 2f : 1.1f);

            var iconRect = new Rect(r.x + 5f, r.y + 5f, 32f, 32f);
            GUI.DrawTexture(iconRect, LaneItemIcons.GetIcon(item.Id), ScaleMode.ScaleToFit);

            // Price stack next to icon
            float px = iconRect.xMax + 5f;
            float pw = Mathf.Max(22f, r.xMax - px - 4f);
            string priceStr = directRem < item.Cost
                ? $"<color=#63e6be><b>{directRem}g</b></color>\n<color=#8696a8>{item.Cost}</color>"
                : $"<color={(auto.CanBuyAnything ? "#ffdf5c" : "#aebbc8")}><b>{item.Cost}g</b></color>";
            GUI.Label(new Rect(px, r.y + 4f, pw, 30f), priceStr, sheetSectionStyle);

            if (ownedCount > 0)
            {
                var ownR = new Rect(r.x + 4f, r.y + 4f, 22f, 13f);
                LaneUiCommon.DrawRect(ownR, new Color(0.12f, 0.56f, 0.28f, 0.95f));
                GUI.Label(ownR, $"<size=9><b>×{ownedCount}</b></size>", sheetSectionStyle);
            }
            else if (!item.IsApproved)
            {
                var pR = new Rect(r.xMax - 18f, r.y + 3f, 15f, 13f);
                LaneUiCommon.DrawRect(pR, new Color(0.28f, 0.24f, 0.16f, 0.92f));
                GUI.Label(pR, "<color=#d6b87a><size=9><b>P</b></size></color>", sheetSectionStyle);
            }

            string clippedName = item.Name.Length > 17 ? item.Name.Substring(0, 16) + "…" : item.Name;
            GUI.Label(new Rect(r.x + 5f, r.y + 39f, r.width - 10f, 27f), $"<b>{clippedName}</b>", sheetSectionStyle);

            if (hovered && Event.current.type == EventType.Repaint)
            {
                SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(item, canShopHere, directRem),
                    GUIUtility.GUIToScreenPoint(r.position), r.size);
            }

            if (hovered && Event.current.type == EventType.MouseDown)
            {
                if (Event.current.button == 1)
                {
                    selectedShopItemId = item.Id;
                    focusedShopTreeItemId = item.Id;
                    TrySmartBuyFromShopUi(item.Id, h, canShopHere);
                    shopLastGridClickId = item.Id;
                    shopLastGridClickTime = Time.unscaledTime;
                    Event.current.Use();
                }
                else if (Event.current.button == 0)
                {
                    float now = Time.unscaledTime;
                    if (shopLastGridClickId == item.Id && (now - shopLastGridClickTime) <= 0.35f)
                    {
                        TrySmartBuyFromShopUi(item.Id, h, canShopHere);
                        shopLastGridClickId = 0;
                    }
                    else
                    {
                        selectedShopItemId = item.Id;
                        focusedShopTreeItemId = item.Id;
                        shopDetailScroll = Vector2.zero;
                        shopLastGridClickId = item.Id;
                        shopLastGridClickTime = now;
                    }
                    Event.current.Use();
                }
            }
        }

        private void DrawShopDetailInspector(HeroSnap h, float availW, bool canShopHere)
        {
            if (!LaneItems.TryGet(selectedShopItemId, out LaneItemDef sel))
            {
                sel = LaneItems.Catalog[0];
                selectedShopItemId = sel.Id;
                focusedShopTreeItemId = sel.Id;
            }

            shopDetailScroll = GUILayout.BeginScrollView(shopDetailScroll);
            GUILayout.BeginVertical(GUILayout.Width(availW));

            // 1. Builds Into strip (progression context at top, wrapped cleanly)
            List<LaneItemDef> upgrades = LaneItems.GetUpgradesInto(sel.Id);
            if (upgrades.Count > 0)
            {
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}><b>Builds Into:</b></color>", sheetSectionStyle);
                float chipW = Mathf.Floor((availW - 8f) / 3f);
                for (int rowStart = 0; rowStart < upgrades.Count; rowStart += 3)
                {
                    GUILayout.BeginHorizontal();
                    for (int u = rowStart; u < Mathf.Min(upgrades.Count, rowStart + 3); u++)
                    {
                        LaneItemDef up = upgrades[u];
                        PurchasePlan upPlan = LaneItems.ResolvePurchase(up.Id, h.items, h.gold);
                        GUILayout.BeginHorizontal(sheetFactPillStyle, GUILayout.Width(chipW), GUILayout.Height(34f));
                        if (GUILayout.Button(LaneItemIcons.GetIcon(up.Id), GUILayout.Width(24f), GUILayout.Height(26f)))
                        {
                            selectedShopItemId = up.Id;
                            focusedShopTreeItemId = up.Id;
                            shopDetailScroll = Vector2.zero;
                        }
                        string un = up.Name.Length > 15 ? up.Name.Substring(0, 14) + "…" : up.Name;
                        if (GUILayout.Button($"<b>{un}</b>\n<color=#ffdf5c>{upPlan.RemainingCost}g</color>", compactRowLabelStyle, GUILayout.Height(26f)))
                        {
                            selectedShopItemId = up.Id;
                            focusedShopTreeItemId = up.Id;
                            shopDetailScroll = Vector2.zero;
                        }
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(4f);
            }

            // 2. Selected Item Header
            AutoPurchasePlan auto = LaneItems.ResolveAutoPurchase(sel.Id, h.items, h.gold);
            GUILayout.BeginHorizontal();
            GUILayout.Box(LaneItemIcons.GetIcon(sel.Id), GUILayout.Width(52f), GUILayout.Height(52f));
            GUILayout.Space(6f);
            GUILayout.BeginVertical();
            string approvalTag = sel.IsApproved
                ? $"<color={LaneHeroSheet.ColLive}>[Approved • {LaneItems.CategoryLabel(sel.Category)}]</color>"
                : $"<color={LaneHeroSheet.ColMuted}>[Provisional • {LaneItems.CategoryLabel(sel.Category)}]</color>";
            GUILayout.Label($"<size=15><b>{sel.Name}</b></size>  {approvalTag}", sheetTextStyle);
            string costLine = auto.InitialRemainingCost < sel.Cost
                ? $"Remaining: <color=#63e6be><b>{auto.InitialRemainingCost}g</b></color> <color=#99a4b0>(Total {sel.Cost}g)</color>"
                : $"Cost: <color=#ffdf5c><b>{sel.Cost}g</b></color>";
            GUILayout.Label(costLine, sheetTextStyle);
            if (!string.IsNullOrEmpty(sel.Fantasy))
            {
                GUILayout.Label($"<i><color=#b5c3d4>{sel.Fantasy}</color></i>", sheetTextStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            // 3. Primary Smart-Buy CTA + Show Math
            GUILayout.BeginHorizontal(GUILayout.Width(availW));
            GUI.enabled = canShopHere && auto.CanBuyAnything;
            GUI.backgroundColor = (canShopHere && auto.CanBuyAnything)
                ? (auto.BuysCompleteTarget ? new Color(0.28f, 0.80f, 0.45f) : new Color(0.28f, 0.72f, 0.82f))
                : new Color(0.55f, 0.58f, 0.62f);
            string ctaLabel = auto.CanBuyAnything
                ? auto.ActionLabel
                : (canShopHere ? auto.ActionLabel : $"Return to Fountain ({auto.InitialRemainingCost}g needed)");
            float mathBtnW = 86f;
            float ctaW = Mathf.Max(140f, availW - mathBtnW - 6f);
            if (GUILayout.Button(ctaLabel, GUILayout.Width(ctaW), GUILayout.Height(28f)))
            {
                TrySmartBuyFromShopUi(sel.Id, h, canShopHere);
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
            if (GUILayout.Button(showShopMath ? "Hide Math" : "Show Math", GUILayout.Width(mathBtnW), GUILayout.Height(28f)))
            {
                showShopMath = !showShopMath;
            }
            GUILayout.EndHorizontal();

            // 4. Interactive Recipe Build Tree (shown whenever item has ingredients, or as 1-node tree for direct inspection)
            GUILayout.Space(5f);
            GUILayout.Label("<b>Recipe Tree:</b>", sheetTextStyle);
            RecipeTreeNode tree = LaneItems.BuildRecipeTree(sel.Id, h.items, h.gold);
            RecipeTreeInteraction treeAct = LaneUiCommon.DrawInteractiveRecipeTree(
                tree,
                focusedShopTreeItemId,
                h.items,
                h.gold,
                shopMode: true,
                canBuyNow: canShopHere,
                availWidth: availW - 6f,
                ref shopLastTreeClickId,
                ref shopLastTreeClickTime,
                (tip, rLocal) => SetHoverTooltip(tip, GUIUtility.GUIToScreenPoint(rLocal.position), rLocal.size));

            if (treeAct.Kind == RecipeTreeInteraction.ActionKind.Inspect)
            {
                focusedShopTreeItemId = treeAct.ItemId;
            }
            else if (treeAct.Kind == RecipeTreeInteraction.ActionKind.SmartBuy)
            {
                focusedShopTreeItemId = treeAct.ItemId;
                TrySmartBuyFromShopUi(treeAct.ItemId, h, canShopHere);
            }

            // Focused component quick-action strip when a tree sub-node is selected
            if (focusedShopTreeItemId != sel.Id && LaneItems.TryGet(focusedShopTreeItemId, out LaneItemDef focusDef))
            {
                AutoPurchasePlan focusAuto = LaneItems.ResolveAutoPurchase(focusDef.Id, h.items, h.gold);
                GUILayout.BeginHorizontal(sheetFactPillStyle);
                GUILayout.Box(LaneItemIcons.GetIcon(focusDef.Id), GUILayout.Width(24f), GUILayout.Height(24f));
                GUILayout.Label($"<b>{focusDef.Name}</b> (<color=#ffdf5c>{focusAuto.InitialRemainingCost}g</color>)", sheetTextStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Inspect", GUILayout.Width(60f), GUILayout.Height(22f)))
                {
                    selectedShopItemId = focusDef.Id;
                    shopDetailScroll = Vector2.zero;
                }
                GUI.enabled = canShopHere && focusAuto.CanBuyAnything;
                if (GUILayout.Button(focusAuto.CanBuyAnything ? $"Buy ({focusAuto.TotalGoldSpent}g)" : "Need Gold/Slot", GUILayout.Width(104f), GUILayout.Height(22f)))
                {
                    TrySmartBuyFromShopUi(focusDef.Id, h, canShopHere);
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }

            // 5. Direct Stats vs Passives strictly separated
            GUILayout.Space(5f);
            GUILayout.Label("<b>Direct Stats:</b>", sheetTextStyle);
            string directLine = FormatDirectStatsLine(sel);
            if (!string.IsNullOrEmpty(directLine))
            {
                LaneUiCommon.DrawDirectStatChips(sel, availW - 10f);
            }
            else
            {
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>No direct base stats (effect comes from passive below).</color>", sheetSectionStyle);
            }

            if (sel.Passives != null && sel.Passives.Length > 0)
            {
                GUILayout.Space(5f);
                GUILayout.Label("<b>Passive Effects:</b>", sheetTextStyle);
                for (int p = 0; p < sel.Passives.Length; p++)
                {
                    LaneUiCommon.DrawPassiveCard(sel.Passives[p], showShopMath, sheetFactPillStyle, sheetTextStyle, sheetBodyStyle, sheetMathBoxStyle);
                }
            }

            GUILayout.EndVertical();
            GUILayout.EndScrollView();
        }

        private void DrawShopInventoryBar(HeroSnap h, float innerW, bool canShopHere)
        {
            float slotW = Mathf.Floor((innerW - 120f - 5f * 5f) / LaneItems.MaxSlots);
            GUILayout.BeginHorizontal(cardBoxStyle, GUILayout.Height(62f));
            int owned = 0;
            for (int i = 0; i < h.items.Length; i++) if (h.items[i] > 0) owned++;
            GUILayout.Label($"<b>Inventory ({owned}/{LaneItems.MaxSlots})</b>\n<color=#9fb0c2>Sell {Mathf.RoundToInt(LaneItems.SellRefundRatio * 100f)}%</color>",
                sheetSectionStyle, GUILayout.Width(100f));

            for (int s = 0; s < LaneItems.MaxSlots; s++)
            {
                int itemId = s < h.items.Length ? h.items[s] : 0;
                if (itemId > 0 && LaneItems.TryGet(itemId, out LaneItemDef ownedDef))
                {
                    int refund = LaneItems.SellRefund(ownedDef.Cost);
                    GUILayout.BeginHorizontal(sheetFactPillStyle, GUILayout.Width(slotW), GUILayout.Height(46f));
                    if (GUILayout.Button(LaneItemIcons.GetIcon(ownedDef.Id), GUILayout.Width(32f), GUILayout.Height(36f)))
                    {
                        selectedShopItemId = ownedDef.Id;
                        focusedShopTreeItemId = ownedDef.Id;
                    }
                    GUILayout.BeginVertical();
                    string sn = ownedDef.Name.Length > 13 ? ownedDef.Name.Substring(0, 12) + "…" : ownedDef.Name;
                    if (GUILayout.Button($"<b>{sn}</b>", compactRowLabelStyle, GUILayout.Height(18f)))
                    {
                        selectedShopItemId = ownedDef.Id;
                        focusedShopTreeItemId = ownedDef.Id;
                    }
                    GUI.enabled = canShopHere;
                    GUI.backgroundColor = new Color(0.88f, 0.74f, 0.44f);
                    if (GUILayout.Button($"Sell +{refund}g", compactBtnStyle, GUILayout.Height(19f)))
                    {
                        scene.SendLocalSellItem(s);
                        shopFeedback = $"Sold {ownedDef.Name} (+{refund}g)";
                        shopFeedbackTimer = 2.0f;
                    }
                    GUI.backgroundColor = Color.white;
                    GUI.enabled = true;
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    Rect boxR = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.Repaint && boxR.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(ownedDef, canShopHere),
                            GUIUtility.GUIToScreenPoint(boxR.position), boxR.size);
                    }
                }
                else
                {
                    GUILayout.Box($"<color=#566475>Empty {s + 1}</color>", sheetFactPillStyle, GUILayout.Width(slotW), GUILayout.Height(46f));
                }
            }
            GUILayout.EndHorizontal();
        }

        private void TrySmartBuyFromShopUi(int targetItemId, HeroSnap h, bool canShopHere)
        {
            if (!LaneItems.TryGet(targetItemId, out LaneItemDef target)) return;
            if (!canShopHere)
            {
                shopFeedback = "Move inside your Fountain (or wait while Dead) to buy items.";
                shopFeedbackTimer = 2.0f;
                return;
            }
            AutoPurchasePlan plan = LaneItems.ResolveAutoPurchase(target.Id, h.items, h.gold);
            if (!plan.CanBuyAnything)
            {
                shopFeedback = plan.ActionLabel;
                shopFeedbackTimer = 2.0f;
                return;
            }
            scene.SendLocalBuyItemSmart(target.Id);
            shopFeedback = $"Ordered {plan.ActionLabel}";
            shopFeedbackTimer = 2.2f;
        }
    }
}
