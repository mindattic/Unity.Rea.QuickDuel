---
codex: 1
project: Unity.Rea.QuickDuel
code: UQD
layer: bible
status: living
updated: 2026-10-26
---

# Unity.Rea.QuickDuel — Project Bible
> Single source of truth for what Unity.Rea.QuickDuel IS, is NOT, and the rules that keep it coherent.
> README says how to build and play it; [ORIGINS](ORIGINS.md) says where it comes from; this says how to
> think about the game.

Status legend: ✅ done (verified by a test/build) · 🟡 partial · ⬜ planned · `living`.

## 1. The one sentence {#UQD-§1}
Unity.Rea.QuickDuel is a Unity 6 reaction duel: two duelists wait through a standoff of unknown length, the
screen flashes and a blade is drawn, and whoever strikes first wins — strike before the cue and you lose.

## 2. The product promise {#UQD-§2}
- **One moment, nothing else.** The whole game is the wait and the strike. It is the core of Ryan's old
  *Bushido Coda* (Android 2011–13, GameMaker 2013), itself grown out of Samurai Kirby; see
  [ORIGINS](ORIGINS.md).
- **Fair to the millisecond.** A strike is timed by its input event's own timestamp, not by the frame that
  noticed it, so frame rate can never decide a duel ([UQD-LAW-2](#UQD-LAW-2)).
- **Unreadable cue.** The standoff is drawn uniformly from 3 to 6.5 seconds after the countdown, so
  the only way to win is to react, not to count.
- **Patience is a skill.** Striking during the standoff loses the round outright, and the loser is taunted
  with a line from the old Android build ("Jumped the Gun", "Too Soon", ...).
- **Couch first.** Two players on one machine (Left Ctrl / Right Ctrl, as the original's Versus had them, or one
  gamepad each), or one player against
  an AI on Easy, Normal or Hard. A match is best of 3.

## 3. What it is NOT {#UQD-§3}
- **NOT Bushido Coda.** No overworld, merchants, weather, items, stats (ART, LCK, PWR), focus bar, skill
  dials, combos, clash button-mash, or arenas and characters as gameplay. Those were dropped on purpose;
  adding any back needs a Bible change first.
- **NOT a character game.** The original's five fighters (Myrmidon, Ronin, Gypsy, Vagabond, Exile) are
  **cosmetic only** ([§4.5](#UQD-§4.5)): a name and the idle, run, attack, defeat and weapon art. They carry
  no stats (PWR, VIT, SPD, LCK), no skills, no HP and no portrait or description, and which fighter stands in
  a seat never changes how a round is timed, judged or scored. There is no character select: the authority
  draws both seats' fighters for each match.
- **NOT frame-timed.** The 2013 build ran its action phase at 1000 FPS to time reactions by frame.
  QuickDuel never does; see [UQD-LAW-2](#UQD-LAW-2).
- **NOT a game where the AI cheats.** The AI draws a reaction time from its difficulty's range when the
  countdown starts and strikes at exactly `CueAt + reaction`; it never sees the player's input and never
  strikes early.
- **NOT a menu game.** The game opens on the original's intro, goes to a main menu that holds only New Game
  (vs computer), Versus (two players), Quit, Credits and the computer's difficulty, and then to the duel. There
  are no options, scores, character select or map screens; the original's `Options` and `Scores` buttons were
  empty stubs and are dropped ([§4.6](#UQD-§4.6)).
- **NOT a game of many arenas as gameplay.** The 11 arenas are a backdrop, one drawn at random per match, with an
  ambient effect on a few; an arena never changes a round's timing, judging or scoring ([§4.7](#UQD-§4.7)).
- **Online is built, not the front door.** Online PvP runs in the Duel scene through a Host / Join menu
  (§4.4, `OnlineDuelMenu`); it never starts by itself, and the main menu offers local play only.

## 4. Architecture canon {#UQD-§4}

```
Assets/Unity.Rea.QuickDuel/
  Core/      engine-free rules (noEngineReferences)   Duel, DuelSettings, DuelResult, DuelJudge, Match, AiOpponent,
                                                      MatchSession, OnlineDuelHost, OnlineDuelClient
  Core/Settings/ the tuning registry                  TuningRegistry (named profiles), Json, SettingsReader, ISettingsSource
  Core/Content/  the original asset manifest          AssetManifest (sprites, sounds, fonts, typefaces, arenas, fighters, credits),
                                                      and the engine-free numbers of each visual: RoundClock, RoundFade,
                                                      CountdownBanner, ReactionCounter, BloodSpray, SplatterField, WhiteFlash,
                                                      WeaponToss, CornerArt, AmbientField, IntroScene, MainMenu, MenuFade,
                                                      LoadingFade, CreditsRoll, VerdictScreen, VerdictBanner, DebugReadout,
                                                      OriginalType, FighterMotion,
                                                      ArenaDraw (each match's arena roll and the arena it lands on),
                                                      FighterDraw (each match's fighter roll and the pair it lands on),
                                                      FighterLineup (where the fighters stand, their fade and tint)
  StreamingAssets/Settings/tuning.json                every tuning number, as data
  Art/, Audio/, Fonts/  the original's files           copied by OriginalAssetGenerator, never edited by hand
  Data/assets.json, Data/credits.txt                  the generated asset manifest and the original credits text
  Runtime/   MonoBehaviours, one set per scene        Duel scene: DuelController (through IDuelAuthority: LocalDuelAuthority
                                                      or OnlineDuelAuthority), DuelStartup, DuelInput, DuelSession, DuelView, DuelTaunt,
                                                      DuelAudio, DuelClock, DuelFade, DuelBlood, DuelWeapons, DuelAmbient,
                                                      DuelCornerArt, DuelVerdict, DuelDebugOverlay, ArenaBackdrop, FighterLook;
                                                      Intro and MainMenu scenes: IntroController, MainMenuController;
                                                      every scene: GameWindow (F1), OriginalFrame (640 x 480 letterbox),
                                                      KatanaCursor, SessionAudio (the CLI mute), TuningStore
  Online/    the wire, over Unity.Multiplayer         OnlineDuel (a NetEntity; compiles only with the package present),
                                                      OnlineDuelLobby, OnlineDuelMenu, OnlineDuel.prefab, QuickDuelNetPrefabs
  Editor/    generators                               DuelSceneBuilder, IntroSceneBuilder, MainMenuSceneBuilder (write
                                                      Assets/Scenes/*.unity), OnlineSceneBuilder, RenderPipelineBuilder,
                                                      DuelPanelBuilder, PlayerIconBuilder, QuickDuelBuild (BuildScenes, the
                                                      Windows build), the art pipeline: OriginalAssetGenerator +
                                                      OriginalAssetCatalog + OriginalAssetImportRules + OriginalFontBuilder
  Rendering/ URP assets                               QuickDuelURP + its renderer, behind RenderPipelineBuilder
  Tests/Editor/                                       EditMode: scene builders, art generators, settings registry validation
  Tests/PlayMode/                                     scene, input, session, view, taunt, audio and per-feature tests, and
                                                      ParitySceneTests (each scene drawn through its panel, a real picture)
  Tests/Online/                                       host + client over the Unity.Multiplayer test kit
Tests/Core.Tests                                      NUnit tests of Core, run with `dotnet test Core.slnx`
```

### 4.1 A round {#UQD-§4.1}
`Duel` is one round, a phase machine driven by caller-supplied timestamps in seconds:

| Phase | Lasts | On screen and in the ear |
| --- | --- | --- |
| `Ready` (the startup) | `StartupSeconds` (3) from the scene's start, or from the round transition's image being gone; a click (or a pad's South) skips it | nothing new: the fighters fade in idle under the fading loading image (`DuelStartup`, `RoundStartup`; online, until the host's Space / Start) |
| `Countdown` | `CountdownSeconds` (4) | Get Ready above the number throughout, nothing numbered for the first quarter, then 3-2-1 at 25%, 50% and 75%, each at full alpha with a beep and fading 0.02 a 60 fps step |
| `Standoff` | `MinStandoffSeconds`–`MaxStandoffSeconds` (3–6.5), random | letterbox closes in, drumroll |
| `Strike` | until the first strike settles; a local round waits as long as it takes, as `gameclock_action` did (UQD-T68), an online one gives up after `StrikeTimeoutSeconds` (3) | the arena backdrop's colours invert, the cue, the blade unsheathed, a millisecond count from the cue in the centre that the first strike freezes and then fades |
| `Resolved` | until the next round | slash, the loser falls; blood spurting from the centre, 6 to 12 splatters and ten white flashes after any strike; the loser's weapon thrown spinning to land with a hit-ground sound; the winner's name and Wins! on the loser's side with the winner's number sliding away from the loser, or a centred premature-strike taunt; a headline only for a tie, nobody striking or a forfeit |

The cue time `CueAt` is fixed when the round begins. After the first strike the round waits
`SettleSeconds` (50 ms) for a later-delivered input event that happened earlier, then resolves.

### 4.2 Judging {#UQD-§4.2}
`DuelJudge.Judge(left, right)` settles a round from reaction times alone (seconds from the cue; negative
means before it; null means no strike). In order:

1. **Forfeit** — a reported reaction that is not finite, or under the human floor (100 ms), forfeits.
   Local play passes a floor of 0, since its strikes are real input events; the floor exists for online
   reports.
2. **EarlyStrike** — a negative reaction loses; if both struck early, the earlier one loses.
3. **NoStrike** — online only: nobody struck before the timeout. Nobody scores. A local round has no timeout (UQD-T68).
4. **Tie** — identical reactions. Nobody scores.
5. **FasterStrike** — the lower reaction wins; a lone striker wins.

### 4.3 Session and match {#UQD-§4.3}
`MatchSession` (Core, UQD-T30) is the one authority over a local duel's round, mode and match: it owns the
mode (Local PvP → vs AI Easy → Normal → Hard), the difficulty mapping, the `Match`, the current `Duel` and
the AI's wiring (scheduling and striking on its own behalf). Nothing changes any of that except its three
commands -- `BeginRound`, `Strike`, `SetMode` -- each named by the seat issuing it (`playerId`: 0 the left
seat, 1 the right, the seam a future `NetEntity` will plug a real player id through, AGENTS.md rule 9); every
change it makes is announced only as an event (`RoundStarted`, `PhaseChanged`, `Resolved`, `ModeChanged`).
`DuelController` threads Unity's clock and tuning through to those commands and republishes its events, and it does
so through one seam whichever authority holds the duel (`IDuelAuthority`, UQD-T70): `LocalDuelAuthority` wraps
`MatchSession`, `OnlineDuelAuthority` wraps the online duel, and the controller reads and commands either the same way,
so local and online play take one path through the scene (rebuild standard rule 9);
`DuelSession` is a thin view that draws the score and verdict labels (and online the host / joined line) from the
session's state. The mode comes from the main menu (New Game or Versus, the original's `is_singleplayer`); the duel
has no key for it and draws no mode line (UQD-T67). `SetMode` is refused mid-round, and changing the mode always
starts a fresh match. A match is first to 2 wins (best of 3); ties and no-strike rounds award nobody. The
scoreboard shows wins and each side's best reaction. A finished local match ends on the original's victory or game
over screen (UQD-T48, [§6](#UQD-§6)), whose Retry starts a new match in the same mode and Continue returns to the menu.

AI reaction ranges (`AiOpponent.ReactionRange`): Easy 450–700 ms, Normal 300–450 ms, Hard 200–300 ms.

### 4.4 Online PvP design {#UQD-§4.4}
Credited to Christopher Danielson. The host only listens: it sends each round's standoff length, each
client runs its own countdown and cue and measures its own reaction from its own cue to its own key press,
and sends back just that number. The host judges the two numbers with `DuelJudge` (human floor on). Ping
never enters the measurement, so neither player is favoured by latency.

Built as (UQD-T14):

- **`OnlineDuelHost`** (Core) draws each round's standoff (and hands out its countdown), accepts the first
  report per side for the open round only, and judges once both are in, or when the report window closes
  (`CountdownSeconds + standoff + StrikeTimeoutSeconds + SettleSeconds + ReportGraceSeconds` (2 s) after the
  round began) with a silent side counted as no strike. A finished match is replaced on the next round.
- **`OnlineDuelClient`** (Core) runs each announced round as an ordinary local `Duel` on its own clock with the
  host's lengths (`Duel.Begin(now, standoffSeconds)`), and reports its own side's reaction once that duel
  resolves: at once for an early strike, after the settle window for a strike, after the timeout for none.
  A round no newer than the current one is ignored.
- **`OnlineDuel`** (Online, a `NetEntity`) is the wire: the server `Seat`s two client ids (synced),
  `BeginRound` sends `RoundBegan(round, countdown, standoff)` to every peer, the seated duelists send
  `ReportReaction(round, struck, reaction)`, reports from any other connection are dropped, and
  `RoundJudged` carries the verdict to every peer, which records it in its own `Match`. A local strike is
  `OnlineDuel.Strike(eventTime)`.

In the Duel scene (UQD-T21):

- **Host / Join menu** (`OnlineDuelMenu`, built in code on `DuelView`'s canvas, left edge under the left score,
  shown between rounds only). **Host** starts a listen server on the scene's `NetBootstrap`; this player duels on
  the left. **Join** dials the typed address (`192.168.1.20` or `192.168.1.20:7777`, parsed by
  Unity.Multiplayer's `HostAddress`); that player duels on the right. **Leave** ends either. Two players at most,
  joined by address; no join-code service is set up. Typing in the address field and clicking the menu never
  strikes, begins a round or cycles the mode (`UiFocus`).
- **`OnlineDuelLobby`** (Online) is the session flow, apart from the menu: on the host it spawns the one
  `OnlineDuel` (network key `quickduel.duel`) once the session is up, seats the host on the left and the first
  player to join on the right, and empties that seat when the player leaves (`OnlineDuel.Unseat`); on every peer
  it hands the `OnlineDuel` to `DuelController.Online` once it arrives and takes it back when the session ends.
- **`DuelController.Online`** (an `IOnlineDuelSource`, Runtime's seam that `OnlineDuel` implements, since Runtime
  cannot name Online) drives the scene instead of the local `MatchSession`, which stands idle and resumes unchanged
  afterwards. The phase is this machine's own round's, but `Resolved` (and `Result`, the slash, the fall, the
  headline, the score) waits for the host's verdict: the local round saw only this side's strike, so its own
  result is never shown. Between this side's report and the verdict the headline reads `...`. Only the host's
  Space / Start begins a round, and only with both seats taken; every strike key strikes for this machine's seat;
  the mode does not apply online. The scoreboard marks the player's own seat `(you)` and the victory / defeat
  sting is heard from it.
- **Scene generation**: `OnlineSceneBuilder` (run by `DuelSceneBuilder`) makes `OnlineDuel.prefab` (NetworkObject,
  NetKey, OnlineDuel, with its `GlobalObjectIdHash` filled in batch mode) and its `NetPrefabList`, networks the scene
  with Unity.Multiplayer's `NetSceneSetup`, and sets the `NetBootstrap` never to start by itself: no auto start, and
  above all no serving when headless, since batch-mode tests and builds load this scene too.

### 4.5 The fighters {#UQD-§4.5}
Cosmetic data only ([§3](#UQD-§3)). `Data/assets.json`'s `fighters` section, written by `OriginalAssetGenerator`
from `load_characters.gml` (in its id order: Myrmidon, Ronin, Gypsy, Vagabond, Exile), gives each fighter its
`character_name` and its `character_asset_idle/run/attack/defeat`, `weapon_asset` and `weapon_asset_ground` sprites;
the stats, skills and portraits in the same script are not read. `AssetManifest.Fighters` validates it.

- **Who stands where.** The authority draws one fighter roll per match (`MatchSession.FighterRoll` locally, on
  construction, on a mode change and when a finished match is replaced; `OnlineDuelHost.FighterRoll` online, sent with
  every `RoundBegan` so both peers agree), and `FighterDraw.Pick` maps it onto a fighter for each seat, both
  independently and uniformly, so both seats can be the same fighter.
- **On the field** (`FighterLineup`, from `globals.gml`): the left fighter stands at a quarter of the screen width
  (`left_position_idle = room_width / 4`) facing right, the right one at three quarters
  (`right_position_idle`) mirrored to face left (`image_xscale = -1`), both centred two thirds of the way down
  (`vertical_position = room_height - room_height / 3`), drawn at the original's 640 x 480 scale (2.25x on the
  1440 x 1080 stage, the room's own frame since UQD-T50).
- **Fade in.** Each fighter starts invisible and fades in at `obj_player`'s `fadespeed`, 0.01 alpha per 60 fps tick
  (1.67 s to full), whenever a new pair takes the field (the scene's first frame and every new match). It is timed
  by the clock, 0.6 alpha a second, not counted per rendered frame ([UQD-LAW-2](#UQD-LAW-2)); a stalled frame
  advances it by at most 50 ms, as the original's frame-locked fade would not have jumped either.
- **Same fighter, two colours.** When both seats hold the same fighter, the left one is tinted `color_red`
  (RGB 196, 64, 64) and the right one `color_blue` (64, 64, 196), as `obj_game` does with `image_blend`;
  otherwise neither is tinted.
- **Deliberate deviation**: the original's player chose a fighter on the character select screen (Myrmidon by
  default) and its single-player opponent was `choose_opponent`'s random pick per location; both screens are
  dropped, so the rebuild draws both seats at random for each match.
- **Motion through a round** (`FighterMotion`, UQD-T35, from the `set_state_*` scripts, `obj_player`, `gameclock_action`
  and `obj_gameclock`'s alarm). On the cue both fighters switch to their run sprite on their run marks, 40% and 60%
  of the width (`left/right_position_run = room_width / 2 -/+ room_width / 10`), and creep together 0.01 original
  pixel per step at the cue's `actionFPS` of 1000, 10 original pixels a second, timed by the clock
  ([UQD-LAW-2](#UQD-LAW-2)), until the verdict. The verdict (`determine_winner`) puts both at the middle: the winner
  plays its attack strip, drawn in front (`depth = -1`), dashing on each 60 fps step by `1 + 0.1` of the gap left to
  the loser's idle mark; the loser plays its defeat strip. Strips advance at `image_speed` 0.1 (a frame every 10
  steps) and hold their last frame (Animation End). Three seconds after the verdict (`alarm[0]`) the winner shows its
  idle sprite again where it stopped; the next round puts both back on their idle marks. A draw or nobody striking
  moves no one, as `determine_winner`'s draw branch does nothing. An early strike plays the same winner and loser
  motion from the idle marks, as the original's premature strike does.
- **Fixed defect**: `obj_player`'s dash for a winner facing left tests `x < right_position_idle` instead of
  `x > left_position_idle`, so the right-hand winner never stops and drifts on to 10 pixels past the left mark; the
  rebuild mirrors the left-hand winner's dash and stops it on the mark.
- **The killing blow, matches** (UQD-T64): the original held a round's loser on the first defeat frame while it had
  HP left (`if(HP > 0) image_index = 0` every step) and played the whole strip only for the killing blow, whose
  Animation End destroyed the blood emitter. Best of 3 stands in for HP ([§3](#UQD-§3)), so the round that decides the
  match is the killing blow: its loser plays the whole defeat strip and the blood stops when the strip ends (40 steps,
  `FighterMotion.StripTicks`, `BloodSpray.StopAfter`); every other round's loser is held on the strip's first frame and
  the blood runs on until the loading fade. (UQD-T35 had played the whole strip every round; restored to the original
  under the faithful-port rule.)

### 4.6 Intro and menu flow {#UQD-§4.6}
The game runs three scenes in the order `BuildScenes` keeps them: `Intro.unity`, `MainMenu.unity`, `Duel.unity`, each
written by its builder ([UQD-LAW-4](#UQD-LAW-4)) and each framed as the original's 640 x 480 room letterboxed into the
window (`OriginalFrame`), with F1 toggling fullscreen (`GameWindow`) and the katana cursor (`KatanaCursor`).

1. **Intro** (`IntroController`, `IntroScene`): `bg_mapscreen` under drifting clouds, the original's story typed out,
   then a loading image fades in and hands on. A click, key or pad button skips to that fade; F1 does not skip.
2. **Main menu** (`MainMenuController`, `MainMenu`): the title over the scrolling splatter. New Game starts a duel vs
   the computer, Versus a two-player duel, Quit exits, Credits rolls the author and arena photo credits (a click,
   Enter or Escape closes it), and the slot where the original had Options holds the computer's difficulty (Easy,
   Normal, Hard). Mouse, arrows or d-pad, and Enter all drive it. The choice reaches the duel through
   `DuelModeHandoff`, and the scene change runs under the loading fade (`LoadingHandoff`).
3. **Duel**: the rounds of [§4.1](#UQD-§4.1), in the mode the menu chose. A finished match
   cuts to the verdict screen (`DuelVerdict`): Retry starts a new match in the same mode, Continue returns to the menu.

The `Options`, `Scores`, character select and map rooms of the original are dropped ([§3](#UQD-§3)).

### 4.7 Arenas {#UQD-§4.7}
`Data/assets.json`'s `arenas` section, written by `OriginalAssetGenerator` from `load_arenas.gml`, names the 11 arenas
with a description, a backdrop sprite and the ambient effect `setup_arena_properties.gml` placed there. The authority
draws an arena roll per match (`MatchSession.ArenaRoll`, `OnlineDuelHost.ArenaRoll` online so both peers agree) and
`ArenaDraw.Pick` maps it onto the list; `ArenaBackdrop` is the arena as the scene carries it. The cue inverts the
backdrop only (a real colour invert). The arena's ambient effect (`AmbientField`, `DuelAmbient`) is cosmetic and waits
while the duel waits on the first strike. Details are in [§6](#UQD-§6).

### 4.8 Deviations from the original {#UQD-§4.8}
Current with the faithfulness audit of 2026-10-26 (UQD-T63 to T69, T73). Under the faithful-port rule a working
original behaviour is kept exactly; these are the only places the rebuild differs on purpose, each with its reason.

- **Event-timestamp timing** instead of 1 ms ticks at 1000 fps ([UQD-LAW-2](#UQD-LAW-2)).
- **No clash**: a dead heat scores nobody ([§3](#UQD-§3)).
- **Cosmetic fighters, fixed AI ranges**: no stats, and Easy / Normal / Hard stand in for the adaptive ART
  ([§4.5](#UQD-§4.5)); both seats are drawn at random per match as there is no character select.
- **Best of 3 for HP**: the match-deciding round stands in for the killing blow ([§3](#UQD-§3)).
- **Taunt wording**: the Android build's ten lines replace "Premature Strike!" ([§6](#UQD-§6)).
- **Online only**: the 3 s strike timeout, the host's verdict and the host's Space / Start to begin a round, because
  a peer can vanish and online has no original ([§4.4](#UQD-§4.4)).
- **No music**: the original's jukebox is commented out.

Restored by the audit, so no longer deviations: the arena once per match (UQD-T63), the loser held on the first
defeat frame unless the round decides the match (UQD-T64), Escape to the main menu at any moment (UQD-T65), the
self-running startup with no Ready screen (UQD-T66), no in-duel mode cycle (UQD-T67), a local round with no strike
timeout (UQD-T68), the original's strike keys (UQD-T69), and no HUD the gamescreen never drew (UQD-T73).

## 5. The Laws {#UQD-§5}

Unity.Rea.QuickDuel **inherits the org-wide house rules** in `../../MindAttic.HouseRules.md`. Of note here:
HOUSE-LAW-8 (done is verified, not asserted), HOUSE-LAW-14 (docs publish through the shared docs site),
HOUSE-LAW-15 (don't wait on a full local test run before landing).

Project-specific laws:

### UQD-LAW-1 — The rules stay engine-free {#UQD-LAW-1}
Everything that decides who wins lives in `Assets/Unity.Rea.QuickDuel/Core` with `noEngineReferences: true`,
and compiles and is tested outside Unity through `Tests/Core.Tests`. Runtime code feeds timestamps in and
draws what comes out; it never judges a round itself.

### UQD-LAW-2 — Time by event, never by frame {#UQD-LAW-2}
A strike carries its input event's own timestamp (`CallbackContext.time`), and every clock handed to the
`Duel` is `Time.realtimeSinceStartupAsDouble`, the same timeline. The AI strikes at its drawn time, not
at the frame that notices it. No rule may read `Time.frameCount`, `deltaTime` or "this frame".

### UQD-LAW-3 — One moment only {#UQD-LAW-3}
A change that adds stats, items, skills, maps or any input other than "strike" and "next round" (plus the
between-rounds mode toggle, and the main menu's choice of mode and computer difficulty, UQD-T45) is out of scope
unless this Bible is changed first ([§3](#UQD-§3)).

### UQD-LAW-4 — The scene is generated {#UQD-LAW-4}
`Assets/Scenes/Duel.unity` is written by `DuelSceneBuilder` (menu *QuickDuel → Rebuild Duel Scene*, or
batch mode), `Assets/Scenes/Intro.unity` by `IntroSceneBuilder` (*QuickDuel → Rebuild Intro Scene*, UQD-T44) and
`Assets/Scenes/MainMenu.unity` by `MainMenuSceneBuilder` (*QuickDuel → Rebuild Main Menu Scene*, UQD-T45); the UI Toolkit
trees are built in code by `DuelView` (UQD-T31), `IntroController` and `MainMenuController`, and the build settings keep
the scenes in the order the game runs them (`BuildScenes`: intro, main menu, duel). Never hand-edit the scene YAML.

### UQD-LAW-5 — One home per fact {#UQD-LAW-5}
Rules live here; history and the surveyed old builds live in [ORIGINS](ORIGINS.md); story and ticket status
live in [USER_STORIES.md](USER_STORIES.md) (generated — never hand-edited, see `tickets db render`).

## 6. Verified state {#UQD-§6}
- ✅ Engine-free duel core: phases, random standoff, early strike, settle window, timeout (UQD-T1, `DuelTests`).
- ✅ `DuelJudge` from reported reactions, with the human floor (UQD-T13, `DuelJudgeTests`).
- ✅ Best-of-N `Match` with best reactions (UQD-T7, `MatchTests`); AI opponent (UQD-T8, `AiOpponentTests`).
- ✅ Core tests in CI on push and pull request (UQD-T9).
- ✅ Duel scene and controller (UQD-T3), two-player input by event timestamp (UQD-T2), presentation (UQD-T4),
  taunts (UQD-T6), original Bushido Coda audio (UQD-T5), vs-AI mode and match scoreboard (UQD-T15) —
  PlayMode tests under `Assets/Unity.Rea.QuickDuel/Tests/PlayMode`.
- ✅ Tuning is data (UQD-T28, `TuningRegistryTests`): every number (round clock, AI reaction ranges, wins needed,
  human floor, online report grace, taunt lines) lives in `StreamingAssets/Settings/tuning.json` as named profiles
  (`standard` holds figures re-derived from BushidoCoda.gmx, see the tuning provenance below), validated on load with every problem named by file,
  key and line, and read through `TuningRegistry` / `TuningStore`; Core has no number baked in. The
  `com.mindattic.unity.shared` package is in the manifest, the shared profile sets the game's volume and session mute
  (UQD-T61, UQD-T74; the registry itself is Core code). `TuningStore` reads the file from disk, so it is desktop-only: Android
  and WebGL need a UnityWebRequest source before they ship.
- ✅ Tuning provenance (UQD-T57, checked against `BushidoCoda.gmx` and the Android `BattleSystem.java`, 03-15-13):
  - **Countdown, 4 s**: `obj_gameclock` `countdown_time = 4 * room_speed`, "Get Ready" for the first quarter, then
    3, 2, 1 at `gameclock_countdown.gml`'s 25%, 50%, 75%. The rebuild had wrongly kept 3 s; fixed, and the
    number shown follows the same quarters (`Duel.CountdownNumber`).
  - **Standoff, 3 to 6.5 s**: `drumroll_time = irandom_range(3 * room_speed, 6.5 * room_speed)`; matches.
  - **Taunts**: the ten lines of `RandomPrematureString` in the Android `BattleSystem.java`, drawn uniformly
    (`GetRandomNumber(1, 10)`); matches exactly. (The GameMaker build shows only "Premature Strike!"; the rebuild
    draws the Android line where and as the GameMaker build drew its own, UQD-T39.)
  - **AI reaction ranges (Easy 450-700, Normal 300-450, Hard 200-300 ms)**: the original has none. Its computer
    strikes at `ART + irandom_range(-LCK, LCK)` ticks at 1000 fps with LCK 5 to 10, and ART starts at 15
    (`obj_player`), set from the human's ART at `obj_game` creation and thereafter fed only by its own past
    reactions (`calculate_art` averages them), so it never adapts to the human and always strikes in about
    5 to 25 ms: unbeatable. **Fixed defect**: the rebuild's fixed difficulty ranges stand in; they are the
    rebuild's own figures, not the original's.
  - **Wins needed, 2**: the original has no rounds-to-win rule (an HP bar per side, `MAXHP = 50 + VIT * 10`,
    damage from `calculate_damage` as `PWR * 2 + irandom_range(0, LCK)`, doubled on a 1-in-20-ish crit; not
    from the reaction gap); best of 3 is the rebuild's own deliberate rule.
  - **Human floor, 100 ms**: the original has none (it ran locally); the rebuild's own, for online reports only.
  - **Strike timeout 3 s, settle window 50 ms, online report grace 2 s**: the rebuild's own, for timestamped
    and online input; the original has no equivalent.
- ✅ Standalone Windows build with `run.ps1` (UQD-T11).
- ✅ Docs site on GitHub Pages via the shared pipeline (UQD-T10).
- ✅ Online PvP protocol and transport over Unity.Multiplayer, per [§4.4](#UQD-§4.4) (UQD-T14):
  `OnlineDuelTests` in Core.Tests, and host + client PlayMode tests in `Tests/Online`.
- ✅ The round headline names the AI in vs-AI mode (UQD-T17, `DuelSessionTests`).
- ✅ End-to-end port check of the Windows build against `BushidoCoda.gmx`: every mode reached and
  played to a match verdict; the room-by-room map is in [ORIGINS](ORIGINS.md) (UQD-T19).
- ✅ Render pipeline (UQD-T29, `RenderPipelineTests`): the project had no render pipeline package at all
  (the built-in pipeline, no URP in `Packages/manifest.json`, `GraphicsSettings.defaultRenderPipeline`
  null). URP 17.4.0 is now in the manifest; `RenderPipelineBuilder` (menu *QuickDuel → Build Render
  Pipeline*, or batch mode, and run by `DuelSceneBuilder` before every scene rebuild) creates
  `Assets/Unity.Rea.QuickDuel/Rendering/QuickDuelURP.asset` (a forward Universal Renderer — the game has no
  sprites or 2D lights, only UI, so the 2D renderer is not needed) and assigns it as the default
  pipeline and on every quality level. The pipeline-agnostic layer is the whole game: `DuelView` draws
  only a UI Toolkit tree (UQD-T31) over a camera backdrop; no shader or material in the repo names URP.
  Verified by a Windows Standalone build (`run.ps1`) that launches and renders the Duel scene.
- ✅ Session and match flow moved to Core (UQD-T30, `MatchSessionTests`): `MatchSession` is now the one
  authority over the round, mode and match, with `Strike`/`BeginRound`/`SetMode` each carrying a player id
  (§4.3); `DuelController` and `DuelSession` are thin Unity views over it, covered by the existing PlayMode
  suite (`DuelSceneTests`, `DuelSessionTests`, `DuelQuitTests`, `DuelInputTests`, `DuelTauntTests`,
  `DuelAudioTests`, `DuelViewTests`).
- ✅ UI Toolkit, not legacy uGUI (UQD-T31, `DuelViewTests`/`DuelSessionTests`/`DuelTauntTests`): the UI was
  `UnityEngine.UI.Text` off `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` drawn on a hand-built
  `Canvas`/`CanvasScaler`, AGENTS.md rule 6's other standards gap ("UI Toolkit or the shared MindAttic UI
  components"). `DuelView` now builds a `UIDocument`/`VisualElement` tree in code (the scene is still
  generated, [UQD-LAW-4](#UQD-LAW-4)) through `Resources/UI/DuelPanelSettings.asset` (scale-with-screen-size,
  1920x1080 reference, since UQD-T50 the room's 1440 x 1080 frame fitted whole (Expand) — built once by `DuelPanelBuilder`, run by `DuelSceneBuilder` the same way
  `RenderPipelineBuilder` is), `Resources/UI/Duel.uss` for its structural USS classes, and a font asset
  (`Resources/UI/Fonts/Cinzel.ttf`, OFL-licensed, already vetted for MindAttic games in Unity.GridGame; replaced by
  the original's own bitmap fonts in UQD-T36) set
  on the root so every label inherits it. `DuelSession` and `DuelTaunt` build their own labels the same way
  (`DuelView.CreateLabel`/`CreateEdgeLabel`), through the same `VisualElement` tree. The `DuelView` surface
  the PlayMode tests read (`CountdownText`, `CueText`, `HeadlineText`, `PromptText`, `ReactionText`,
  `HasFallen`, `Inverted`, `LetterboxAmount`, `FormatReaction`, `Headline`) is unchanged; `UnityEngine.UI`
  is no longer a Runtime assembly reference.

- ✅ Online PvP in the Duel scene (UQD-T21, [§4.4](#UQD-§4.4)): the Host / Join menu, and `DuelController`,
  `DuelSession` and `DuelView` driven by an `OnlineDuel` instead of the local `MatchSession`. `OnlineDuelSceneTests`
  plays a host's and a joined client's scene over real loopback (the host starts the round, each strikes on its own
  clock, both scenes show the host's verdict and score, and a leaver's seat empties while their scene goes back to
  local play); `OnlineDuelMenuTests` loads the generated scene and checks that it never hosts or serves by itself,
  that Host / Leave put it online and back, and that a bad address is refused with a reason.

- ✅ The original's art, sound and fonts in the project (UQD-T32, `AssetManifestTests`): `OriginalAssetGenerator`
  (menu *QuickDuel → Import Original Assets*, or `-executeMethod Unity.Rea.QuickDuel.EditorTools.OriginalAssetGenerator.Generate
  -originalRoot <BushidoCoda.gmx>`) copies out of the GameMaker project the 76 sprites the parity tickets draw (the five
  fighters' idle, run, attack and defeat frames, the 12 weapon and ground-weapon sprites, the 11 arenas `load_arenas.gml`
  names, the 14 loading images, the title, four corner arts, the katana cursor, two blood drops, ten splatters and the
  flash), the twelve duel, hit-ground, flick and click sounds, the five bitmap fonts (atlas plus GameMaker glyph table)
  and the Old English TrueType face, plus `credits.txt`, and writes `Data/assets.json` from each sprite's
  `.sprite.gmx` (size, origin, frames) and `load_arenas.gml` (names, descriptions). `OriginalAssetImportRules` applies
  the import settings in code on every import: single sprite, 1 pixel per unit, point filter, no mipmaps, no
  compression, pivot at the GameMaker origin. The copies and their `.meta` files are committed, so neither the game nor
  CI needs the original; `AssetManifest` (Core) validates the manifest on load. The one exception is
  `sfx_defeat.ogg`: the original's `.ogg` fails Unity's FMOD import, so the committed re-encode of its `.wav`
  (UQD-T24) is kept and the generator never overwrites it.

- ✅ The 11 arenas and a real invert (UQD-T33, `ArenaDrawTests`, `DuelViewTests`): every match is fought on one of
  `load_arenas.gml`'s arenas (Wastes of Nod, Pastel Fields, Frozen Tundra, The Fumaroles, Dusty Junction, Sozza's Point,
  South Ridge, Arid Plains, Interior Sea, Tu'verone Forest, Harvest Cresent), read from the data file
  `Data/assets.json` (name, description, backdrop sprite) by `DuelSceneBuilder` into `DuelView`'s serialized arena list.
  The authority draws an arena roll with each match (`MatchSession.ArenaRoll` locally, `OnlineDuelHost.ArenaRoll`
  online, sent in the `RoundBegan` RPC so both peers show the same arena) and `ArenaDraw.Pick` maps it onto the list;
  the backdrop covers the screen behind everything else. On the cue the whole backdrop inverts with a UI Toolkit
  `invert` filter, the same per-pixel `1 - colour` as `obj_arena`'s `draw_set_blend_mode_ext(10, 1)` (inverse
  destination colour) rectangle over the view; the old tint of the placeholder boxes and labels is gone, and as in
  the original only the backdrop inverts, not what is drawn over it.
  - **Fixed defect**: `load_arenas.gml` gives South Ridge Sozza's Point's id (5), so South Ridge overwrote it and
    `irandom_range(0, 9)` could only ever draw 10 arenas; every one of the 11 is drawn now.
  - **Once per match, matches** (UQD-T63): the original rolled its arena once per game (`choose_random_arena.gml`,
    called only from `obj_location`, the arena kept by `obj_game`); the rebuild draws one with each match and keeps it
    for every round of that match. (UQD-T33 had drawn one per round; restored to the original under the faithful-port
    rule.)
  - The display name keeps the original's spelling, "Harvest Cresent", as `load_arenas.gml` has it; the names and
    descriptions were only shown on the dropped overworld (`obj_locationart`), so nothing shows them yet.

- ✅ The five original fighters, cosmetic only (UQD-T34, [§4.5](#UQD-§4.5); `FighterDrawTests`, `AssetManifestTests`,
  `DuelViewTests`): Myrmidon, Ronin, Gypsy, Vagabond and Exile from `load_characters.gml` (name, idle, run, attack and
  defeat strips, weapon and ground weapon; no stats) in `Data/assets.json`, carried into the scene by `DuelSceneBuilder`
  as `DuelView`'s `FighterLook` list in place of the placeholder boxes. The authority draws a fighter roll per match
  (sent with `RoundBegan` online); the pair stands in its idle sprites at a quarter and three quarters of the width,
  facing each other, fades in at 0.01 alpha per original 60 fps step, and is tinted red and blue when both seats hold
  the same fighter.
- ✅ The fighters' motion (UQD-T35, [§4.5](#UQD-§4.5); `FighterMotionTests`, `DuelViewTests`): on the cue both run
  from 40% and 60% of the width, creeping together 0.01 pixel a 1000 fps step; the winner dashes from the middle on to
  the loser's mark (easing `1 + 0.1` of the gap a 60 fps step) playing its attack strip in front, the loser plays its
  defeat strip at the middle, and three seconds on the winner returns to idle. The loser no longer topples.
- ✅ The original's type (UQD-T36, `OriginalTypeTests`, `OriginalTextTests`): the five GameMaker bitmap fonts are UI
  Toolkit font assets (`Resources/UI/Fonts/fnt_basic`, `fnt_mono`, `fnt_oldenglish`, `fnt_oldenglish_small`,
  `fnt_tiny`), built in code by `OriginalFontBuilder` (menu *QuickDuel → Build Original Font Assets*, rerun by the asset
  generator and, when missing, by `DuelSceneBuilder`) from each font's committed atlas and glyph table
  (`BitmapGlyphTable`, Core): static TextCore assets whose glyphs are GameMaker's own cells (atlas rect, offset, shift,
  cell top at the line top), on an Alpha8 copy of the atlas so the text colour tints them, point-filtered, at the
  original's pixel scale (2.25 stage pixels a pixel). Every piece of duel text is `OutlinedText`, the one shared style:
  `draw_text_outline`'s black copies at each offset of the (2t + 1) square, then the fill on top, white unless a colour
  is given. `OriginalType` (Core) says which font and thickness each kind of text takes, as the original drew it:
  banners, the countdown and the reaction count in Old English outlined 1 (`obj_gameclock`), the title outlined 2 like
  "Game Over", the winner's name in the small Old English, the intro in the mono face outlined 2 (`obj_typewriter`), the
  HUD and buttons in the basic face, the tiny face for small readouts. The root carries the basic face, so the online
  menu's controls use it too. Cinzel, the placeholder font of UQD-T31, is gone.
  - **Deliberate decision**: the ticket named the `oldenglish`, `sansation`, `cyberia`, `gnuolane`, `infinity` and
    `junction` TrueType files. The GameMaker original never draws with any of them: all five of its fonts were baked
    into bitmap atlases from the system faces Old English Text MT, Monotype Corsiva and Estrangelo Edessa, and the
    other five `.ttf` files belong to the Android builds (§2.4 of RECONSTRUCTION). The rebuild therefore draws the
    original's own baked glyphs, pixel for pixel, rather than re-rasterising look-alike faces; `oldenglish.ttf` stays
    imported (UQD-T32) and the Android faces are not brought in.
- ✅ Get Ready and a fading 3-2-1 (UQD-T37; `DuelTests`, `DuelViewTests`): `obj_gameclock`'s draw event shows
  "Get Ready" (`CountdownBanner.Text`) at `default_alpha` for the whole countdown, 44 original pixels above the number
  (`room_height/2 - 60` against `- 16`), in the Old English outlined 1 of `OriginalType.Countdown`. Each of 3, 2, 1
  appears at full alpha with its `sfx_beep` as its quarter begins and fades at `gameclock_countdown.gml`'s 0.02 alpha a
  60 fps step (`alpha -= 0.02`), so it is gone 50 steps (0.83 s) later, before the next one; `Duel.CountdownNumberAlpha`
  times the fade by the clock, never by the rendered frame.
  - **Countdown length, matches**: the original's countdown is `countdown_time = 4 * room_speed`, which reads as 1 s of
    Get Ready alone (`count < countdown_time * 0.25` draws no number) plus a 3 s count of 3, 2, 1, a second each. The
    rebuild's `CountdownSeconds` is 4 (`tuning.json`, UQD-T57), split the same way by `Duel.CountdownNumber`, so the
    two agree exactly; at any other `CountdownSeconds` the quarters scale with it, while the fade stays at the
    original's 1.2 alpha a second.
- ✅ Live reaction counter (UQD-T38; `DuelTests`, `DuelViewTests`): while the cue is up `obj_gameclock` draws
  `string(ceil(count))` in the centre (`room_height/2 - 16`, where the countdown's number was) in the Old English
  outlined 1, and `gameclock_action.gml` runs the action at `actionFPS` = 1000, so `count` is milliseconds since the
  cue, 1 on the cue's own step. `Duel.ReactionCount` reads it off the event clock (`ReactionCounter.Count`), never the
  rendered frame (UQD-LAW-2), capped at the strike timeout. The first strike freezes it at that strike's own count
  (`action_attack.gml`: `reaction_time = gameclock.count`), and from there it fades at `gameclock_aftermath.gml`'s 0.003
  alpha a 60 fps step (gone 333 steps, 5.6 s, later; `Duel.ReactionCountAlpha`), held through the verdict of a round
  decided by reaction; an early strike or nobody striking shows none. Online, each peer's count freezes at the first
  strike its own round saw.
  - **The first strike, matches**: in the original the first strike ends the action (`mode = "aftermath"`), so the
    number stops there; the rebuild's Strike phase waits `SettleSeconds` (or the second strike) before its verdict, and
    the count is frozen from the first strike all the same.
  - **The cue, matches** (UQD-T73): the original's cue is the backdrop's invert and the unsheath alone; the red "!" the
    rebuild had drawn over it is gone.
- ✅ Winner banner and sliding reaction (UQD-T39; `VerdictBannerTests`, `DuelViewTests`, `DuelTauntTests`): in the
  aftermath `obj_gameclock` draws, above the loser's idle mark (`right_position_idle` when the left won,
  `left_position_idle` otherwise), the winner's `character_name` in `fnt_oldenglish_small` at `room_height/2 - 80` over
  "Wins!" in `fnt_oldenglish` at `room_height/2 - 60`, both outlined 1 at `default_alpha`; and the winner's
  `ceil(reaction_time)` at `aftermathX`, `room_height/2 - 16`, which `gameclock_aftermath.gml` moves one pixel a 60 fps
  step away from the loser (`aftermathX -= 1` when the left won, `+= 1` otherwise) while its alpha falls 0.003 a step.
  The rebuild's `DuelView` draws the name (the winner's `FighterLook.Name`) and "Wins!" there (`VerdictBanner`), and
  slides UQD-T38's frozen count by `VerdictBanner.Slide`, timed by the clock from the first strike, the same start as
  its fade (`DuelController.ReactionCountSlide`; online the host's verdict sets the direction). A premature strike draws
  its line centred on that same `room_height/2 - 60`, white, `fnt_oldenglish` outlined 1 (`DuelTaunt`). The headline
  ("Left wins") is no longer drawn over either; it stays only for the rebuild's own outcomes the original never drew (a
  tie, nobody striking, a forfeit) and for "..." while an online verdict is on its way. The per-side reaction times the
  rebuild had drawn under the fighters are gone (UQD-T73): both sides' `reaction_time` showed only on F12's debug
  overlay (`DuelDebugOverlay`), and the scoreboard keeps each side's best.
  - **Taunt wording, deviation**: the GameMaker build's line is always "Premature Strike!"; the rebuild keeps the
    Android build's ten taunts (`tuning.json` `taunts`, which include "Premature Strike"), drawn in its place.
  - **The slide's start, matches**: the original's aftermath, its fade and its slide all begin at the first strike; the
    rebuild's verdict waits `SettleSeconds` (50 ms) or the second strike, so when the banner appears the number has
    already slid the 3 or so original pixels of that wait, rather than starting from the centre late.
  - **A dead heat, fixed defect**: `determine_winner.gml` sets no `last_winner` on a draw, so the original would have
    put "Wins!" over the previous round's loser naming the previous winner; the rebuild's tie shows no banner and no
    slide, the count held at the centre under the "Draw" headline.
- ✅ Blood, splatter and flash (UQD-T40; `BloodSprayTests`, `SplatterFieldTests`, `WhiteFlashTests`, `DuelBloodTests`):
  `gameclock_aftermath.gml`'s first pass creates `obj_blood_emitter` at `room_width/2, vertical_position`, 6 to 12
  `obj_splatter` and one `obj_flash`. The emitter's alarm creates an `obj_blood_drop` every 2 steps (`spr_blood_drop1`
  or `2`, even odds); each drop leaves at `velocityX = irandom_range(-5,5)`, `velocityY = irandom_range(2,12)`, one in 32
  a "huge spurt" of `irandom_range(12,24)`, and every 2 steps moves `x += velocityX; y -= velocityY; velocityY -= 1;
  y += 1` until it is below `room_height + sprite_height`. A splatter is one of `spr_splatter1` to `10` centred anywhere
  from half a sprite off one edge to half off the other, at `image_alpha` 0.95 falling 0.0003 a step (about 53 s), and
  0.01 a step (about 1.6 s) once a left or right click lands on it or the cursor is dragged into it with a button held;
  nothing else removes one, so they outlive the round. The flash is `spr_flash` (plain white) stretched over the room
  at 0.6, toggled every step for ten blinks (white on steps 0 and 1, then every odd step, gone on step 20). Depths are
  kept: drops (1) behind the fighters and text (0), splatters (-100) over them, the flash (-1000) over those and the
  letterbox (-10000) over all. Core holds the rules (`BloodSpray`, `SplatterField`, `WhiteFlash`); `DuelBlood` draws
  them, stepping at 60 a second off the controller's clock (UQD-LAW-2), the drops on the stage at the fighters' pixel
  scale and the splatters spread over the real screen. The scene carries the two drops, the ten splatters with each
  one's click box and the flash (`DuelSceneBuilder`, from the asset manifest).
  - **Which rounds bleed, matches**: in the original every strike sends the round to the aftermath, so every one bleeds;
    the rebuild spills on a fair win, an early strike and a tie (the original's clash went on to an aftermath too), and
    not for its own endings where nobody is cut (nobody striking, a forfeit).
  - **The emitter's end, matches**: the original destroys it when the loading fade (0.005 a step, 200 steps) has covered
    the screen after the 3 s aftermath, so it spurts for 380 steps; the rebuild stops it there, or sooner when the next
    round begins, and drops already in the air fall on.
  - **The click box, matches**: `obj_splatter`'s mouse events use the sprite's automatic rectangle mask (colkind 1,
    bboxmode 0); the builder measures each splatter's opaque-pixel bounds from its image, which equal every `bbox_*`
    the original's `.sprite.gmx` records, and only a click inside that box wipes it. The pointer is only read, so the
    click still strikes. The original has no key that wipes splatters, so neither does the rebuild.
  - **Splatter height, fixed defect**: `obj_splatter` draws its y from `irandom_range(-(sprite_height/2), room_width +
    (sprite_height/2))`, the room's width (640) where its height (480) was meant, so about a quarter of the splatters
    fell below the screen; the rebuild spreads them over the screen's height.
  - **The start, matches**: as with the sliding number, the original's aftermath begins at the first strike and the
    rebuild's at the verdict, up to `SettleSeconds` (50 ms) later.
- ✅ The loser's weapon thrown (UQD-T41; `WeaponTossTests`, `DuelWeaponsTests`): `gameclock_aftermath.gml`'s first
  pass calls `spawn_weapons.gml` after `determine_winner`, which creates an `obj_weapon` at `room_width/2,
  vertical_position` with the loser's `weapon_asset`, first a second one behind it for the Myrmidon (its shield,
  `id_shield`) or the Ronin (a second sword), each mirrored (`image_xscale = -1`) when the right fighter lost. Each piece
  leaves at `velocityX = irandom_range(5,6)`, negative with `spin = 15` when the left won and positive with `spin = -15`
  when the right did, `velocityY = irandom_range(12,20)`, and picks `landY = irandom_range(ground_level - 10,
  ground_level)` (`ground_level` is the fighters' feet, 384). Its step event turns it `spin` degrees every step and its
  alarm, every 2 steps, turns it `spin` more and moves it `x += velocityX; y -= velocityY; velocityY -= 1; y += 1` while
  `y < landY`; the first alarm that finds it at or below `landY` plays one of `sfx_hitground1` to `4` (`choose`), swaps
  in its `weapon_asset_ground` sprite and sets its angle to 0, and it lies there until the next round's loading fade
  destroys it. `obj_weapon` is at depth -1: over the text and the fallen loser, level with the attacking winner (drawn
  over it) and under the splatters. Core holds the rule (`WeaponToss`); `DuelWeapons` draws it on the stage at the
  fighters' pixel scale, stepping at 60 a second off the controller's clock (UQD-LAW-2), and plays the sounds. Weapons
  stay cosmetic data: each fighter's in-hand and ground sprites come with its `FighterLook`, the shield and the four
  sounds from the asset manifest (`DuelSceneBuilder`).
  - **Which rounds throw, matches**: a fair win and an early strike, where a loser falls; nobody striking and a forfeit
    cut no one and throw nothing.
  - **A dead heat, fixed defect**: `determine_winner.gml` leaves `last_loser` as the previous round set it (the right
    fighter in the first round), so on a draw `spawn_weapons` threw the previous loser's weapon though nobody fell; the
    rebuild's tie throws nothing.
  - **Clearing, matches**: the original destroys every `obj_weapon` when the next round's loading fade covers the
    screen; so does the rebuild (UQD-T42), or sooner when the next round begins first.
- ✅ The round transition (UQD-T42; `RoundFadeTests`, `DuelFadeTests`, `DuelInputTests`): `gameclock_aftermath.gml`
  counts `aftermath_time = 3 * room_speed` steps from the cut and sets `obj_fade_gamescreen.mode = "fadein"`; the fade
  (depth -10000, over everything) shows one of `spr_loading1` to `14` (`irandom_range(1, 14)`) and raises its alpha
  `fadespeed = 0.005` a step; once it is full it resets the battle (`with (obj_blood_emitter)` and `with (obj_weapon)
  instance_destroy()`, `gameclock.mode = "startup"`), whose first pass sets both fighters idle, clears the reaction
  times and the clock's text alpha, and sends the fade to `"fadeout"`, 0.005 a step down to nothing. Counted in 60 a
  second steps from the moment the round ended (`Duel.EndedAt`: the first strike, the early strike, or the timeout),
  the image fades in from step 180, is full and the battle reset on step 380, and is gone on step 580. Core holds the
  timing and the pick (`RoundFade`); `DuelFade` draws the image over the whole panel off the controller's clock
  (UQD-LAW-2) and raises `BattleReset`, on which `DuelView` puts both fighters back idle and clears the aftermath's
  text, `DuelTaunt` its taunt, `DuelBlood` stops the emitter and `DuelWeapons` clears the weapons.
  - **The splatters, matches**: the ticket's wording says the blood and splatter are cleared, but the original's fade
    destroys only the emitter and the weapons (and `obj_criticalhit`, which the rebuild does not have); the splatters
    are not its to destroy and fade on at their own 0.0003 a step. The rebuild keeps the original.
  - **The startup, matches** (UQD-T66): `gameclock_startup` counts `startup_time` (3 s, `DuelTuning.StartupSeconds`)
    once the image is gone (at once for the scene's first round, `juststarted`) and then starts the countdown by itself;
    `obj_game`'s global left press while it runs (from the reset under the full image on) starts it at once. The
    rebuild's `DuelStartup` does the same with `RoundStartup`'s rule, a pad's South standing in for the click; a
    finished match never starts another (the verdict's button decides). The scene opens under the image it was entered
    with, fading at `obj_fade_gamescreen`'s 0.005 a step (it had used the menu's 0.01). (UQD-T42 had kept a Ready phase
    that waited for Space or Start, with a "QuickDuel" title and a controls prompt the gamescreen never drew; restored
    to the original under the faithful-port rule. Online, which has no original, the host's Space / Start still begins
    each round.)
  - **A round begun early**: online the host may begin the next round before a client's fade is done; the client's
    image then simply goes, and the new round resets the battle as it always did.
- ✅ The corner arts (UQD-T43; `CornerArtTests`, `DuelCornerArtTests`): `gamescreen.room` places `obj_cornerart_left`
  at (0, 24) and `obj_cornerart_right` at (488, 24), drawing `spr_cornerart_left2` and `right2` (152 x 32, top-left
  origin; the plain `left` / `right` sprites are an earlier cut nothing draws) in Draw GUI at depth -101. Their create
  events put them a whole sprite width off the screen (`x = -sprite_width`, `x = room_width`) and each step, until
  `in_position`, closes `1 + 0.05` of the distance left (`x += 1 + abs(x) * 0.05`, mirrored on the right), snapping home
  once the step finds it there or past. So the two move as mirror images of one distance, 152 off, `0.95 d - 1` a step:
  the room's first step runs before its first draw, step 42 carries them 0.03 of a pixel past their places and step 43
  snaps them home, about 0.7 s in, where they stay for the scene (the loading fade does not touch them). Core holds the
  rule (`CornerArt`); `DuelCornerArt` draws them at the stage's pixel scale against the real screen edges, stepping at
  60 a second off the controller's clock from the scene's start (UQD-LAW-2).
  - **Placement, deliberate**: the original's 640 x 480 room was its screen, so its corners were the screen's; the
    rebuild's screen is wider than the room, and the arts go to the real screen corners (24 original pixels below the
    room's top) rather than the room's, so the frame still frames the screen.
  - **Layering, matches**: Draw GUI runs after every ordinary draw, so the arts lie over the fighters, the blood, the
    splatters and the flash, and under the letterbox and the loading fade (-10000).
- ✅ The intro (UQD-T44; `IntroSceneTests`, `IntroTests`): the game now opens on the original's `intro` room, built in
  `Assets/Scenes/Intro.unity`. Core holds the room (`IntroScene`, `LoadingFade`), stepped at 60 a second off the clock
  from the scene's start (UQD-LAW-2), the room's first step before its first draw; `IntroController` draws it at the
  original's pixel scale on a 640 x 480 area centred on the screen. The art and the story are data: `spr_cloud1` to `4`
  and the room's `bg_mapscreen` background are in the asset manifest, and so is `load_text.gml`'s `introText` (the
  generator reads it from the script, `#` line breaks as newlines, the original's "eachother" kept).
  - **Clouds, match**: four cloud sprites (256 x 48, top-left origin) placed three times each, every one at half alpha
    (`image_alpha = 0.5`) moving left a pixel a step (`hspeed = -1`), created at a random x in -128..768 and y in
    -24..456, and, the step one is found past `x < -sprite_width`, sent back to a random x in 640..1280 and a fresh y.
    They draw at depth -100, over the story; the map (`bg_mapscreen`) is the room's background and covers the screen.
  - **Story, match**: `obj_intro`'s alarm creates `obj_typewriter_intro` on step 180 (3 s), which shows one more
    character every 4 steps (`type_speed = 4`; a line break is a character like any other) from (40, 50) in `fnt_mono`
    outlined 2, white; once its alarm finds nothing left to type it holds 300 steps (5 s) and starts the fade. The
    story's own fade-out is never set in the intro, so the text stays at full alpha. An alarm set to n goes off n steps
    later; an object created in a step runs its alarms from the next.
  - **Loading fade, match**: the room opens under `spr_loading1` (globals.gml's `fade_asset`) full, fading out 0.01 a
    step (100 steps); the fade-in rises 0.01 a step from nothing and enters the next room on the step after it is full,
    in double precision as GameMaker counted it. The image is handed on (`LoadingHandoff`, the original's global
    `fade_asset`) and the next scene opens under it, fading it away 0.01 a step, as `obj_fade_mainmenu` did.
  - **Random loading image, fixed defect**: `obj_fade_intro` shares one `firstPass` between its fade-out and its
    fade-in, and the fade-out on room start has already spent it, so the fade-in's first pass, where it picks one of the
    14 loading images (`irandom_range(1, 14)`) and starts it from nothing, could never run: the intro always faded
    `spr_loading1` back in. Entering the fade-in now starts its own first pass, so a random image fades in from nothing,
    as written.
  - **Skip, deliberate**: the original skipped only on a left click (`obj_intro`'s global left press starts the fade).
    A click still does, and so does any key and a pad's South or Start, so a keyboard or pad player is never stuck
    watching it. Skipping starts the same fade, at once, from any moment; a second press changes nothing.
  - **Where it leads, matches**: the intro goes on to the main menu (UQD-T45), which opens under the intro's image.
    F1 toggles fullscreen there as everywhere (UQD-T50) and does not skip.
- ✅ The main menu (UQD-T45; `MainMenuTests` in Core and PlayMode): the intro now leads to the original's `mainmenu`
  room, built in `Assets/Scenes/MainMenu.unity`. Core holds the room (`MainMenu`, `MenuFade`), stepped at 60 a second
  off the clock from the scene's start (UQD-LAW-2); `MainMenuController` draws it at the original's pixel scale on a
  640 x 480 area centred on a black screen. The art is data: `bg_splatter_scroll` and the buttons' `spr_btn_right1`,
  `right2`, `right3` and `left4` with their `_over` twins are in the asset manifest (the "button" group).
  - **Room, matches**: `bg_splatter_scroll` (1279 x 480, white with blood) is the room's background, tiled and moving
    left a pixel a step; `obj_title` draws `spr_titlescreen` over it at depth 100, a black screen with the logo cut out
    of it, so the blood scrolls through the letters. The buttons draw over the title (depth 0).
  - **Buttons, match**: New Game (`spr_btn_right1` at (576, 320)), Versus (`right2` at (576, 384)) and Quit (`left4` at
    (64, 448)), origin (64, 32). Moving the mouse onto one (its sprite's rectangle mask, the `.sprite.gmx` bbox, which is
    the bounds of its opaque pixels) plays `sfx_flick` once and draws its `_over` sprite and its message ("Start a new
    game", "Player vs. Player", "Quit the game") in `fnt_basic`, white and not outlined, centred at (320, 450), until the
    mouse leaves. The label is `fnt_basic` outlined 1, centred 6 px right of x on the right-hand buttons and 6 px left on
    the left-hand one, its top 6 px above y. A press plays `sfx_click`; New Game sets `is_singleplayer`, Versus clears
    it, and both start the fade to the duel; pressing the other one during the fade only changes the mode. Quit and
    Escape end the game at once.
  - **Loading fade, matches `obj_fade_mainmenu`**: the menu opens under the intro's image (`LoadingHandoff`; spr_loading1
    when started on its own) fading out 0.01 a step; the step after it runs out, "off" chooses the random image the menu
    will leave under. Its `firstPass` is shared as in the original: a button pressed during the opening fade turns it
    round, the opening image fading back in from where it had got to; one pressed afterwards fades the chosen image in
    from nothing; either way the duel follows the step after it is full, opening under that image (`DuelFade`).
  - **The mode, deliberate**: `is_singleplayer` goes on to the Duel scene (`DuelModeHandoff`): New Game plays the
    computer, Versus two players. The duel has no key for the mode, as the original's gamescreen had none (UQD-T67; Tab /
    Select had cycled it, with a mode line the gamescreen never drew); a Duel scene loaded without the menu plays two
    players.
  - **Difficulty, deliberate**: the original's computer adapted (its ART); the rebuild's has fixed Easy / Normal / Hard
    ([RECONSTRUCTION §5](RECONSTRUCTION.md)), so the menu chooses it. It takes the slot of the original's Options button (`spr_btn_right3` at
    (576, 448)), an empty stub: its label is the difficulty, a click plays the click and steps Easy, Normal, Hard, and
    its message says what New Game will play. The choice is kept for the menu's next visit.
  - **Keyboard and pad, deliberate**: the original's menu was mouse-only. The arrow keys or the d-pad also walk the
    buttons (highlighting and flicking as the mouse does), and Enter, Space or a pad's South or Start press the
    highlighted one, so a keyboard or pad player is never stuck on the menu.
  - **Dropped**: Options and Scores were empty stubs (Scores jumped to the dropped `travel` room); Credits is built
    below (UQD-T47); F1 fullscreen is built (UQD-T50). The original's menu played no music.
- ✅ The katana cursor (UQD-T46; `KatanaCursorTests` in PlayMode): `obj_mouse`, placed in `mainmenu` and `gamescreen`,
  is `KatanaCursor` on the main menu and the duel (`IKatanaCursorHost`: `MainMenuController`, `DuelView`).
  - **Matches**: the original's Global Game Settings hid the system cursor (`option_showcursor` False) and `obj_mouse`
    moved to (`mouse_x`, `mouse_y`) each step and drew `spr_katana_cursor` (40 x 40, origin (0, 0)) in Draw GUI at depth
    -5000. The system cursor is hidden while either scene runs and the sprite's top-left corner, the blade's tip, is at
    the pointer, at the original's pixel scale. It draws over everything the room drew (the corner arts at -101 among
    them) and under the letterbox (the duel) and the loading fades (both scenes) at -10000, as the original's depths had it.
  - **Gamepad, deliberate**: the original had no pad. While a gamepad is the device last used (any button, the d-pad or
    a stick pushed) the katana is hidden, a pointer nothing is driving; it returns the moment the mouse moves or clicks.
    Which device is active carries from scene to scene.
  - **Not drawn**: the intro (`obj_mouse` was never placed in it; the system cursor shows there as before). With
    `QUICKDUEL_MULTIPLAYER` the online menu, added to the duel's tree after the letterbox, draws over the katana.
- ✅ The credits (UQD-T47; `CreditsRollTests` in Core, `MainMenuTests` in Core and PlayMode): `obj_btn_credits` on the
  main menu opens a credits screen (`CreditsRoll`, drawn by `MainMenuController`).
  - **The button, matches**: `spr_btn_left1` (bbox 0, 2 to 127, 58) at (64, 384), labelled "Credits" 6 px left of x,
    its message "Who made what?", flicking and clicking as the other buttons do. It is fifth in the room's instance
    order, after Quit, so the arrow keys reach it after Quit.
  - **Credits screen, fixed defect**: the original's button only played `sfx_click`; its `room_goto` was commented out
    and no credits room was ever made, so the arena photo credits in `datafiles/credits.txt` were never shown. The
    rebuild shows them: "Credits" in `fnt_oldenglish`, "A game by" and the author (the project's `option_author`,
    Ryan DeBraal, read by the asset generator from `Configs/Default.config.gmx` into the manifest's `text.author`) in
    `fnt_oldenglish_small`, then "Arena photographs" and each picture credits.txt names (Dusty Junction, The Fumaroles,
    Sozza's Point, Southern Ridge, Arid Plains, Interior Sea, Tu'Verone Forest, farmlands, forest2, cliffside, riverbed,
    fumaroles2, Panoramas) with its source addresses, the names in `fnt_basic` and the addresses in `fnt_tiny`, all
    white outlined 1, exactly as the file has them. credits.txt ships as `Data/credits.txt` and the menu reads it when
    it starts; a file that does not read (no credit, or a name without an address) stops the scene build.
  - **Layout, deliberate**: with no original screen to match, the lines roll up the 640 x 480 room from its foot a
    pixel a 60 fps step (the menu background's own speed) and start again from the foot once the last has gone over
    the top; the title, buttons and message stand down and a black shade at 0.8 covers the room, the blood scrolling on
    through it. The line advances (44, 18, 34, 26, 17 and 13 px) and the 12 px gap after each credit are the
    rebuild's own. A click anywhere, Enter, Space, a pad's South or Start, or Escape closes the credits with the click
    (Escape does not end the game while they are up), and the roll starts from the foot each time it is opened.
- ✅ The match verdict (UQD-T48; `VerdictScreenTests` and `AssetManifestTests` in Core, `DuelVerdictTests` in PlayMode,
  screenshots `10-verdict-victory` and `10-verdict-gameover`): the original's victory and gameover rooms. Once a round
  ends the match, the aftermath and the loading fade play as after any round (UQD-T42); when the image is full and the
  battle reset under it, the duel cuts to the verdict, as `obj_fade_gamescreen`'s `room_goto(gameover)` /
  `room_goto(victory)` did (`VerdictScreen` in Core, drawn by `DuelVerdict`): `bg_victory` or `bg_gameover` filling
  the 640 x 480 room, the heading in `fnt_oldenglish` outlined 2 centred on (320, 64), and the button on
  `spr_btn_right4` at (576, 448), its `_over` sprite under the pointer (no flick: the original's verdict buttons had
  none), "Continue" or "Retry" in `fnt_basic` outlined 1 on (x + 6, y - 6), clicking with `sfx_click`. Space / Start no
  longer begins a round under it, and Escape does nothing there, as the original's victory and
  gameover rooms had no Escape (UQD-T65). Not online: an online match has no verdict room
  and its host replaces a finished match on the next round, as before.
  - **Best of 3, deliberate**: the original went there when an HP bar ran out ([§6](#UQD-§6), "Wins needed, 2"); the
    rebuild goes there when a side has its two wins. Game Over when the computer took the match, Victory when the player
    did, and in Versus (which the original never sent to a verdict room, its check being `is_singleplayer` only)
    Victory for whichever player won, named in the result line.
  - **Retry and Continue, deliberate**: both buttons went on to the overworld (`room_goto(travel)`, or `location` in
    Versus), which is dropped. Retry (Game Over) loads the duel again for a new match in the same mode; Continue
    (Victory) goes back to the main menu. Either scene opens under the loading image the verdict cut from (`fade_asset`).
    A key or pad (Enter, Space, South or Start) presses the one button too, as on the menu.
  - **Fixed defects**: the gameover room placed `obj_victory` where `obj_gameover` belonged, so "Game Over" was never
    drawn; the rebuild draws it. Both objects set `fa_left` and drew from `room_width/2`, so their text started at the
    room's middle; it is centred, as the coordinate meant. `obj_victory`'s labels "Round Time", "Fastest Reaction" and
    "Other" (`fnt_oldenglish_small` outlined 2, at `room_height/4`, `/3` and `/2`) never had values: the first line
    carries the match's result ("Left takes the match 2-1") and the second the match's fastest reaction and whose it
    was; "Other" had nothing to say and is left out. The victory room had no heading at all: it takes "Victory" in Game
    Over's style. Neither room placed `obj_mouse`, so with the system cursor hidden the button had no visible pointer;
    the katana cursor is drawn over the verdict.
  - **Dropped**: `obj_virtue` x7 and `spr_virtue_*` (the virtues went with stats), and the HP bars.
- ✅ The arenas' ambient effects (UQD-T49; `AmbientFieldTests` and `AssetManifestTests` in Core, `DuelAmbientTests` in
  PlayMode, screenshots `12-ambient-*`): `setup_arena_properties.gml` created one particle emitter per arena when the
  game began, and the rebuild plays it for the arena on screen (`AmbientField` in Core, drawn by `DuelAmbient`). The
  generator reads the switch into the manifest's per-arena `ambient` ("sand", "butterfly", "snow", "fog", "fly", or
  "none"), so the list is the original's: sand in the Wastes of Nod, butterflies in Pastel Fields, snow in the Frozen
  Tundra, fog in the Fumaroles, flies in the Arid Plains, still air in the other six.
  - **Matches**: every figure of the emitters, their particles and `ai_butterfly` / `ai_fly` / `ai_firefly` (counts,
    spawn ranges, speeds, alphas, the spin of 1 degree a step, the alarms' 5 to 30 or 60 steps, the bands and the
    screen-edge turns, the butterflies' 6 flapping frames and facing, the clouds wrapping round), stepped at 60 a second
    off the clock. Every particle's step sat inside `if (gameclock.mode != "action")`: nothing moves while the duel
    waits on the first strike (the butterflies' wings stop too), though the alarms run on. Depths: sand, snow, fog and
    rain (-1) draw over the fighters and under the splatters; butterflies, flies and fireflies (0, the fighters' own)
    just behind the fighters and over the blood drops (1). They are not inverted on the cue: `obj_arena` inverted only
    what was drawn before it, at depth 100.
  - **Per match, matches** (UQD-T63): the original rolled its arena once per game, so its emitter ran for the whole
    game; the rebuild keeps the match's arena, and its effect, through every round, and a new match's arena starts its
    effect afresh as the original's did with the room (the streams start from an empty sky).
  - **Options**: the original's rain (`obj_rain_emitter` / `obj_raindrop`), leaf (`obj_leaf_emitter`) and firefly
    (`obj_firefly_emitter` / `obj_firefly`, `ai_firefly`) emitters, which no arena created, are built with their
    figures: an arena's `ambient` may name "rain", "leaf" or "firefly", and `DuelAmbient.Override` plays any of the
    eight. Their sprites ship in the manifest's `ambient` group.
  - **Fixed defects**: the emitters themselves had no action check and, stepping at the action's 1000 fps, piled up
    frozen particles off screen (2 grains of sand or 1 flake a millisecond) to pour in after the cut; they wait with their
    particles. `irandom_range(0.5, 2)` could give a snowflake no fall at all, to hang above the screen for ever; a flake
    falls 1 or 2. `obj_fly` set `image_index`, not `sprite_index`, to `choose(spr_fly, spr_fly2)`, so every fly was
    spr_fly; half are spr_fly2. `ai_fly` and `ai_firefly` turned a fly back above (`room_height/2`) but not below, so the
    swarm wandered off the bottom of the screen in time; one below its spawn band (`room_height - 40`) turns back up.
    Sand blown off the right edge was never destroyed (only `y > room_height` was); it is. `obj_leaf_emitter`, its
    sprite spr_leaf1, created raindrops (there was no `obj_leaf`); it lets spr_leaf1 and spr_leaf2 fall as `obj_snow`'s
    flakes do.
  - **Dropped**: `obj_tree_emitter` / `obj_tree1` (never placed; it put its tree at `y = room_width`), `obj_lens` (an
    additive white sheet, never placed) and `obj_spotlight` (the dropped character select's).
- ✅ The window, the clock and the debug overlay (UQD-T50; `RoundClockTests` and `DebugReadoutTests` in Core,
  `GameWindowTests`, `DuelClockTests` and `DuelDebugOverlayTests` in PlayMode; a scripted run of the Windows player at
  1600 x 700 and fullscreen).
  - **The frame, matches**: the original's window kept its 640 x 480 room's aspect at any size (`option_scale -1`,
    `option_sizeable`). Every scene now draws on that room as a 1440 x 1080 frame (2.25 panel pixels a pixel,
    `OriginalFrame`), the panel scaling so the whole frame fits the window (`DuelPanelSettings`: ScaleWithScreenSize,
    Expand) and black round it, so a wide window bars the sides and a tall one the top and bottom. The duel's stage was
    a 16:9 1920 x 1080 frame; it is the room's frame now, and the default window is 1280 x 960 (twice the room).
  - **F1, matches**: `obj_intro`, `obj_mainmenu` and `obj_game` each toggled `window_set_fullscreen` on F1; each scene
    adds `GameWindow`, which goes to the whole display (borderless) and back to the window it was. F1 no longer
    skips the intro (it is the window's key, as in the original).
  - **The clock, matches**: `obj_time` (`RoundClock`, drawn by `DuelClock`): "mm:ss" in `fnt_basic` outlined 1 at
    the top centre, the minutes ending 3 pixels left of the middle and the seconds starting 3 right of it, dropping from
    y -100 at 5 a step to y 5 and then counting the match's time; it starts over with each new match and stops when the
    verdict covers the duel (the original's `room_goto`). **Fixed defects**: it counted frames, so the action phase's
    1000 steps a second skewed it (the rebuild counts seconds off the clock, [UQD-LAW-2](#UQD-LAW-2)); a turning minute
    showed "mm:60" for a step; it slid in reading "00:0".
  - **Debug overlay, deliberate**: `globals.gml` shipped `debug = true`, so the original opened with F12's overlay on.
    The rebuild's `DuelDebugOverlay` (`DebugReadout`) draws the same lines where `obj_game` and `obj_gameclock` drew
    them (each side's `reaction_time` in ms, 9999 before a strike; the phase and its count; frames a second in
    `room_speed`'s place) but starts off, and F12 toggles it only in the editor and development builds.
  - **Icon, matches**: the player's icon is the original's `bushidocoda.ico` (its 64 x 64 frame, set by
    `PlayerIconBuilder` before every build); the product name stays QuickDuel.

- ✅ Parity scene tests and the EditMode suite (UQD-T51): `Tests/Editor` (asmdef `Unity.Rea.QuickDuel.Editor.Tests`,
  EditMode) opens each built scene and checks it, checks the build order and registration (`SceneBuilderTests`), runs the
  art and data generators on stand-in files and the catalog against the shipped manifest and files on disk
  (`OriginalAssetGeneratorTests`), and validates the settings registry against the shipped `tuning.json`
  (`SettingsRegistryValidationTests`). PlayMode `ParitySceneTests` draws the intro, menu, credits, duel and verdict
  through their panels at 1440 x 1080 and asserts a real picture (not blank or one colour); with `QUICKDUEL_SHOTS` set it
  saves `parity-<scene>.png`, committed under `docs/screenshots` as the evidence. A missing graphics device fails the
  drawn-scene tests unless `QUICKDUEL_ALLOW_NO_GRAPHICS=1`.
- ✅ Unity CI (UQD-T52): `.github/workflows/ci.yml` keeps the fast `dotnet test Core.slnx` job and adds a GameCI job that
  runs the EditMode and PlayMode suites and builds the Windows player through `QuickDuelBuild.BuildWindows` (the entry
  point `run.ps1` uses). It is gated on the `UNITY_LICENSE` (or `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD`)
  secrets and skips cleanly, not failing, when they are absent.
- ✅ A silent CLI launch (UQD-T61, `SessionAudioTests`): `play.ps1` launches muted (`MINDATTIC_CLI_MUTED=1`, `-muted`;
  `-Sound` opts out); `SessionAudio` silences the listener when the shared profile reports sound muted, and the saved
  `settings.json` is never written.
- ✅ The mode line (UQD-T62): the online host / joined line sits below the round clock, which owns the top centre, so
  the two never overprint at any window size. Local play draws no mode line (UQD-T67).

## 7. Frontier {#UQD-§7}
- ✅ The port gaps found by UQD-T19 are closed: a strike during the countdown is ignored (UQD-T22), Escape leaves the duel
  (UQD-T23; for the main menu at any moment since UQD-T65, as `obj_game`'s Escape left for character select), the victory / defeat sting plays (UQD-T24), a mouse or touch strike works for the left duelist
  (UQD-T25).
- ✅ The final play-through (UQD-T54): the muted Windows player launched, ran the intro and reached the menu, and a
  scripted PlayMode run walked every mode, a clean win, a premature strike, a round nobody strikes in and both
  verdicts, each drawn and committed under `docs/parity` beside the original's own art. **The repo is complete**
  (AGENTS.md rule 8): every [RECONSTRUCTION](RECONSTRUCTION.md) row is built or dropped on
  purpose. What remains below is optional future work, not part of the proof of concept.
- ⬜ Player profiles through `com.mindattic.unity.shared`: only the shared profile's sound-effect volume and mute (the
  session mute among them) are used (UQD-T61, UQD-T74); the tuning registry stays the game's own (UQD-T28).
- ⬜ A tuning source for Android and WebGL (`TuningStore` reads the file from disk, so it is desktop-only).
- ⬜ Online play from the main menu: Host / Join lives in the Duel scene only ([§3](#UQD-§3)).
- ⬜ The Unity CI job has not yet run on a hosted runner: it waits for the Unity license secrets (UQD-T52).
