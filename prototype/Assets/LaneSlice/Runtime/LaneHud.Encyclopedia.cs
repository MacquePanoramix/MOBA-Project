using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public sealed partial class LaneHud
    {
        private string compendiumSearch = string.Empty;
        private ItemBrowseTab compendiumTab = ItemBrowseTab.All;
        private ItemStatFilter compendiumStatFilter = ItemStatFilter.None;
        private int selectedCompendiumItemId = LaneItemIds.BananaBlade;
        private int selectedCompendiumPassiveIndex;
        private bool showCompendiumMath;
        private Vector2 compendiumListScroll;
        private Vector2 compendiumDetailScroll;
        private Vector2 compendiumPreviewScroll;
        private int compendiumTreeClickId;
        private float compendiumTreeClickTime;
        private readonly List<LaneItemDef> compendiumFilteredItems = new List<LaneItemDef>(48);

        internal void BenchSelectCompendiumItem(int itemId, int passiveIndex = 0)
        {
            if (LaneItems.TryGet(itemId, out LaneItemDef item))
            {
                selectedCompendiumItemId = item.Id;
                int maxP = item.Passives != null ? Mathf.Max(0, item.Passives.Length - 1) : 0;
                selectedCompendiumPassiveIndex = Mathf.Clamp(passiveIndex, 0, maxP);
                compendiumDetailScroll = Vector2.zero;
                compendiumPreviewScroll = Vector2.zero;
                SyncCompendiumPreviewSelection();
            }
        }

        partial void OnOpenItemCompendium(int itemId)
        {
            if (itemId > 0 && LaneItems.TryGet(itemId, out LaneItemDef passed))
            {
                selectedCompendiumItemId = passed.Id;
            }
            else if (!LaneItems.TryGet(selectedCompendiumItemId, out _))
            {
                selectedCompendiumItemId = LaneItemIds.BananaBlade;
            }
            selectedCompendiumPassiveIndex = 0;
            compendiumDetailScroll = Vector2.zero;
            compendiumPreviewScroll = Vector2.zero;
            SyncCompendiumPreviewSelection();
        }

        private void SyncCompendiumPreviewSelection(int exampleIndex = 0)
        {
            if (!LaneItems.TryGet(selectedCompendiumItemId, out LaneItemDef item))
            {
                SelectedCompendiumItemHasPassive = false;
                scene.AbilityPreview?.Deactivate();
                return;
            }
            bool hasPassive = item.Passives != null && item.Passives.Length > 0;
            SelectedCompendiumItemHasPassive = hasPassive;
            if (!hasPassive)
            {
                selectedCompendiumPassiveIndex = 0;
                scene.AbilityPreview?.Deactivate();
                return;
            }
            selectedCompendiumPassiveIndex = Mathf.Clamp(selectedCompendiumPassiveIndex, 0, item.Passives.Length - 1);
            scene.AbilityPreview?.SelectItemPassive(item.Id, selectedCompendiumPassiveIndex, exampleIndex);
        }

        partial void DrawEncyclopediaDrawer(bool inMatch)
        {
            EnsureStyles();
            if (blurTextInputRequested)
            {
                GUI.FocusControl(null);
                blurTextInputRequested = false;
            }

            float margin = 12f;
            var r = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);
            TrackPointer(r);
            LaneUiCommon.DrawBorderedPanel(r, LaneUiCommon.ColBgDark, new Color(0.30f, 0.42f, 0.56f, 0.96f), 1.5f);

            float pad = 14f;
            float innerW = r.width - pad * 2f;
            GUILayout.BeginArea(new Rect(r.x + pad, r.y + 10f, innerW, r.height - 20f));

            // ---- TOP BAR ----
            GUILayout.BeginHorizontal(GUILayout.Height(28f));
            string backLabel = inMatch ? "← Back to Match" : "← Back to Lobby";
            GUI.backgroundColor = new Color(0.68f, 0.78f, 0.92f);
            if (GUILayout.Button(backLabel, GUILayout.Width(124f), GUILayout.Height(25f)))
            {
                ItemCompendiumOpen = false;
                GUI.FocusControl(null);
                scene.AbilityPreview?.Deactivate();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(8f);
            GUILayout.Label("<b><size=16>ITEM COMPENDIUM</size></b>  <color=#9fb0c2>(44 Items • Explorer & Passive Lab)</color>", sheetTextStyle);

            GUILayout.FlexibleSpace();
            GUILayout.Label("Search:", sheetSectionStyle, GUILayout.Width(48f));
            GUI.SetNextControlName("CompendiumSearchInput");
            string newSearch = GUILayout.TextField(compendiumSearch ?? string.Empty, 48, GUILayout.Width(175f), GUILayout.Height(22f));
            if (newSearch != compendiumSearch)
            {
                compendiumSearch = newSearch;
                compendiumListScroll = Vector2.zero;
            }
            if (!string.IsNullOrEmpty(compendiumSearch) && GUILayout.Button("×", GUILayout.Width(22f), GUILayout.Height(22f)))
            {
                compendiumSearch = string.Empty;
                compendiumListScroll = Vector2.zero;
            }
            GUILayout.Space(6f);

            if (GUILayout.Button("Hero Guide", GUILayout.Width(86f), GUILayout.Height(25f)))
            {
                GUI.FocusControl(null);
                OpenHeroSheet(scene.LocalPick);
            }

            bool demosOn = scene.Settings == null || scene.Settings.Data.enableAbilityDemos;
            GUI.backgroundColor = demosOn ? new Color(0.32f, 0.78f, 0.52f) : new Color(0.68f, 0.74f, 0.80f);
            if (GUILayout.Button(demosOn ? "Demos: ON" : "Demos: OFF", GUILayout.Width(94f), GUILayout.Height(25f)))
            {
                scene.Settings?.SetAbilityDemosEnabled(!demosOn);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("X", GUILayout.Width(28f), GUILayout.Height(25f)))
            {
                ItemCompendiumOpen = false;
                GUI.FocusControl(null);
                scene.AbilityPreview?.Deactivate();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6f);

            float colGap = 8f;
            float listW = Mathf.Floor(innerW * 0.275f);
            float midW = Mathf.Floor(innerW * 0.395f);
            float rightW = innerW - listW - midW - colGap * 2f;
            float bodyH = r.height - 60f;

            GUILayout.BeginHorizontal(GUILayout.Height(bodyH));

            // ---- LEFT COLUMN: CATEGORIES + 15-STAT FILTERS + CATALOG LIST ----
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(listW), GUILayout.Height(bodyH));
            DrawCompendiumBrowser(listW - 22f);
            GUILayout.EndVertical();

            GUILayout.Space(colGap);

            // ---- CENTER COLUMN: ITEM PAGE DETAILS, RECIPE TREE, BUILD PATHS & PASSIVE TABS ----
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(midW), GUILayout.Height(bodyH));
            if (!LaneItems.TryGet(selectedCompendiumItemId, out LaneItemDef sel))
            {
                sel = LaneItems.Catalog[0];
                selectedCompendiumItemId = sel.Id;
                SyncCompendiumPreviewSelection();
            }
            DrawCompendiumItemPage(sel, midW - 24f);
            GUILayout.EndVertical();

            GUILayout.Space(colGap);

            // ---- RIGHT COLUMN: INTERACTIVE PASSIVE DEMONSTRATION & RELEVANT SCALING SLIDERS ----
            GUILayout.BeginVertical(cardBoxStyle, GUILayout.Width(rightW), GUILayout.Height(bodyH));
            DrawCompendiumPreviewColumn(sel, rightW - 22f, demosOn);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawCompendiumBrowser(float colW)
        {
            // 2 rows of 3 category tabs
            for (int r = 0; r < 2; r++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 3; c++)
                {
                    ItemBrowseTab tab = ShopTabOrder[r * 3 + c];
                    bool active = compendiumTab == tab;
                    GUI.backgroundColor = active ? new Color(0.32f, 0.72f, 0.54f) : new Color(0.84f, 0.88f, 0.92f);
                    if (GUILayout.Button(LaneItemCatalogQuery.TabLabel(tab), GUILayout.Height(21f)))
                    {
                        compendiumTab = tab;
                        compendiumListScroll = Vector2.zero;
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            // 3 compact rows of 5 stat chips (15 stats + Reset)
            StatFilterMeta[] metas = LaneItemCatalogQuery.AllStatFilters;
            for (int row = 0; row < 3; row++)
            {
                GUILayout.BeginHorizontal();
                for (int c = 0; c < 5; c++)
                {
                    int idx = row * 5 + c;
                    if (idx >= metas.Length) break;
                    StatFilterMeta m = metas[idx];
                    bool active = (compendiumStatFilter & m.Flag) != 0;
                    GUI.backgroundColor = active ? LaneUiCommon.ParseHex(m.ColorHex, new Color(0.35f, 0.78f, 0.65f)) : new Color(0.75f, 0.80f, 0.86f);
                    if (GUILayout.Button(m.ShortLabel, GUILayout.Height(18f)))
                    {
                        compendiumStatFilter = active ? (compendiumStatFilter & ~m.Flag) : (compendiumStatFilter | m.Flag);
                        compendiumListScroll = Vector2.zero;
                    }
                    Rect cr = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.Repaint && cr.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip($"Filter: <b>{m.FullLabel}</b> (direct stat; click to toggle)",
                            GUIUtility.GUIToScreenPoint(cr.position), cr.size);
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            if (compendiumStatFilter != ItemStatFilter.None || !string.IsNullOrEmpty(compendiumSearch) || compendiumTab != ItemBrowseTab.All)
            {
                if (GUILayout.Button("Reset Filters", GUILayout.Height(19f)))
                {
                    compendiumTab = ItemBrowseTab.All;
                    compendiumStatFilter = ItemStatFilter.None;
                    compendiumSearch = string.Empty;
                    compendiumListScroll = Vector2.zero;
                }
            }

            GUILayout.Space(4f);
            LaneItemCatalogQuery.FilterItems(compendiumTab, compendiumStatFilter, compendiumSearch, compendiumFilteredItems);
            compendiumListScroll = GUILayout.BeginScrollView(compendiumListScroll);

            if (compendiumFilteredItems.Count == 0)
            {
                GUILayout.Space(14f);
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>No items match the current search / filters.</color>", sheetBodyStyle);
                GUILayout.EndScrollView();
                return;
            }

            for (int gi = 0; gi < LaneItemCatalogQuery.GroupOrder.Length; gi++)
            {
                ItemDisplayGroup grp = LaneItemCatalogQuery.GroupOrder[gi];
                int groupCount = 0;
                for (int i = 0; i < compendiumFilteredItems.Count; i++)
                {
                    if (LaneItemCatalogQuery.GetGroup(compendiumFilteredItems[i]) == grp) groupCount++;
                }
                if (groupCount == 0) continue;

                GUILayout.Label($"<color=#b7c6d8><b>{LaneItemCatalogQuery.GroupLabel(grp)}</b> ({groupCount})</color>", sheetSectionStyle);
                for (int i = 0; i < compendiumFilteredItems.Count; i++)
                {
                    LaneItemDef item = compendiumFilteredItems[i];
                    if (LaneItemCatalogQuery.GetGroup(in item) != grp) continue;
                    bool isSel = item.Id == selectedCompendiumItemId;

                    GUI.backgroundColor = isSel ? new Color(0.28f, 0.48f, 0.68f) : new Color(0.84f, 0.88f, 0.92f);
                    GUILayout.BeginHorizontal(sheetFactPillStyle, GUILayout.Height(36f));
                    GUI.backgroundColor = Color.white;
                    if (GUILayout.Button(LaneItemIcons.GetIcon(item.Id), GUILayout.Width(28f), GUILayout.Height(28f)))
                    {
                        SelectCompendiumItem(item.Id);
                    }
                    string tag = item.IsApproved ? "" : " <color=#b8a878>[P]</color>";
                    string passiveBadge = (item.Passives != null && item.Passives.Length > 0)
                        ? $" <color={LaneHeroSheet.ColCc}>•{item.Passives.Length}P</color>"
                        : "";
                    if (GUILayout.Button($"<b>{item.Name}</b>{tag}\n<color=#ffdf5c>{item.Cost}g</color>{passiveBadge}", sheetSectionStyle, GUILayout.Height(28f)))
                    {
                        SelectCompendiumItem(item.Id);
                    }
                    GUILayout.EndHorizontal();

                    Rect rowR = GUILayoutUtility.GetLastRect();
                    if (Event.current.type == EventType.Repaint && rowR.Contains(Event.current.mousePosition))
                    {
                        SetHoverTooltip(LaneHeroSheet.BuildItemTooltip(item, canSellNow: false),
                            GUIUtility.GUIToScreenPoint(rowR.position), rowR.size);
                    }
                }
                GUILayout.Space(4f);
            }

            GUILayout.EndScrollView();
        }

        private void SelectCompendiumItem(int itemId)
        {
            selectedCompendiumItemId = itemId;
            selectedCompendiumPassiveIndex = 0;
            compendiumDetailScroll = Vector2.zero;
            compendiumPreviewScroll = Vector2.zero;
            SyncCompendiumPreviewSelection();
        }

        private void DrawCompendiumItemPage(LaneItemDef sel, float pageW)
        {
            compendiumDetailScroll = GUILayout.BeginScrollView(compendiumDetailScroll);

            // Header
            GUILayout.BeginHorizontal();
            GUILayout.Box(LaneItemIcons.GetIcon(sel.Id), GUILayout.Width(54f), GUILayout.Height(54f));
            GUILayout.Space(8f);
            GUILayout.BeginVertical();
            string statusTag = sel.IsApproved
                ? $"<color={LaneHeroSheet.ColLive}><b>Approved</b> • {LaneItems.CategoryLabel(sel.Category)}</color>"
                : $"<color={LaneHeroSheet.ColMuted}><b>Provisional</b> • {LaneItems.CategoryLabel(sel.Category)}</color>";
            GUILayout.Label($"<size=16><b>{sel.Name}</b></size>", sheetTextStyle);
            GUILayout.Label($"{statusTag}  •  Cost: <color=#ffdf5c><b>{sel.Cost}g</b></color> <color=#9fb0c2>(Sell +{LaneItems.SellRefund(sel.Cost)}g)</color>", sheetTextStyle);
            if (!string.IsNullOrEmpty(sel.Fantasy))
            {
                GUILayout.Label($"<i><color=#b5c3d4>{sel.Fantasy}</color></i>", sheetBodyStyle);
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            // Direct Stats vs Passives strictly separated
            GUILayout.Space(6f);
            GUILayout.Label("<b>Direct Stats:</b>", sheetTextStyle);
            string directLine = FormatDirectStatsLine(sel);
            if (!string.IsNullOrEmpty(directLine))
            {
                LaneUiCommon.DrawDirectStatChips(sel, pageW - 10f);
            }
            else
            {
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>No direct base stats (effect comes from passive below).</color>", sheetSectionStyle);
            }

            // Full Interactive Recipe Tree (single click opens clicked ingredient page)
            if (sel.HasRecipe)
            {
                GUILayout.Space(6f);
                GUILayout.Label("<b>Full Recipe Tree:</b>", sheetTextStyle);
                RecipeTreeNode tree = LaneItems.BuildRecipeTree(sel.Id, null, 0);
                RecipeTreeInteraction act = LaneUiCommon.DrawInteractiveRecipeTree(
                    tree,
                    sel.Id,
                    null,
                    0,
                    shopMode: false,
                    canBuyNow: false,
                    availWidth: pageW - 8f,
                    ref compendiumTreeClickId,
                    ref compendiumTreeClickTime,
                    (tip, rLocal) => SetHoverTooltip(tip, GUIUtility.GUIToScreenPoint(rLocal.position), rLocal.size));
                if (act.Kind == RecipeTreeInteraction.ActionKind.Inspect && act.ItemId != sel.Id)
                {
                    SelectCompendiumItem(act.ItemId);
                }
            }

            // Potential Upgrade Destinations
            List<LaneItemDef> upgrades = LaneItems.GetUpgradesInto(sel.Id);
            if (upgrades.Count > 0)
            {
                GUILayout.Space(6f);
                GUILayout.Label("<b>Builds Into:</b>", sheetTextStyle);
                GUILayout.BeginHorizontal();
                for (int u = 0; u < upgrades.Count; u++)
                {
                    LaneItemDef up = upgrades[u];
                    GUILayout.BeginHorizontal(sheetFactPillStyle, GUILayout.MaxWidth(160f));
                    if (GUILayout.Button(LaneItemIcons.GetIcon(up.Id), GUILayout.Width(24f), GUILayout.Height(24f)))
                    {
                        SelectCompendiumItem(up.Id);
                    }
                    if (GUILayout.Button($"<b>{up.Name}</b>\n<color=#ffdf5c>{up.Cost}g</color>", sheetSectionStyle))
                    {
                        SelectCompendiumItem(up.Id);
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            // Named Passives & Interactive Passive Selector
            if (sel.Passives != null && sel.Passives.Length > 0)
            {
                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"<b>Passive Abilities ({sel.Passives.Length}) — Select Passive:</b>", sheetTextStyle);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(showCompendiumMath ? "Hide Exact Math" : "Show Exact Math", GUILayout.Width(118f), GUILayout.Height(22f)))
                {
                    showCompendiumMath = !showCompendiumMath;
                }
                GUILayout.EndHorizontal();

                int count = sel.Passives.Length;
                int perRow = count <= 2 ? count : 2;
                for (int start = 0; start < count; start += perRow)
                {
                    GUILayout.BeginHorizontal();
                    for (int p = start; p < Mathf.Min(count, start + perRow); p++)
                    {
                        bool active = selectedCompendiumPassiveIndex == p;
                        GUI.backgroundColor = active ? new Color(0.32f, 0.78f, 0.56f) : new Color(0.82f, 0.86f, 0.90f);
                        if (GUILayout.Button(sel.Passives[p].Name, GUILayout.Height(24f)))
                        {
                            selectedCompendiumPassiveIndex = p;
                            SyncCompendiumPreviewSelection();
                        }
                    }
                    GUI.backgroundColor = Color.white;
                    GUILayout.EndHorizontal();
                }

                GUILayout.Space(4f);
                int pIdx = Mathf.Clamp(selectedCompendiumPassiveIndex, 0, count - 1);
                string tip = LaneUiCommon.GetPracticalTip(sel.Id, pIdx);
                LaneUiCommon.DrawPassiveCard(
                    sel.Passives[pIdx],
                    showCompendiumMath,
                    sheetFactPillStyle,
                    sheetTextStyle,
                    sheetBodyStyle,
                    sheetMathBoxStyle,
                    tip,
                    sheetTipBoxStyle);
            }
            else
            {
                GUILayout.Space(8f);
                GUILayout.BeginVertical(sheetFactPillStyle);
                GUILayout.Label($"<b>Stat-Focused Item</b>", sheetTextStyle);
                GUILayout.Label(
                    upgrades.Count > 0
                        ? "This component grants pure direct statistics with no extra passive triggers, building smoothly into higher-tier items above."
                        : "This starter item provides direct baseline stats with no passive effects.",
                    sheetBodyStyle);
                GUILayout.EndVertical();
            }

            GUILayout.EndScrollView();
        }

        private void DrawCompendiumPreviewColumn(LaneItemDef sel, float rightW, bool demosOn)
        {
            if (sel.Passives == null || sel.Passives.Length == 0)
            {
                GUILayout.Label("<b><size=14>STAT ITEM OVERVIEW</size></b>", sheetTextStyle);
                GUILayout.Space(8f);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Box(LaneItemIcons.GetIcon(sel.Id), GUILayout.Width(64f), GUILayout.Height(64f));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Space(6f);
                GUILayout.Label($"<b>{sel.Name}</b> (<color=#ffdf5c>{sel.Cost}g</color>)", sheetTextStyle);
                GUILayout.Label(sel.Summary, sheetBodyStyle);
                GUILayout.Space(6f);
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Pure stat items keep their reference clean without unnecessary simulation demos or sliders. Select any passive-bearing item (e.g. Banana Blade, The Giant's Heart, Draconic Blade) to inspect its live scenario.</color>", sheetBodyStyle);
                return;
            }

            int pIdx = Mathf.Clamp(selectedCompendiumPassiveIndex, 0, sel.Passives.Length - 1);
            ItemPassiveDoc activePassive = sel.Passives[pIdx];
            GUILayout.Label($"<b><size=14>PASSIVE LAB — {activePassive.Name.ToUpperInvariant()}</size></b>", sheetTextStyle);

            LaneAbilityPreview preview = scene.AbilityPreview;
            string[] exNames = LaneAbilityPreview.GetItemExampleNames(sel.Id, pIdx);
            if (exNames.Length > 1 && preview != null)
            {
                GUILayout.BeginHorizontal();
                for (int e = 0; e < exNames.Length; e++)
                {
                    bool actEx = preview.ExampleIndex == e;
                    GUI.backgroundColor = actEx ? new Color(0.95f, 0.78f, 0.35f) : Color.white;
                    if (GUILayout.Button(exNames[e], GUILayout.Height(21f)))
                    {
                        SyncCompendiumPreviewSelection(e);
                    }
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(4f);
            float boxW = Mathf.Max(220f, rightW - 8f);
            float boxH = Mathf.Clamp(boxW * 0.58f, 155f, 224f);
            Rect texRect = GUILayoutUtility.GetRect(boxW, boxH);
            DrawBox(texRect, new Color(0.05f, 0.07f, 0.10f, 1f));

            if (demosOn && preview != null && preview.PreviewTexture != null)
            {
                GUI.DrawTexture(texRect, preview.PreviewTexture, ScaleMode.ScaleToFit, false);
                preview.DrawOverlayBars(texRect, sheetSectionStyle);
            }
            else
            {
                GUI.Label(
                    new Rect(texRect.x + 12f, texRect.y + texRect.height * 0.30f, texRect.width - 24f, 68f),
                    $"<color={LaneHeroSheet.ColMuted}>Animated item demonstrations are disabled.\nExplanation, scaling bullets, and exact math remain fully readable.</color>",
                    sheetTextStyle);
                if (!demosOn && GUI.Button(new Rect(texRect.x + 12f, texRect.y + texRect.height * 0.66f, 150f, 24f), "Enable Animated Demos"))
                {
                    scene.Settings?.SetAbilityDemosEnabled(true);
                }
            }

            if (preview != null)
            {
                GUILayout.Space(3f);
                GUILayout.BeginHorizontal();
                GUI.backgroundColor = preview.IsPaused ? new Color(0.84f, 0.68f, 0.30f) : new Color(0.32f, 0.76f, 0.52f);
                if (GUILayout.Button(preview.IsPaused ? "Play" : "Pause", GUILayout.Width(64f), GUILayout.Height(21f)))
                {
                    preview.IsPaused = !preview.IsPaused;
                }
                GUI.backgroundColor = Color.white;
                if (GUILayout.Button("Replay", GUILayout.Width(64f), GUILayout.Height(21f)))
                {
                    preview.IsPaused = false;
                    preview.RestartScenario();
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"<color={LaneHeroSheet.ColMuted}>Loop {preview.ElapsedInLoop:0.0}s / {preview.LoopDuration:0.0}s</color>", sheetSectionStyle);
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(preview.ScenarioCaption))
                {
                    GUILayout.Label($"<color=#dce7cf>{preview.ScenarioCaption}</color>", sheetTextStyle);
                }

                GUILayout.Space(4f);
                compendiumPreviewScroll = GUILayout.BeginScrollView(compendiumPreviewScroll);

                List<PreviewSliderSpec> sliders = LaneAbilityPreview.GetRelevantItemSliders(sel.Id, pIdx);
                if (sliders.Count == 0)
                {
                    GUILayout.BeginVertical(sheetFactPillStyle);
                    GUILayout.Label($"<color={LaneHeroSheet.ColMuted}><b>Fixed Passive Mechanics:</b> No scaling stat sliders apply to {activePassive.Name}.</color>", sheetBodyStyle);
                    GUILayout.EndVertical();
                }
                else
                {
                    PreviewStatOverrides ov = preview.Overrides;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("<b>Bonus Stat Sliders (stacks onto item):</b>", sheetSectionStyle);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Reset", GUILayout.Width(56f), GUILayout.Height(20f)))
                    {
                        preview.ResetItemOverrides();
                    }
                    GUILayout.EndHorizontal();

                    for (int si = 0; si < sliders.Count; si++)
                    {
                        PreviewSliderSpec spec = sliders[si];
                        float cur = LaneAbilityPreview.GetOverrideValue(in ov, spec.Field);
                        string valText = spec.AsPercent ? $"+{cur * 100f:0}%" : $"+{cur:0}";
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"<color={spec.ColorHex}><b>{spec.Label}</b></color>", sheetTextStyle, GUILayout.Width(116f));
                        float raw = GUILayout.HorizontalSlider(cur, spec.Min, spec.Max);
                        float snapped = Mathf.Clamp(Mathf.Round(raw / spec.Step) * spec.Step, spec.Min, spec.Max);
                        GUILayout.Label($"<b>{valText}</b>", sheetTextStyle, GUILayout.Width(44f));
                        GUILayout.EndHorizontal();
                        if (Mathf.Abs(snapped - cur) > 0.0001f)
                        {
                            LaneAbilityPreview.SetOverrideValue(ref ov, spec.Field, snapped);
                            preview.ApplyLiveOverrides(ov);
                        }
                    }
                }

                GUILayout.EndScrollView();
            }
        }
    }
}
