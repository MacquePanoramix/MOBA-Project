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
            Debug.Log("[LanePerfAndUxChecks] Stage A cache & Stage B auto-buy / recipe-tree checks passed.");
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
