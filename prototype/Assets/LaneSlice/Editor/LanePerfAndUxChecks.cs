using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moba.LaneSlice
{
    public static class LanePerfAndUxChecks
    {
        public static void Run()
        {
            CheckHeroGuideCacheMatchesStatelessAndNoLeaks();
            CheckAutoPurchaseAllPart25Scenarios();
            CheckRecipeTreeInvariantsAndDuplicateFeedback();
            CheckStageCIconsAndCatalogQuery();
            CheckStageDEItemCompendiumAndPassiveDemos();
            Debug.Log("[LanePerfAndUxChecks] Stage A cache, Stage B auto-buy/recipe-tree, Stage C icons/query, & Stage D/E compendium/passive-demo checks passed.");
        }

        private static void CheckHeroGuideCacheMatchesStatelessAndNoLeaks()
        {
            var tuning = new LaneTuning();
            var cache = new LaneHeroSheet.HeroGuideCache();
            PreviewStatOverrides ov = LaneAbilityPreview.DefaultOverrides(LaneAbilitySlot.R);
            ov.bonusStrength = 40f;
            ov.bonusAgility = 25f;

            LaneHeroSheet.HeroGuideData cached = cache.GetOrBuild(LaneHeroId.Ogre, tuning, null, true, ov, out HeroSnap cachedSnap);
            HeroSnap directSnap = LaneHeroSheet.EvaluatePreviewHeroSnap(LaneHeroId.Ogre, tuning, ov);
            LaneHeroSheet.HeroGuideData directGuide = LaneHeroSheet.BuildHeroGuide(LaneHeroId.Ogre, tuning, directSnap);

            Assert(cached != null && directGuide != null, "Guides built cleanly");
            Assert(cached.Cards.Count == directGuide.Cards.Count, "Cached guide card count matches stateless");
            Assert(Mathf.Abs(cachedSnap.strength - directSnap.strength) < 0.01f, "Cached preview snap STR matches stateless");
            Assert(cached.Cards[1].LiveLine == directGuide.Cards[1].LiveLine, "Cached guide LiveLine matches stateless 1:1");

            // Switching stats invalidates cleanly
            ov.bonusStrength = 90f;
            LaneHeroSheet.HeroGuideData updated = cache.GetOrBuild(LaneHeroId.Ogre, tuning, null, true, ov, out HeroSnap updatedSnap);
            Assert(!ReferenceEquals(updated, cached), "Cache invalidates when slider overrides change");
            Assert(Mathf.Abs(updatedSnap.strength - 90f) < 0.01f, "Updated snap reflects new slider STR");
        }

        private static void CheckAutoPurchaseAllPart25Scenarios()
        {
            var t = new LaneTuning { waveFirstSpawnDelay = 999f, passiveGoldPerSecond = 0f, startingGold = 0 };
            var m = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);

            // 1. Insufficient gold even for cheapest component (0g)
            m.SetHeroGoldForTest(LaneTeam.Blue, 0);
            Assert(!m.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out List<int> bought0),
                "0g auto-purchase Heartstone rejected");
            Assert(bought0.Count == 0, "0 items bought at 0g");

            // 2. One affordable component (250g -> 1x Life Crystal 220g)
            m.SetHeroGoldForTest(LaneTeam.Blue, 250);
            Assert(m.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out List<int> bought1),
                "250g buys 1 Life Crystal toward Heartstone");
            Assert(bought1.Count == 1 && bought1[0] == LaneItemIds.LifeCrystal, "Bought 1 Life Crystal");
            HeroSnap b1 = GetHero(m.Snapshot(), LaneTeam.Blue);
            Assert(b1.gold == 30, $"Remaining gold 30g after 220g spend, got {b1.gold}");
            Assert(LaneItems.ResolvePurchase(LaneItemIds.Heartstone, b1.items, 9999).RemainingCost == 340,
                "Remaining cost for Heartstone is 340g");

            // 3. Exact prompt example: 0 owned, 440g on Heartstone (560g) -> buys 2x Life Crystal, leaves 120g upgrade
            var m2 = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            m2.SetHeroGoldForTest(LaneTeam.Blue, 440);
            Assert(m2.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out List<int> bought2),
                "440g buys both Life Crystals");
            Assert(bought2.Count == 2 && bought2[0] == LaneItemIds.LifeCrystal && bought2[1] == LaneItemIds.LifeCrystal,
                "Exact 2x Life Crystal auto-purchased");
            HeroSnap b2 = GetHero(m2.Snapshot(), LaneTeam.Blue);
            Assert(b2.gold == 0, "All 440g spent cleanly");
            Assert(LaneItems.ResolvePurchase(LaneItemIds.Heartstone, b2.items, 120).RemainingCost == 120,
                "120g remains to complete Heartstone");
            m2.SetHeroGoldForTest(LaneTeam.Blue, 120);
            Assert(m2.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out List<int> completeHs),
                "120g finishes Heartstone from 2x Life Crystal");
            Assert(completeHs.Count == 1 && completeHs[0] == LaneItemIds.Heartstone, "Completed Heartstone");
            HeroSnap b2Done = GetHero(m2.Snapshot(), LaneTeam.Blue);
            Assert(CountId(b2Done.items, LaneItemIds.Heartstone) == 1 && CountId(b2Done.items, LaneItemIds.LifeCrystal) == 0,
                "Consumed both Life Crystals cleanly");

            // 4. Intermediate priority: 560g on Giant's Heart (0 owned) buys Heartstone directly in 1 slot (not 2x Life Crystal)
            var m3 = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            m3.SetHeroGoldForTest(LaneTeam.Blue, 560);
            Assert(m3.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.GiantsHeart, out _, out List<int> intermedBuy),
                "560g auto-purchases Heartstone directly toward Giant's Heart");
            Assert(intermedBuy.Count == 1 && intermedBuy[0] == LaneItemIds.Heartstone,
                "Intermediate Heartstone prioritized over 2x Life Crystal leaves");

            // 5. Slot consolidation upgrade priority: owning Small Axe + 360g targeting Executioner's Axe upgrades to Heavy Axe (340g)
            var m4 = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            m4.SetHeroGoldForTest(LaneTeam.Blue, 240);
            Assert(m4.TryBuyItem(LaneTeam.Blue, LaneItemIds.SmallAxe, out _), "Seed Small Axe");
            m4.SetHeroGoldForTest(LaneTeam.Blue, 360);
            Assert(m4.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.ExecutionersAxe, out _, out List<int> upgradePriority),
                "Upgrades Small Axe -> Heavy Axe");
            Assert(upgradePriority.Count == 1 && upgradePriority[0] == LaneItemIds.HeavyAxe,
                "Slot consolidation chose Heavy Axe upgrade instead of buying loose Small Axe");
            HeroSnap b4 = GetHero(m4.Snapshot(), LaneTeam.Blue);
            Assert(b4.gold == 20 && CountId(b4.items, LaneItemIds.HeavyAxe) == 1 && CountId(b4.items, LaneItemIds.SmallAxe) == 0,
                "Heavy Axe replaced Small Axe cleanly without wasting slot");

            // 6. Nested recipe full completion + repeated smart-buy safety (zero overbuy / zero gold exploit)
            m4.SetHeroGoldForTest(LaneTeam.Blue, 970); // Executioner remaining = 1550 - 580 = 970g
            Assert(m4.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.ExecutionersAxe, out _, out List<int> finishExec),
                "Remaining 970g completes Executioner's Axe");
            Assert(finishExec.Count == 1 && finishExec[0] == LaneItemIds.ExecutionersAxe, "Finished Executioner's Axe");
            HeroSnap b4Done = GetHero(m4.Snapshot(), LaneTeam.Blue);
            Assert(b4Done.gold == 0 && CountId(b4Done.items, LaneItemIds.ExecutionersAxe) == 1, "Exact gold spent");

            // Repeated BuyItemSmart with 300g for ANOTHER Executioner's Axe shouldn't touch already-completed slot 0
            m4.SetHeroGoldForTest(LaneTeam.Blue, 300);
            Assert(m4.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.ExecutionersAxe, out _, out List<int> secondAxe),
                "Second target Executioner's Axe uses remaining free slots independently");
            Assert(secondAxe.Count == 1 && secondAxe[0] == LaneItemIds.Titanstone, "Buys Titanstone for 2nd target");
            HeroSnap b4After = GetHero(m4.Snapshot(), LaneTeam.Blue);
            Assert(b4After.items[0] == LaneItemIds.ExecutionersAxe, "Existing unrelated/completed item preserved untouched");

            // 7. Full 6/6 unrelated inventory blocks new component purchase with 0 gold lost & 0 items sold
            var mFull = new LaneMatch(LaneHeroId.Miravel, LaneHeroId.Ogre, t);
            mFull.SetHeroGoldForTest(LaneTeam.Blue, 2000);
            for (int k = 0; k < 6; k++) Assert(mFull.TryBuyItem(LaneTeam.Blue, LaneItemIds.IronDagger, out _), "Fill 6/6 slots");
            HeroSnap preFull = GetHero(mFull.Snapshot(), LaneTeam.Blue);
            Assert(!mFull.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out _),
                "6/6 full unrelated inventory blocks Heartstone auto-buy");
            HeroSnap postFull = GetHero(mFull.Snapshot(), LaneTeam.Blue);
            Assert(postFull.gold == preFull.gold && CountId(postFull.items, LaneItemIds.IronDagger) == 6,
                "Full inventory never sells or overwrites unrelated items or spends gold");

            // 8. 6/6 full inventory holding 1 ingredient CAN still upgrade in-place via TryBuyItemSmart
            Assert(mFull.TrySellItem(LaneTeam.Blue, 0, out _), "Sell slot 0 for upgrade test");
            mFull.SetHeroGoldForTest(LaneTeam.Blue, 620);
            Assert(mFull.TryBuyItem(LaneTeam.Blue, LaneItemIds.LifeCrystal, out _), "Buy Life Crystal into slot 0 (now 6/6)");
            Assert(mFull.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Heartstone, out _, out List<int> inPlaceUpgrade),
                "6/6 holding Life Crystal can smart-upgrade to Heartstone");
            Assert(inPlaceUpgrade.Count == 1 && inPlaceUpgrade[0] == LaneItemIds.Heartstone, "In-place 6/6 upgrade works");
            HeroSnap postInPlace = GetHero(mFull.Snapshot(), LaneTeam.Blue);
            Assert(postInPlace.items[0] == LaneItemIds.Heartstone && CountId(postInPlace.items, LaneItemIds.IronDagger) == 5 && postInPlace.gold == 60,
                "In-place upgrade preserved 5 unrelated Iron Daggers and exact 340g cost");

            // 9. Out-of-fountain rejection & retired/invalid IDs
            m4.ConfigurePreviewHero(LaneTeam.Blue, Vector2.zero, 90f); // center lane, out of fountain
            m4.SetHeroGoldForTest(LaneTeam.Blue, 2000);
            Assert(!m4.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.Titanstone, out _, out _), "Rejected outside fountain while alive");
            m4.ConfigurePreviewHero(LaneTeam.Blue, t.blueFountainPos, 90f);
            Assert(!m4.TryBuyItemSmart(LaneTeam.Blue, 19, out _, out _), "Retired ID 19 rejected");
            Assert(!m4.TryBuyItemSmart(LaneTeam.Blue, 9999, out _, out _), "Unknown ID rejected");

            // 10. Anti-gold-duplication invariant: sell all refund <= total spent across multi-step auto-buy
            var mGold = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, t);
            mGold.SetHeroGoldForTest(LaneTeam.Blue, 1000);
            Assert(mGold.TryBuyItemSmart(LaneTeam.Blue, LaneItemIds.BananaBlade, out _, out _), "Partial Banana Blade buy");
            HeroSnap gSnap = GetHero(mGold.Snapshot(), LaneTeam.Blue);
            int totalRefund = 0;
            for (int s = 0; s < gSnap.items.Length; s++)
            {
                if (gSnap.items[s] > 0)
                {
                    Assert(mGold.TrySellItem(LaneTeam.Blue, s, out _), "Sell bought component");
                    totalRefund += LaneItems.TryGet(gSnap.items[s], out LaneItemDef ld) ? LaneItems.SellRefund(ld.Cost) : 0;
                }
            }
            HeroSnap gAfterSell = GetHero(mGold.Snapshot(), LaneTeam.Blue);
            Assert(gAfterSell.gold <= 1000, $"Sell-all gold ({gAfterSell.gold}) never exceeds initial 1000g (anti-exploit)");
            Assert(totalRefund == gAfterSell.gold - gSnap.gold, "Refund arithmetic exact");
        }

        private static void CheckRecipeTreeInvariantsAndDuplicateFeedback()
        {
            // Heartstone with 1/2 Life Crystals & 250g -> root PartialAffordable (need 340g), child Life Crystal 1/2 PartialAffordable (220g <= 250g)
            int[] inv1 = { LaneItemIds.LifeCrystal, 0, 0, 0, 0, 0 };
            RecipeTreeNode hsTree = LaneItems.BuildRecipeTree(LaneItemIds.Heartstone, inv1, 250);
            Assert(hsTree != null && hsTree.Children.Count == 1, "Heartstone has 1 ingredient entry (2x Life Crystal)");
            RecipeTreeNode lcNode = hsTree.Children[0];
            Assert(lcNode.RequiredQuantity == 2 && lcNode.OwnedDirectCount == 1 && lcNode.MissingCopies == 1,
                $"Duplicate feedback shows 1/2 owned and 1 missing (got {lcNode.OwnedDirectCount}/{lcNode.RequiredQuantity})");
            Assert(lcNode.State == RecipeNodeState.PartialAffordable, "Remaining copy is affordable with 250g");

            // Giant's Heart with Heartstone owned -> Heartstone Owned & LifeCrystal children SatisfiedByParent
            int[] invHs = { LaneItemIds.Heartstone, 0, 0, 0, 0, 0 };
            RecipeTreeNode ghTree = LaneItems.BuildRecipeTree(LaneItemIds.GiantsHeart, invHs, 300);
            Assert(ghTree != null && ghTree.Children.Count == 3, "Giant's Heart has 3 direct branches (no recipe scroll node)");
            RecipeTreeNode hsChild = ghTree.Children[0];
            Assert(hsChild.Item.Id == LaneItemIds.Heartstone && hsChild.State == RecipeNodeState.Owned, "Heartstone node marked Owned");
            Assert(hsChild.Children[0].SatisfiedByParent && hsChild.Children[0].IsSatisfied && hsChild.Children[0].State == RecipeNodeState.Owned,
                "Sub-components under owned intermediate marked SatisfiedByParent & Owned");

            // Every item in 44-item catalog produces valid recipe tree with zero scroll nodes & valid item IDs
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                LaneItemDef item = LaneItems.Catalog[i];
                RecipeTreeNode tree = LaneItems.BuildRecipeTree(item.Id, inv1, 5000);
                Assert(tree != null && tree.Item.Id == item.Id, $"Tree built for {item.Name}");
                AssertNoScrollNodes(tree);
            }
        }

        private static void CheckStageCIconsAndCatalogQuery()
        {
            Assert(LaneItems.Catalog.Length == 44, $"Expected 44-item catalog, got {LaneItems.Catalog.Length}");
            Assert(LaneItemCatalogQuery.AllStatFilters.Length == 15,
                $"Expected all 15 stat filters, got {LaneItemCatalogQuery.AllStatFilters.Length}");

            // 1. All 44 item icons generate cleanly at 64x64
            for (int i = 0; i < LaneItems.Catalog.Length; i++)
            {
                Texture2D icon = LaneItemIcons.GetIcon(LaneItems.Catalog[i].Id);
                Assert(icon != null && icon.width == 64 && icon.height == 64,
                    $"Icon valid 64x64 for {LaneItems.Catalog[i].Name}");
            }

            // 2. Zero role lockouts & complete catalog availability
            var results = new List<LaneItemDef>(48);
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "", results);
            Assert(results.Count == 44, $"Unfiltered catalog returns all 44 items, got {results.Count}");

            // 3. Every one of the 15 stat filters matches >= 1 item
            for (int f = 0; f < LaneItemCatalogQuery.AllStatFilters.Length; f++)
            {
                StatFilterMeta meta = LaneItemCatalogQuery.AllStatFilters[f];
                LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, meta.Flag, "", results);
                Assert(results.Count >= 1, $"Stat filter {meta.FullLabel} matched >= 1 item");
            }

            // 4. Multi-stat AND filter
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.AttackDamage | ItemStatFilter.Strength, "", results);
            Assert(ContainsItem(results, LaneItemIds.ExecutionersAxe) && ContainsItem(results, LaneItemIds.DraconicBlade),
                "AD+STR filter finds Executioner's Axe and Draconic Blade");
            Assert(!ContainsItem(results, LaneItemIds.SmallAxe) && !ContainsItem(results, LaneItemIds.Titanstone),
                "AD+STR filter excludes single-stat items");

            // 5. Strict separation: direct AD filter excludes Giant's Heart; passive text search finds it
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.AttackDamage, "", results);
            Assert(!ContainsItem(results, LaneItemIds.GiantsHeart),
                "Direct AD stat filter must not misclassify Giant's Heart as direct AD");

            var gh = default(LaneItemDef);
            Assert(LaneItems.TryGet(LaneItemIds.GiantsHeart, out gh), "Lookup Giant's Heart");
            var chips = new List<(string label, string colorHex)>();
            LaneUiCommon.CollectDirectStatChips(in gh, chips);
            for (int c = 0; c < chips.Count; c++)
            {
                Assert(!chips[c].label.Contains("Attack Damage", StringComparison.OrdinalIgnoreCase),
                    "Giant's Heart direct stat chips must not claim direct Attack Damage");
            }

            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "Giant's Might", results);
            Assert(ContainsItem(results, LaneItemIds.GiantsHeart), "Text search finds Giant's Heart via passive name");
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "attack damage", results);
            Assert(ContainsItem(results, LaneItemIds.GiantsHeart), "Text search 'attack damage' matches Giant's Might passive explanation");

            // 6. Multi-token & abbreviation search
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "retreat armor", results);
            Assert(ContainsItem(results, LaneItemIds.CowardsShield), "Multi-token 'retreat armor' finds The Coward's Shield");
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "rcr", results);
            Assert(ContainsItem(results, LaneItemIds.RunedSigil) && ContainsItem(results, LaneItemIds.EconomistsTome),
                "Abbreviation 'rcr' matches RCR items");
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.All, ItemStatFilter.None, "crescent strike", results);
            Assert(results.Count == 1 && results[0].Id == LaneItemIds.BananaBlade, "Crescent Strike search narrows to Banana Blade");

            // 7. Category tab counts
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.Starters, ItemStatFilter.None, "", results);
            Assert(results.Count == 5, $"Expected 5 starters, got {results.Count}");
            LaneItemCatalogQuery.FilterItems(ItemBrowseTab.Provisional, ItemStatFilter.None, "", results);
            Assert(results.Count == 17, $"Expected 17 provisional items, got {results.Count}");

            LaneItemIcons.DisposeAll();
            LaneUiCommon.DisposeAll();
        }

        private static void CheckStageDEItemCompendiumAndPassiveDemos()
        {
            var tuning = new LaneTuning();
            // Separate live match to verify 100% isolation from preview sandbox
            var liveMatch = new LaneMatch(LaneHeroId.Ogre, LaneHeroId.Miravel, tuning);
            HeroSnap liveBefore = GetHero(liveMatch.Snapshot(), LaneTeam.Blue);

            using (var preview = new LaneAbilityPreview(tuning, enableRenderVisuals: false))
            {
                int testedPassives = 0;

                // 1. Validate all 44 catalog entries & run every passive-bearing item scenario
                for (int i = 0; i < LaneItems.Catalog.Length; i++)
                {
                    LaneItemDef item = LaneItems.Catalog[i];
                    Assert(LaneItems.TryGet(item.Id, out _), $"Compendium item lookup {item.Name}");
                    if (item.Passives == null || item.Passives.Length == 0) continue;

                    for (int p = 0; p < item.Passives.Length; p++)
                    {
                        string tip = LaneUiCommon.GetPracticalTip(item.Id, p);
                        Assert(tip != null, $"Tip safe non-null for {item.Name} p{p}");

                        string[] exNames = LaneAbilityPreview.GetItemExampleNames(item.Id, p);
                        for (int ex = 0; ex < exNames.Length; ex++)
                        {
                            preview.SelectItemPassive(item.Id, p, ex);
                            Assert(preview.Mode == PreviewMode.ItemPassive && preview.SelectedItemId == item.Id,
                                $"ItemPassive mode active for {item.Name} p{p}");
                            for (int f = 0; f < 45; f++)
                            {
                                preview.Tick(1f / 30f, shouldRun: true);
                            }
                            Assert(!string.IsNullOrEmpty(preview.ScenarioCaption), $"Caption populated for {item.Name} p{p}");
                            Assert(!string.IsNullOrEmpty(preview.LiveTelemetryLine), $"Telemetry populated for {item.Name} p{p}");
                        }
                        testedPassives++;
                    }
                }
                Assert(testedPassives >= 28, $"Expected >=28 passive tabs tested across catalog, ran {testedPassives}");

                // 2. Relevant sliders & authentic mechanics: Banana Blade -> Crescent Strike (3 sliders) & secondary cleave
                List<PreviewSliderSpec> csSliders = LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.BananaBlade, 0);
                Assert(csSliders.Count == 3 &&
                       csSliders[0].Field == PreviewStatField.AttackDamage &&
                       csSliders[1].Field == PreviewStatField.Agility &&
                       csSliders[2].Field == PreviewStatField.Intelligence,
                    "Crescent Strike shows only AD, AGI, INT sliders");
                preview.SelectItemPassive(LaneItemIds.BananaBlade, 0, 0);
                for (int f = 0; f < 90; f++) preview.Tick(1f / 30f, true);
                bool anySecondaryDamaged = false;
                for (int m = 0; m < preview.LatestPreviewSnapshot.minions.Count; m++)
                {
                    if (preview.LatestPreviewSnapshot.minions[m].hp < 750f) anySecondaryDamaged = true;
                }
                Assert(anySecondaryDamaged, "Crescent Strike demo cleaved at least one secondary enemy behind primary target");

                // 3. Slippery Trickster -> Agility slider (1) & real MS burst
                Assert(LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.BananaBlade, 1).Count == 1 &&
                       LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.BananaBlade, 1)[0].Field == PreviewStatField.Agility,
                    "Slippery Trickster shows only Agility slider");
                preview.SelectItemPassive(LaneItemIds.BananaBlade, 1, 0);
                for (int f = 0; f < 30; f++) preview.Tick(1f / 30f, true);
                HeroSnap slipSnap = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                Assert(slipSnap.slipperyBonus > 0.1f, $"Slippery Trickster granted MS burst in preview ({slipSnap.slipperyBonus:0.00})");

                // 4. Weightlessness -> 0 sliders & slow strength reduced with identical duration
                Assert(LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.WingedCharm, 0).Count == 0 &&
                       LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.BananaBlade, 3).Count == 0,
                    "Weightlessness shows 0 unnecessary scaling sliders");
                preview.SelectItemPassive(LaneItemIds.BananaBlade, 3, 0);
                for (int f = 0; f < 24; f++) preview.Tick(1f / 30f, true);
                HeroSnap slowWith = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                preview.SelectItemPassive(LaneItemIds.BananaBlade, 3, 1);
                for (int f = 0; f < 24; f++) preview.Tick(1f / 30f, true);
                HeroSnap slowWithout = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                Assert(slowWith.slowAmount < slowWithout.slowAmount - 0.05f &&
                       Mathf.Abs(slowWith.slowTimer - slowWithout.slowTimer) < 0.05f,
                    $"Weightlessness reduced slow strength ({slowWith.slowAmount:0.00} vs {slowWithout.slowAmount:0.00}) at equal duration");

                // 5. Giant's Heart -> Ancient Vitality (deferred pool) & Giant's Might (+3% MaxHP -> AD) & Immovable Mountain
                Assert(LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.GiantsHeart, 0).Count == 2,
                    "Ancient Vitality shows MaxHp + STR sliders");
                preview.SelectItemPassive(LaneItemIds.GiantsHeart, 0, 0);
                for (int f = 0; f < 30; f++) preview.Tick(1f / 30f, true);
                HeroSnap avSnap = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                Assert(avSnap.deferredDamageTotal > 1f, $"Ancient Vitality populated deferred damage pool ({avSnap.deferredDamageTotal:0.0})");

                List<PreviewSliderSpec> gmSliders = LaneAbilityPreview.GetRelevantItemSliders(LaneItemIds.GiantsHeart, 1);
                Assert(gmSliders.Count == 1 && gmSliders[0].Field == PreviewStatField.MaxHp, "Giant's Might shows only MaxHp slider");
                preview.SelectItemPassive(LaneItemIds.GiantsHeart, 1, 0);
                float baseAd = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue).attackDamage;
                PreviewStatOverrides gmOv = preview.Overrides;
                gmOv.bonusMaxHp = 1000f;
                preview.ApplyLiveOverrides(gmOv);
                float scaledAd = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue).attackDamage;
                Assert(Mathf.Abs((scaledAd - baseAd) - 30f) < 0.1f, $"Giant's Might +1000 MaxHP raised AD by +30.0 (got {scaledAd - baseAd:0.00})");

                preview.SelectItemPassive(LaneItemIds.GiantsHeart, 2, 0);
                for (int f = 0; f < 25; f++) preview.Tick(1f / 30f, true);
                float pushWith = Mathf.Abs(GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue).x - 0.6f);
                preview.SelectItemPassive(LaneItemIds.GiantsHeart, 2, 1);
                for (int f = 0; f < 25; f++) preview.Tick(1f / 30f, true);
                float pushWithout = Mathf.Abs(GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue).x - 0.6f);
                Assert(pushWith < pushWithout - 0.3f, $"Immovable Mountain shortened knockback ({pushWith:0.00}u vs {pushWithout:0.00}u)");

                // 6. Draconic Blade & Dragon Scale & Colossus Pauldron
                preview.SelectItemPassive(LaneItemIds.DraconicBlade, 0, 0);
                for (int f = 0; f < 60; f++) preview.Tick(1f / 30f, true);
                HeroSnap heatFoe = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Red);
                Assert(heatFoe.heatStacks >= 1 && heatFoe.armor < 8f, $"Draconic Blade stacked Heat ({heatFoe.heatStacks}) and shredded Armor");

                preview.SelectItemPassive(LaneItemIds.DragonScale, 0, 0);
                for (int f = 0; f < 45; f++) preview.Tick(1f / 30f, true);
                HeroSnap scSnap = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                Assert(scSnap.adaptiveScalesStacks >= 1 && scSnap.adaptiveScalesBonus > 0f, "Adaptive Scales stacked per-attacker resists");

                preview.SelectItemPassive(LaneItemIds.ColossusPauldron, 0, 0);
                for (int f = 0; f < 45; f++) preview.Tick(1f / 30f, true);
                HeroSnap shieldSnap = GetHero(preview.LatestPreviewSnapshot, LaneTeam.Blue);
                Assert(shieldSnap.shieldHp > 10f, $"Titan Aegis granted shield in preview ({shieldSnap.shieldHp:0.0})");

                // 7. Pause/suspension lifecycle
                int ticksAtClose = preview.SimulationTickCount;
                preview.Deactivate();
                for (int f = 0; f < 10; f++) preview.Tick(1f / 30f, shouldRun: false);
                Assert(!preview.IsActive && preview.SimulationTickCount == ticksAtClose,
                    "Preview stops completely when deactivated / Compendium closed");
            }

            HeroSnap liveAfter = GetHero(liveMatch.Snapshot(), LaneTeam.Blue);
            Assert(Mathf.Abs(liveAfter.hp - liveBefore.hp) < 0.01f && liveAfter.items[0] == 0,
                "Compendium item demos never mutate live match state");
        }

        private static bool ContainsItem(List<LaneItemDef> list, int itemId)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == itemId) return true;
            }
            return false;
        }

        private static void AssertNoScrollNodes(RecipeTreeNode node)
        {
            Assert(LaneItems.TryGet(node.Item.Id, out LaneItemDef actual), $"Tree node {node.Item.Id} is a real catalog item");
            Assert(!actual.Name.Contains("Scroll", StringComparison.OrdinalIgnoreCase) &&
                   !actual.Name.Contains("Recipe", StringComparison.OrdinalIgnoreCase),
                $"No recipe scroll node allowed ({actual.Name})");
            for (int i = 0; i < node.Children.Count; i++)
            {
                AssertNoScrollNodes(node.Children[i]);
            }
        }

        private static HeroSnap GetHero(LaneSnapshot snap, LaneTeam team)
        {
            for (int i = 0; i < snap.heroes.Count; i++)
            {
                if (snap.heroes[i].team == team) return snap.heroes[i];
            }
            return null;
        }

        private static int CountId(int[] inv, int canonicalId)
        {
            int c = 0;
            for (int i = 0; i < inv.Length; i++) if (inv[i] == canonicalId) c++;
            return c;
        }

        private static void Assert(bool cond, string msg)
        {
            if (!cond) throw new InvalidOperationException($"[LanePerfAndUxChecks] FAILED: {msg}");
        }
    }
}
