# Conversation Handoff — What Happened Before Codex

This file is a **structured handoff**, not a verbatim export of the entire ChatGPT transcript. It preserves significant user decisions and topics discussed as of 2026-11-17. The hero descriptions in `02_HERO_DESIGNS.md` are the authoritative detail.

## Design exploration

The user provided five original heroes in order:

1. **Luís**: Health regeneration aura manipulating allies/enemies; permanent healing-power gains from takedowns; periodic denyable orbs heal allies/hurt enemies; invisibility with illusion; enemy spellcasting amplifies a retaliatory burn curse; channeled R slowly builds slow/vulnerability/healing amplification.
2. **All-Passive Hero**: Q/W/E/R are all passive, triggered by attacking, taking damage over time, standing still and hero kills respectively. Overlapping every passive grants stronger defenses/lifesteal and enhanced passives.
3. **Chibi Necromancer**: Harvest souls; spend current stock entirely to revive a dead ally as a more or less powerful chibi; retain lifetime soul count to scale zigzag ghost and skeletons; E darkens vision while eyes remain visible; R empowers all red-eyed chibis and adds ghost fear.
4. **Four-Form Botlaner**: Preselect order of ADC/poke/engage/enchanter forms; every ability casts then switches to next form; cooldown slots shared. Different mechanics in each form, including ADC shot-stack multicast, poke mana refunds and wall-bouncing projectile, engage double-hook collision, enchanter next-ability geometry buffs and ally repositioning.
5. **Slow Tortuga**: Slow shell form, eat trees and sleep, shell diverts 50% of damage and has separate HP/resistances, hide in shell, lick ally to nominate Pet Owner, size-changing R. Shell death transforms him to speedy, red, bleeding/acid/charge fighter until hero death restores shell.

The assistant rated favorite **designs** (subjective): Chibi Necromancer, Tortuga, Luís, Four-Form, All-Passive. This is preference, not priority or implementation order.

## Positions in LoL-style lanes (only suggestions)

- Luís: support primarily; mid and top situational.
- All-Passive: top primarily; jungle potentially.
- Chibi: support or mid, depending on minion soul income.
- Four-Form: bot carry or support.
- Tortuga: top; support or jungle plausible.

## Macro format discussion

The user considered expanding from LoL-like 5v5 to **6v6**, where a sixth hero would either share top as a secondary support or become a second jungler. The map and jungle would be significantly larger. Assistant suggested a flexible role approach, but this is **not a final user choice**.

## Blend of MOBA inspirations

From old Dota 2: slower, more cumbersome/weighted movement; substantial terrain and tree interaction. From LoL: fewer point-and-click abilities, shorter CC, mostly passive/stat items, dedicated jungle/support items, more financially empowered supports, broad ability scaling. From both: **Strength, Agility and Intelligence** are scaling stats with **no automatic inherent bonuses**. Bushes are undecided.

## Production discussion

The user originally considered a Dota custom game but later leaned toward a standalone game to control core movement, stat, vision, ability and economy mechanics. The assistant recommended **standalone as destination**, while Dota Workshop could still be a disposable experiment tool.

The assistant later recommended Unreal Engine 5 with C++/Blueprint hybrid and Gameplay Ability System, contingent on machine suitability and local verification. First milestone: minimal top-down movement sandbox (one capsule, a camera, pathing around trees, turn speed and attack feel eventually) before building full heroes/economy/multiplayer.

## Human + coding-agent collaboration

The user wants an AI technical co-developer to take on as much setup, code, architecture, debugging, tests, project organization and balance tooling as possible. The user will do editor/device interaction, local execution, playtesting and subjective tuning, plus other actions beyond tool access. Prefer small, highly actionable steps and clear diffs. Respect the user's ownership of game design.

## Current transfer status

A direct transfer of this ChatGPT chat into Codex's conversation history was not available via the tools used. This project folder was created as a **portable local handoff**. It is not yet opened in Codex, and no Unreal project has been generated or installed.
