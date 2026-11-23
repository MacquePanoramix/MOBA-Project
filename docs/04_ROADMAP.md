# Roadmap — Small, Playable Milestones

## Milestone 0 — Validate the toolchain and project skeleton

- [x] Confirm Unity `6000.6.0f1` for the movement lab trial. The full-game engine decision remains open.
- [x] Inspect the installed tools and verify the build on Windows.
- [x] Create the Unity project inside `prototype/`.
- [x] Initialize Git, ignore generated files, and verify the Windows player launch.
- [x] Document the editor and Windows player launch steps in the [lab guide](../prototype/README.md).

**Done when:** local project opens reliably and source is versioned.

## Milestone 1 — Movement Lab v0.01

- [x] Fixed top-down camera.
- [x] Right-click movement with pathfinding around static tree-like obstacles.
- [x] Editable movement speed, turning rate, and stop behavior.
- [x] Visible path, destination, facing, and status feedback.
- [x] Repeatable test course with a restorable tuning baseline.
- [x] Designer playtest and movement feel feedback. Both tested turn rates fit the intended range.

**Done when:** human can run a tiny sandbox and report whether movement feels weighted enough.

## Milestone 2 — Basic combat laboratory

- [x] First slice with one stationary dummy, visible attack phases, and a shared basic attack for both movement profiles.
- [ ] Two targetable graybox heroes, teams, HP and respawn.
- [ ] Basic attacks, attack wind-up and backswing.
- [ ] One projectile skillshot, simple collision, short stun/root, damage and healing.
- [ ] Test lightweight attribute scaling formulas and unit tests.

**Done when:** tiny 1v1 graybox combat supports repeated tuning.

## Milestone 3 — Expand the first duel

