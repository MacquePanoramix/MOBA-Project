using System;
using UnityEngine;

namespace Moba.Duel
{
    public static class DuelMatchChecks
    {
        public static void Run()
        {
            MovementAndBasic();
            SlamAndImmunity();
            SweepAndTree();
            OrbContests();
            Knockout();
            Debug.Log("Duel match checks passed: movement, basic attack, cast geometry, crowd control, orb contests and knockout.");
        }

        private static DuelTuning CombatTuning() => new DuelTuning { orbFirstDelay = 500f };

        private static void MovementAndBasic()
        {
            var tuning = CombatTuning();
            var game = new DuelMatch(tuning);
            Vector2 destination = new Vector2(-6f, 1f);
            game.Command(DuelHero.Ogre, DuelOrder.Move, destination);
            game.Command(DuelHero.Ogre, DuelOrder.Move, DuelArena.TreeCenters[0]);
            game.Command(DuelHero.Ogre, DuelOrder.Move, new Vector2(float.NaN, 0f));
            game.Tick(4f);
            Require(Vector2.Distance(game.Snapshot().ogre.position, destination) < 0.02f,
                "A blocked or nonfinite click disrupted a legal destination.");
            game.Command(DuelHero.Ogre, DuelOrder.Basic, game.Snapshot().luis.position);
            Require(game.Snapshot().ogre.action == 0 && !game.Snapshot().ogre.chasing, "Ogre acquired a basic attack.");

            game.Command(DuelHero.Luis, DuelOrder.Basic, destination);
            WaitForBasic(game);
            game.Tick(tuning.basicWindup * 0.3f);
            game.Command(DuelHero.Luis, DuelOrder.Move, new Vector2(-3f, 0f));
            game.Tick(1f);
            Close(game.Snapshot().ogre.hp, tuning.ogreMaxHp, "Cancelled basic applied damage.");

            game.Command(DuelHero.Luis, DuelOrder.Basic, game.Snapshot().ogre.position);
            WaitForBasic(game);
            game.Command(DuelHero.Luis, DuelOrder.Basic, game.Snapshot().ogre.position);
            game.Tick(3f);
            Close(game.Snapshot().ogre.hp, tuning.ogreMaxHp - tuning.basicDamage, "A single order did not land exactly once.");
        }

        private static void WaitForBasic(DuelMatch game)
        {
            for (int i = 0; i < 400; i++)
            {
                game.Tick(1f / 30f);
                if (game.Snapshot().luis.action == (int)DuelOrder.Basic) return;
            }
            throw new InvalidOperationException("No basic attack after approaching and turning.");
        }

        private static void SlamAndImmunity()
        {
            var tuning = CombatTuning();
            var game = AtPositions(tuning, new Vector2(-1.7f, 0f), new Vector2(1.7f, 0f));
            game.Command(DuelHero.Ogre, DuelOrder.Slam, new Vector2(5f, 0f));
            Require(game.Snapshot().ogre.immune && !game.TryStun(DuelHero.Ogre, 1f),
                "Stun interrupted the protected wind-up.");
            game.Tick(tuning.slamWindup * 0.8f);
            Close(game.Snapshot().luis.hp, tuning.luisMaxHp, "Slam dealt damage before impact.");
            game.Tick(tuning.slamWindup * 0.2f + 0.02f);
            var hit = game.Snapshot();
            Close(hit.luis.hp, tuning.luisMaxHp - tuning.slamDamage, "Slam missed an enemy in its rectangle.");
            Require(hit.luis.stun > 0f && hit.ogre.recovering && game.TryStun(DuelHero.Ogre, 0.2f),
                "Slam stun or provisional recovery vulnerability failed.");
            game.Command(DuelHero.Luis, DuelOrder.Move, new Vector2(0f, 1f));
            game.Tick(0.15f);
            Require(Vector2.Distance(hit.luis.position, game.Snapshot().luis.position) < 0.01f,
                "Stunned combatant moved.");
            game.Tick(1f);
            game.Command(DuelHero.Ogre, DuelOrder.Slam, Vector2.right);
            Require(game.Snapshot().ogre.action == 0 && game.Snapshot().ogre.qCooldown > 0f,
                "A second slam ignored cooldown.");
            game.Tick(4f);
            float previousHp = game.Snapshot().luis.hp;
            game.Command(DuelHero.Ogre, DuelOrder.Slam, new Vector2(-1.7f, 3f));
            game.Tick(2f);
            Close(game.Snapshot().luis.hp, previousHp, "A parallel enemy outside the aimed rectangle took damage.");
        }

