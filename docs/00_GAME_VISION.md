# Game Vision — Working Draft

## Elevator pitch

An original, deliberately paced MOBA that combines the physical, terrain-conscious, occasionally janky feel of early Dota 2 with a skillshot-oriented, lower-hard-CC combat language and largely passive/stat-focused items. The designer is considering a strategically flexible sixth player on a larger map.

## Intended feel

- **Slow does not just mean low movement speed or long match duration.** Encounters should unfold visibly and make commitment, turning, traversal, and positioning matter.
- Heroes should feel physically situated in the world: turn rates, attack wind-ups, collision, terrain/pathing, destructible trees, and vision are design priorities.
- Old Dota 2's launch-era heavier/jankier movement is a reference point, not a request to copy bugs or produce unresponsive controls.
- Offensive abilities generally favor aimed/projectile/area/positional interactions over point-and-click certainty.
- Hard crowd-control durations should generally be shorter than in older Dota-style designs. Exact numbers are not approved.
- Items should more often offer stats and passives than active buttons, with selective actives permitted.
- Supports should be economically meaningful, scale, and have compelling gameplay rather than being perpetually underfunded.
- Jungle and support item ecosystems should support their gameplay/economic roles.
- Most hero abilities should retain meaningful scaling, possibly via different stats or conditional resources.

## Roster design philosophy

The twelve hero concepts explore different ways to move, attack, farm, and warp an encounter.

1. **Luís:** sustain, orb denial, illusion mind games, anti-casting curse, escalating zone/channel teamfight.
2. **All-Passive Hero:** Q/W/E/R all passive; satisfy behavioral triggers and overlap buffs to enter a strong state.
3. **Chibi Necromancer:** collect souls, revive allies as chibis, grow summons, dark-vision/red-eyes thematic teamfights.
4. **Four-Form Botlaner:** preset ordered rotation across ADC, poke mage support, engage support, enchanter support; each cast advances form; shared slot cooldowns.
5. **Slow Tortuga:** defensive shell with damage diversion; shell breaking transforms into fast aggressive red bleed/acid/charge form.
6. **Ogre:** fights entirely through club abilities, gaining crowd-control immunity during their animations. During the ultimate's channel, the player queues and aims a rapid sequence of slams and sweeps with a delayed critical finisher.
7. **Airplane Fighter:** takes off from base, flies continuously toward the cursor over terrain and parks on returning. Stuns and roots lock its heading. It aims strikes with the cursor and leaves bombs along its flight path.
8. **Old Man Swordsman:** moves slowly and attacks twice in quick succession. His first strike sustains him, and his second builds his resistances while wearing down the opponent's. He can dash past the last foe he basic-attacked within a short window. Executing that foe refreshes his ultimate.
9. **Bomb Planter:** no basic-attack hero damage—instead plants long-timer bombs on hits while exploding last-hit creeps; mutual push-off Q, temporary wall-hop-and-return W, wide reveal taunt E that charges planted bombs, and skill-check log-barricade run R.
10. **Automatron Bot 3000:** outside-observer engineer who unlocks up to four AI Automatrons across tiers with individual gold/levels and toggled camera, brave/coward, farming, and tier-cycling directives.
11. **Chain Laser Shooter:** unlimited non-repeating chain-laser basics that stack laser vulnerability, expanding gold-harvest Q beam, stack-detonating slow shocks W, projectile/hero-blocking stun decoy E, and moving multi-proc beam R.
12. **Multiple Phases Raid Boss Hero:** unhealable, zero-regen five-phase boss who cleanses and turns invulnerable at every 20% HP loss while unlocking Q/W/E/R multi-part combos and growing more demonic, culminating in an isolating true-damage arena R.

## Failure shapes to watch

- **Sluggish instead of deliberate:** slow turning or long wind-ups become unresponsive input lag instead of readable commitment and counterplay.
- **Spectacle noise:** unusual kits (chain beams, four forms, dark vision, skill-check logs, 12-hero fights) lose clarity if telegraphs and hit areas disagree at game-camera distance.
- **Provisional numbers hardening into law:** placeholder prototype timings, HP pools, or test workarounds get treated as final balance rather than dials for Luís's playtests.
- **Green tests mistaken for fun feel:** automated checks prove simulation invariants, never whether spacing, turning, or ability cadence feels good in play.

## Open directions

- Start with 5v5 or 6v6? A possible 6v6 arrangement is five conventional roles (top, jungle, mid, bot carry, bot support) plus a flex player who can be a secondary top support/roamer or second jungler.
- 6v6 would require a meaningfully larger map/jungle and enough economy to sustain six players.
- Bushes may coexist with trees, but their distinct vision function remains undecided.
- Visual style, camera implementation details, match length, precise scope, monetization, branding and launch targets are undecided.