- [ ] Extend a selected duelist with a signature interaction that the opponent can read and respond to.
- [ ] Play from both sides before expanding either kit. Use the [first playable match proposal](#first-playable-match-proposal) for candidate interactions.
- [ ] Keep ability values configurable and test transition edge cases.

**Done when:** the first pair shows how its two heroes make different decisions in the same fight.

## Milestone 4 — One lane and game economy

- [ ] Basic creeps, towers, gold/XP, item/stat pipeline.
- [ ] Basic support item and jungle camp proof of concept.
- [ ] Hero role and scaling validation.

## Milestone 5 — Map, vision, multiplayer proof of concept

- [ ] Fog/trees/bushes prototype, map architecture, objective/resource economy.
- [ ] Minimal authoritative networked interactions, latency tests.
- [ ] Profile creep/summon counts, performance.

## Milestone 6 — 5v5 or 6v6 alpha

- [ ] Choose team size based on tested economy/travel/role constraints.
- [ ] Expand map/jungle, test sixth-player strategies if 6v6 chosen.
- [ ] Implement remaining roster iteratively and test for meaningful counterplay.

## First playable match proposal

**Designer direction.** Start with a 1v1 setup. Decide which heroes make a good duel before starting character models. The existing movement and attack lab remains the playable baseline.

**Recommended first pairing for review — Ogre and Hero Luís.** A periodic Luís orb gives the heroes a shared destination for opposite reasons. If Ogre spends his long Q wind-up covering it, Luís can feint toward it, sidestep the impact and come back. If Luís commits early, Ogre can sweep him away with W, then walk over the orb to deny it. Ogre can also simply take the orb if Luís yields the ground. If Luís reaches it first, his pickup heals him and pulses damage against nearby enemies. The proposal tests whether a player watching the other hero can find an answer. Orb placement, attack reach, damage, timings and round rules would be editable trial settings.

### What each hero can show in a duel

| Hero | What two opponents can test | Limit or reason to schedule it later |
| --- | --- | --- |
| Ogre | An opponent can read his wind-up, dodge the slam or bait it before closing. Lacking a basic attack makes a miss count. | Club motion and hit area must agree at game-camera distance. Luís's orb kit alone cannot exercise Ogre's CC immunity. An Ogre mirror can. |
| Hero Luís | The same orb creates a pickup for one player and a denial for the other. Later, his curse can give an enemy a reason to hold a spell. | Safe orb spawns could reward waiting. Test contested positions first. The strength of the healing aura and curse could obscure the initial spacing test. |
| Old Man Swordsman | Each basic has two contacts with different rewards. His Q can hit Ogre during a cast, testing whether the damage lands while the slow is resisted. | A shove between strikes exposes an open rule about the second hit. A single opponent can test R's dash and execute. Using the refresh against a different target needs another enemy. |
| Slow Tortuga | Against Ogre, tucking through a telegraphed hit and chasing after a shell break give the fight two distinct phases. | His slow form could be caught repeatedly and the matchup needs both forms before we can judge it. Pet Owner needs an ally. |
| Airplane Fighter | Its unsteerable straight flight during a stun gives the opponent a chance to predict its path. The pilot must also steer with the cursor used for aiming. | Flight controls, base parking and air-versus-ground targeting introduce three new variables together. A dedicated flight trial followed by a duel would clarify which causes a one-sided result. |
| All-Passive Hero | Attacking, standing still and taking enemy damage are three competing ways to prepare buffs in a duel. | The fourth passive needs a hero kill. If a round ends at first kill, the player cannot use the four-buff overlap against a live opponent. Repeated engagements suit this kit. |
| Chibi Necromancer | A slow zigzag ghost and skeletons give the opponent projectiles to dodge and bodies to navigate. | A solo opponent offers little pre-kill soul income in an empty lane. An allied corpse for the revival never exists in a strict 1v1. |
| Four-Form Botlaner | Cast order and shared slot cooldowns reward planning. | Ally-targeted enchanter spells and the engage form's two-target hook lack intended targets here. Allies and a wave would make the rotation much more representative. |
| Bomb Planter | Long-fuse bomb planting, mutual Q push-off, and risky E taunt channels create distinct cat-and-mouse spacing. | Minion explosion passive, W wall-cross-and-return, and R's skill-check log minigame need lane walls, creeps, and bespoke UI—best scheduled once Milestone 4 adds waves and walls. |
| Automatron Bot 3000 | Tests indirect AI posture toggles (Brave/Coward, Farm) and macro camera/unit cycling. | Cannot validate direct hero movement/attack feel; relies on minions, individual gold/XP, and shop infrastructure (Milestone 4+). |
| Chain Laser Shooter | Strong ranged kiting and skillshot duel test: extending Q beam, W stack slows, and E decoy blocks/stuns. | Unlimited chain bounces and Q multi-kill bonus gold stay mostly dormant without a minion wave; strong candidate for the first ranged test once waves exist (or a 3rd 1v1). |
| Multiple Phases Raid Boss Hero | Tests boss-bar phase threshold pacing (20% cleanses) and an isolating R arena. | Q/W/E multi-part combo patterns are still undecided, and Phase 1 begins with zero active abilities; better after core combat combos and threshold feel settle. |

### Proposed builds and playtest

1. **Duel foundation.** Use a short, symmetric graybox lane, two opposing units with health, two spawns, a visible round result and a quick reset with the roles swapped. Start from identical positions relative to the center so each side can run the same opening. A scripted sparring opponent can exercise the controls solo. Two human players are needed to judge whether the bait or denial is genuinely persuasive. The player connection and control setup is still open.
2. **First asymmetric exchange.** Give Ogre his inability to basic attack, CC immunity during ability animations, aimed Q stun and shorter W push. Give Luís a provisional basic attack and his periodic Q orb with pickup, denial, expiry and the authored pulse. Use a reproducible, reachable orb spawn sequence. Show Ogre's reach and impact with an original rough club animation. Let a tester play each side. Hero models, waves, towers and the shop can follow once the exchange works.
3. **Read the results.** From both starts, try a denied orb, a pickup in pulse range and a baited Q that misses and leaves an opening. Record when the player recognized the wind-up, whether turning left enough time to dodge, and whether the shove allowed a contested denial. If waiting on the spawn always wins or taking one stun guarantees the next, change one timing or spawn variable and repeat. An Ogre mirror can verify immunity with a Q or W landing during the other Ogre's cast and again between abilities.
4. **Extend one interaction at a time.** Try Luís's retaliatory E against Ogre before adding Ogre's queued R. Ogre must cast to fight, so check whether he still has a meaningful response once cursed. For the combined ult test, decide whether entering a Q/W during the channel or executing the stored strike counts as a cast for the curse. The full mechanics for both heroes stay in the [hero designs](02_HERO_DESIGNS.md).

The [Tortuga and lane art research](08_TORTUGA_AND_FIRST_LANE.md) remains available for a later Tortuga matchup.

### Second matchup recommendation

Keep Ogre in the same lane and swap Luís for the [Old Man Swordsman](02_HERO_DESIGNS.md#8-old-man-swordsman). This isolates a different question: can a slower melee hero bait a club swing, then get close enough to land both strikes? Compare Ogre pushing before contact, between the strikes, and after both land. Check the first hit's heal and the second hit's two resistance changes separately. The result of displacement between hits is for Luís to decide. Since the strikes come in quick succession, Ogre may have to anticipate the commitment. Preserve that cadence during the test.

This pairing also lets both players see what Ogre's immunity does: the swordsman's Q deals damage and attempts a slow, and the slow should be resisted if it lands during Ogre's immune animation. A later round can test whether the swordsman's R gives him a readable way to catch the last opponent he reached. If testing the choice between denying an orb and staying in reach for another pair of strikes becomes the priority, use Luís versus the swordsman on the same lane.
