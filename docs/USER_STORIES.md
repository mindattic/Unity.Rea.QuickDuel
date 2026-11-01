---
codex: 1
project: Unity.Rea.QuickDuel
code: UQD
layer: stories
status: living
updated: 2026-10-21
---
<!-- GENERATED from MindAttic.Tickets/data/Tickets.db by `tickets db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the tickets CLI. -->

# Unity.Rea.QuickDuel — User Stories

## Priority backlog
_Nothing recorded yet._

## Epic P — Parity with the original Bushido Coda duel
- **UQD-US-P1 ✅** As a player, I see the original Bushido Coda's own art in the duel, so that the game looks like the game it rebuilds.
- **UQD-US-P2 ✅** As a player, each duel is set on one of the original's arenas, so that no two duels feel the same.
- **UQD-US-P3 ✅** As a player, I see two samurai on the field, so that the standoff has someone in it.
- **UQD-US-P4 ✅** As a player, I see the duelists charge at the cue, so that the strike feels like a motion and not a flash.
- **UQD-US-P5 ✅** As a player, I read the original's own type, so that the text has its character.
- **UQD-US-P6 ✅** As a player, I see Get Ready and a fading 3-2-1, so that the countdown reads like the original's.
- **UQD-US-P7 ✅** As a player, I watch the reaction counter run during the strike, so that I feel the time pass.
- **UQD-US-P8 ✅** As a player, I see who won and by how much, so that a round has a clear verdict.
- **UQD-US-P9 ✅** As a player, a strike ends in blood and a flash, so that the cut has weight.
- **UQD-US-P10 ✅** As a player, the loser drops their weapon, so that the duel has a physical end.
- **UQD-US-P11 ✅** As a player, rounds fade through the original's loading art, so that the next round arrives like in the original.
- **UQD-US-P12 ✅** As a player, the corners of the screen carry the original's ornament, so that the frame looks finished.
- **UQD-US-P13 ✅** As a player, I start in the original's intro, so that the game opens on its story.
- **UQD-US-P14 ✅** As a player, I choose how to play from the original's main menu, so that I do not need to know about Tab.
- **UQD-US-P15 ✅** As a player, I see the original's katana cursor over the menus and the duel, so that the pointer matches the game.
- **UQD-US-P16 ✅** As a player, I can read who made the art, so that credits are given.
- **UQD-US-P17 ✅** As a player, I see a victory or defeat screen when the match ends, so that the end of a match has a place.
- **UQD-US-P18 ✅** As a player, the arena is alive, so that the standoff is not static.
- **UQD-US-P19 ✅** As a player, I can toggle fullscreen and see a match clock, so that the window and HUD behave like the original's.
- **UQD-US-P20 ✅** As the owner, I want a final scripted play-through compared against the original, so that I know the rebuild reached parity.


## Epic S — Unity best practice and shared standards
- **UQD-US-S1 ✅** As the maintainer, I want the duel's tuning (countdown, standoff range, strike timeout, settle window, AI reaction ranges, wins needed, taunt lines, human floor) in a validated settings registry and profile on the shared Unity.Shared package, so that no tuning number is hard-coded in behaviour.
- **UQD-US-S2 ✅** As the maintainer, I want the game on URP behind a pipeline-agnostic layer, so that it follows the Unity 6 standard the other repos use.
- **UQD-US-S3 ✅** As the maintainer, I want every duel rule, including the match flow and mode selection that live in DuelSession today, in the engine-free Core behind commands that carry a player id and produce events, so that presentation only reads state and plays events and local and online play share one path.
- **UQD-US-S4 ✅** As a player, I want crisp, resolution-independent text and UI, so that the game looks right at any window size.
- **UQD-US-S5 ✅** As the maintainer, I want the scene, art import, data and generators all built and checked from code, so that nothing depends on hand-wiring in the Editor.
- **UQD-US-S6 ✅** As the maintainer, I want CI to compile and test the Unity project and build the player, so that a break in the Runtime, Online or Editor assemblies cannot land unseen.
- **UQD-US-S7 ✅** As a new contributor, I want the README and BIBLE to describe what the repo is now, so that they are the one true description.