        private static void SweepAndTree()
        {
            var tuning = CombatTuning();
            var game = AtPositions(tuning, new Vector2(-1.1f, 0f), new Vector2(1.1f, 0f));
            game.Command(DuelHero.Ogre, DuelOrder.Sweep, new Vector2(-6f, 0f));
            game.Tick(4f);
            Close(game.Snapshot().luis.hp, tuning.luisMaxHp, "A backwards sweep hit its opponent.");
            game.Command(DuelHero.Ogre, DuelOrder.Sweep, new Vector2(4f, 0f));
            game.Tick(1f);
            var swept = game.Snapshot();
            Close(swept.luis.hp, tuning.luisMaxHp - tuning.sweepDamage, "A sweep in range missed.");
            Close(swept.luis.position.x, 1.1f + tuning.sweepPush, "Clear lane shove distance mismatch.");

            Vector2 nearTree = new Vector2(3.2f, -1.45f);
            game = AtPositions(tuning, new Vector2(3.2f, -0.4f), nearTree, 6f);
            game.Command(DuelHero.Ogre, DuelOrder.Sweep, nearTree);
            game.Tick(1f);
            Vector2 tree = new Vector2(3.2f, -2.85f);
            float clearance = Vector2.Distance(game.Snapshot().luis.position, tree);
            Require(clearance > DuelArena.UnitRadius + DuelArena.TreeRadius &&
                clearance < DuelArena.UnitRadius + DuelArena.TreeRadius + 0.02f, "Shove clipped or crossed a tree.");
        }

        private static void OrbContests()
        {
            var timing = new DuelTuning { orbFirstDelay = 0.1f, orbLifetime = 0.25f, orbInterval = 0.2f };
            var game = new DuelMatch(timing);
            game.Tick(0.12f);
            Require(game.Snapshot().orbActive && game.Snapshot().orb == Vector2.zero, "First orb missing.");
            game.Tick(0.26f);
            Require(!game.Snapshot().orbActive && game.Snapshot().orbDenials == 0, "Expired orb counted as a deny.");
            game.Tick(0.22f);
            Require(game.Snapshot().orbActive && game.Snapshot().orb != Vector2.zero, "Next site failed to spawn.");

            var tuning = new DuelTuning { orbFirstDelay = 6f };
            game = AtPositions(tuning, new Vector2(-2f, 0f), new Vector2(1.6f, 0f));
            game.Command(DuelHero.Ogre, DuelOrder.Slam, Vector2.right);
            game.Tick(1.6f);
            game.Command(DuelHero.Luis, DuelOrder.Move, Vector2.zero);
            game.Tick(2f);
            var picked = game.Snapshot();
            Require(picked.orbPickups == 1 && picked.orbDenials == 0 && !picked.orbActive, "Luís failed to claim the orb.");
            Require(picked.notice.Contains("+"), "Orb heal amount missing from notice.");
            Close(picked.luis.hp, tuning.luisMaxHp - tuning.slamDamage + tuning.orbHeal, "Orb healing incorrect.");
            Close(picked.ogre.hp, tuning.ogreMaxHp - tuning.orbDamage, "Orb pulse missed nearby opponent.");

            game = AtPositions(tuning, Vector2.zero, DuelArena.LuisSpawn);
            game.Tick(2.3f);
            var denied = game.Snapshot();
            Require(denied.orbDenials == 1 && denied.orbPickups == 0 && !denied.orbActive, "Ogre failed to deny the orb.");
            Close(denied.luis.hp, tuning.luisMaxHp, "Denial damaged an opponent.");
        }

        private static void Knockout()
        {
            var tuning = CombatTuning();
            tuning.luisMaxHp = tuning.slamDamage;
            var game = AtPositions(tuning, new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f));
            game.Command(DuelHero.Ogre, DuelOrder.Slam, new Vector2(3f, 0f));
            game.Tick(2f);
            var end = game.Snapshot();
            Require(end.finished && end.winner == (int)DuelHero.Ogre && end.luis.hp == 0f, "Lethal hit failed to end round.");
            game.Command(DuelHero.Luis, DuelOrder.Move, Vector2.zero);
            game.Tick(1f);
            Require(game.Snapshot().luis.position == end.luis.position, "A knockout round kept accepting orders.");
            var fresh = new DuelMatch(tuning).Snapshot();
            Require(!fresh.finished && fresh.winner == -1 && fresh.ogre.position == DuelArena.OgreSpawn &&
                fresh.luis.position == DuelArena.LuisSpawn, "New round did not restore spawn state.");
        }

        private static DuelMatch AtPositions(DuelTuning tuning, Vector2 ogre, Vector2 luis, float travelSeconds = 4f)
        {
            var game = new DuelMatch(tuning);
            game.Command(DuelHero.Ogre, DuelOrder.Move, ogre);
            game.Command(DuelHero.Luis, DuelOrder.Move, luis);
            game.Tick(travelSeconds);
            Require(Vector2.Distance(game.Snapshot().ogre.position, ogre) < 0.02f &&
                Vector2.Distance(game.Snapshot().luis.position, luis) < 0.02f, "Positions not reached by normal move commands.");
            return game;
        }

        private static void Close(float actual, float expected, string message) =>
            Require(Mathf.Abs(actual - expected) < 0.015f, message);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
