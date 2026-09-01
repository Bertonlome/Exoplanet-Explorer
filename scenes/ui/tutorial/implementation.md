# Tutorial System — Proposed Implementation

## Status and scope

Checkpoints 0, 1, and 1.5 are accepted. The overlay/runtime prototype is accepted, and the level menu
selects and persists tutorial intent. Checkpoint 2 integrates the runtime into gameplay and implements
the first Level 1 slice; it is compile-verified and awaiting its Godot gate test.

The initial scope is a reusable tutorial system for `Level1`, `Level2`, and `Level3` that can express:

> When a condition occurs, show an instruction, focus a UI or world target, optionally suspend normal
> play, and keep the instruction active until the player performs a specified action.

The tutorial engine should remain usable for later levels and the fragment-analysis minigame without
putting level-specific tutorial logic inside `BaseLevel`, `GameUI`, or `SelectedRobotUI`.

## Codebase findings

- The project is Godot 4.3 with C# and uses `BaseLevel.tscn` as the common inherited scene for all
  gameplay levels.
- `BaseLevel` already owns the level lifecycle and creates transient interfaces such as
  `SelectedRobotUI` and `FragmentAnalysisUI`. It is the appropriate place to own one level-scoped
  tutorial director.
- `GameEvents` is an autoloaded typed signal bus. It already exposes useful semantic events such as
  `BuildingPlaced`, `RobotSelected`, `BuildingMoved`, `FragmentAnalysisRequested`, bridge/antenna
  requests, lift/drop, and resource-count changes.
- Some important tutorial actions are not on `GameEvents`. Deployment selection, exploration-mode
  changes, opening/folding panels, path painting/execution, analysis completion, and several button
  actions currently exist only as local signals or callbacks.
- Many useful controls have scene-unique names, but `BuildingSection`, `UnitSection`, and
  `SelectedRobotUI` are created dynamically. Long `NodePath` strings would therefore be brittle and
  cannot reliably identify a particular dynamically-created control.
- The mission timer is driven by `BuildingManager._Process`. There is no general pause service in the
  current project, and even the escape menu does not pause the scene tree.
- In the current edited scenes, Level 1 has a pre-placed base, Level 2 starts without one, and Level 3
  starts with a base and a monolith fragment and supplies the fragment-analysis scene. The scripts must
  respect those actual starting states: Level 1 recognizes its base, while Level 2 teaches placement.

## Recommendation

Build a small, level-scoped tutorial state machine with a typed C# builder as its authoring language.
Use `GameEvents` as one source of facts, but add a thin tutorial event adapter rather than making the
director know about every gameplay class and local signal.

The builder is recommended over a custom text parser for the first version. It reads like a script,
is checked by the compiler, supports typed event payload filters, and avoids introducing a parser and
error-reporting language before the tutorial behavior itself is stable. Its step model can later be
backed by `.tres` resources without changing the director or overlay.

Example of the intended authoring style:

```csharp
public sealed class Level1Tutorial : TutorialScript
{
    public override void Build(TutorialBuilder tutorial)
    {
        tutorial.Step("welcome")
            .When(TutorialEvent.LevelReady)
            .Say("First, deploy your landing base.")
            .PointTo(TutorialTarget.Ui(TutorialTargetId.DeployRobotsPanel))
            .HardPause()
            .UntilContinue();

        tutorial.Step("choose-base")
            .After("welcome")
            .Say("Select the base, then place it on valid terrain.")
            .PointTo(TutorialTarget.Ui(TutorialTargetId.DeployBaseButton))
            .GuideAction()
            .Until(TutorialEvent.BuildingPlaced, e => e.Building.IsBase);

        tutorial.Step("select-rover")
            .When(TutorialEvent.BuildingPlaced, e => e.Building.IsGroundRover)
            .Say("Select the rover to open its controls.")
            .PointTo(TutorialTarget.World(e => e.Building))
            .GuideAction()
            .Until(TutorialEvent.RobotSelected, e => e.Building.IsGroundRover);
    }
}
```

Names in the example illustrate the API rather than locking in the final spelling.

## Runtime design

### 1. `TutorialDirector`

A `Node` owned by the active `BaseLevel`. It:

- loads the script associated with `levelDefinitionResource.Id`;
- evaluates trigger and completion conditions;
- runs one step at a time;
- subscribes and unsubscribes through one event adapter;
- resolves targets through the target registry;
- asks the overlay to present the current step;
- applies and restores the requested pause/input policy;
- supports Skip Tutorial and optional Back/Replay controls;
- records step completion independently of whether a popup happens to be visible.

It must check whether a completion condition is already true when a step begins. Event-only waiting can
deadlock if the player completes an action before the corresponding step becomes active.

### 2. `TutorialStep` and the builder

`TutorialStep` is immutable after construction and contains:

- stable step ID;
- trigger (`When`, `After`, or immediate);
- instruction text and optional title;
- target descriptor;
- presentation mode;
- completion condition;
- optional event-payload predicate;
- optional timeout/fallback when a target cannot be resolved;
- optional flag controlling whether the step may be skipped.

Conditions should be typed implementations of a small interface rather than strings interpreted by
reflection. Initial condition types:

- event received, with optional payload predicate;
- UI target pressed/toggled;
- world target clicked;
- state predicate, evaluated on entry and after relevant events;
- Continue pressed;
- all/any composition for the occasional compound requirement.

### 3. `TutorialEventBridge`

This level-scoped adapter converts existing global and local signals into a small semantic event stream:

```text
LevelReady
BuildChoiceSelected
BuildingPlaced
RobotSelected
RobotMoved
ExplorationStarted
ExplorationStopped
FragmentAnalysisOpened
FragmentAnalysisCompleted
BridgePlaced
AntennaPlaced
RobotLifted
RobotDropped
```

Existing `GameEvents` signals should be reused where they represent a successful action. Missing events
should be emitted at the authoritative success point, not merely from a button press. For example,
`PlaceAntennaButtonPressed` means the player requested placement; a tutorial that teaches successful
placement should complete only when `BuildingPlaced` reports an antenna.

The bridge also makes current state queryable so late-starting steps can recognize that the player has
already placed a base or selected a rover.

### 4. `TutorialTargetRegistry`

Tutorial scripts refer to stable semantic IDs, not scene paths:

```text
DeployRobotsPanel
DeployBaseButton
DeployRoverButton
DeployDroneButton
DeployedUnitsPanel
SelectedRobotPanel
RandomExploreButton
AnalyseSampleButton
FragmentCanvas
```

Static and dynamically-created UI controls register themselves when they enter the tree and unregister
when freed. A registration can be a `Control`, a `Node2D`, or a callable that returns the current screen
rectangle. Callable targets are useful for a moving robot and for interfaces such as `SelectedRobotUI`
that are destroyed and recreated whenever selection changes.

If a target temporarily does not exist, the director should wait for a short configurable interval. It
must then use a text-only fallback and log a warning rather than trapping the player behind an invisible
or unclickable tutorial step.

### 5. `TutorialOverlay.tscn`

A high-layer `CanvasLayer` with `ProcessMode = Always` containing:

- four dimming/input-blocking rectangles arranged around the focus rectangle, creating a real input
  hole instead of only drawing a visual hole;
- a pulsing focus border that never consumes mouse events;
- a simple drawn arrow whose head points to the target;
- a callout `PanelContainer`, title/text labels, Continue, Back (optional), and Skip Tutorial buttons;
- safe-area placement logic that selects the side with the most room and clamps the callout to the
  viewport.

Using four blockers makes guided UI clicks pass naturally to the highlighted control while clicks
outside the focus are consumed. The focus geometry must be recomputed when the viewport resizes, when a
container relayouts, and while a world target moves.

World targets are projected to screen coordinates using the active canvas transform. They should supply
a meaningful world rectangle or radius rather than relying on a hard-coded 64-pixel box in the overlay.

## Pause and input semantics

The word "pause" needs two explicit behaviors:

### Hard pause

Use for explanatory steps completed by Continue/Skip. Set `SceneTree.Paused = true`; the director and
overlay run with `ProcessMode.Always`. Remember and restore the previous pause value, so the tutorial
does not accidentally resume a game paused by another system.

An underlying gameplay action must never be the completion condition of a hard-paused step.

### Guided action

Use for "click this button" or "perform this move" steps. Do not pause the entire scene tree, because
the target's gameplay handler must run. Instead:

- the four overlay blockers reject pointer input outside the focus;
- unrelated keyboard/world commands are rejected by a small tutorial input policy;
- the world camera is disabled unless the current step explicitly permits it;
- the level clock can be suspended independently so reading the tutorial does not consume mission time.

The first version should add an explicit clock-suspension flag to `BaseLevel`/`BuildingManager`. It
should not use `Engine.TimeScale`, which would affect tweens, timers, audio, and other systems globally.
If a later tutorial must freeze autonomous robot movement during a guided action, add that as a separate
policy rather than silently equating it with clock suspension.

## Proposed files

```text
scenes/ui/tutorial/
  implementation.md                 (this proposal)
  TutorialOverlay.tscn
  TutorialOverlay.cs
  TutorialDirector.cs
  TutorialModels.cs                 (step, enums, event context)
  TutorialBuilder.cs
  TutorialEventBridge.cs
  TutorialTargetRegistry.cs
  TutorialInputPolicy.cs
  TutorialCatalog.cs
  scripts/
    Level1Tutorial.cs
    Level2Tutorial.cs
    Level3Tutorial.cs
```

Keep the engine files small and level-neutral. Put wording, target choices, and sequencing only in the
three files under `scripts/`.

## Integration points

1. Export or instantiate `TutorialDirector` from `BaseLevel.tscn` and initialize it after `GameUI`,
   `BuildingManager`, and `GridManager` are ready.
2. Expose `LevelDefinitionResource.Id` to the director or let `BaseLevel` pass it explicitly.
3. Add only the missing semantic success signals needed by the first three scripts. Continue using the
   existing typed `GameEvents` methods and payloads where possible.
4. Register static targets in `GameUI`; register resource-specific `BuildingSection` targets after
   `SetBuildingResource`; register selected-rover actions in `SelectedRobotUI`; register analysis targets
   in `FragmentAnalysisUI` only when Level 3 needs them.
5. Ensure tutorial signal connections are disconnected in `_ExitTree` because selected-robot and
   analysis interfaces have short lifetimes.
6. Add Skip Tutorial. Persisting "do not show again" is a separate choice because `SaveData` currently
   stores only level completion. For the first vertical slice, restarting a level can restart its
   tutorial; persistence can be added after the behavior is accepted.

## Provisional content for the first three levels

Exact copy and number of steps should be tuned in Godot after the engine is working.

### Level 1 — rover movement

- deploy a rover
- Explain the mission and the time/resources panels.
- Recognize the pre-placed base rather than repeating base placement.
- focus on the rover movement and capacity to create bridges
- gather wood, stock wood to the base, understand that it can be used to charge the robots when it's in base, or be used to construct bridges when it's in robot.
- start and stop autonomous exploration;
- use trace/anomaly information;

### Level 2 — deployment basics

- Open the deployment section.
- select and place the base;
- deploy a rover
- select a rover and explore the beach
- Gather 3 minerals, bring them back to base and transform them into material to construct a drone
- perform one directed move or start an exploration mode with the drone to scout the map;
- explain anomaly readings and the monolith objective.
- compare rover and drone capabilities;
- use the drone to lift the rover to get the rover to the monolith up a hill


### Level 3 — fragment workflow

- Move a ground rover into sample range;
- introduce battery, communication coverage, and returning to base.
- select the rover and identify sample availability;
- press Analyse Sample;
- introduce the fragment-analysis interface in short staged instructions;
- complete or exit analysis and use its directional result in the world.

## Implementation checkpoints

### Checkpoint 0 — overlay prototype

- Implement the overlay with a mock static `Control` target.
- Verify callout positioning at 1920x1080 and one smaller window size.
- Verify Continue and Skip while the scene tree is hard-paused.

**Implementation status — 2026-09-01:** Accepted after one correction pass. The initial failure and
accepted retest are retained below as the implementation record.

**Implementation summary:**

- Added `TutorialOverlay.tscn` and `TutorialOverlay.cs` as the reusable presentation layer.
- Added four independently-sized dimming blockers. Together they leave a real hole around the target
  while consuming pointer input everywhere else.
- Added a pulsing cyan focus border, line/arrowhead, sci-fi themed callout, wrapped title/body copy,
  Continue, and Skip Tutorial controls.
- Added responsive placement that chooses the side of the target with the most available space and
  clamps the callout to the viewport. Layout refreshes after container layout and on viewport resize.
- Added `TutorialOverlayMode` and a public overlay API without putting pause ownership in the view. This
  preserves the planned separation between hard pause and guided action behavior.
- Added `TutorialOverlayPrototype.tscn` and `TutorialOverlayPrototype.cs`. This standalone harness
  highlights a mock gameplay button, hard-pauses the scene tree, verifies Continue, then verifies Skip
  and restores the exact pause state that existed before the prototype opened.
- The overlay uses canvas layer 9 so it appears over normal game UI while the existing custom cursor on
  layer 10 remains visible.
- No gameplay scene or event bus was modified. In particular, the current user edits to `Level1.tscn`
  and `Level2.tscn` were preserved.

**Automatic verification:** `dotnet build --no-restore` passes with 0 errors. The three warnings are
the same pre-existing nullable-annotation warnings in `BuildingComponent.cs` and unused-variable warning
in `SaveManager.cs`. `git diff --check` passes for the tutorial folder. A Godot executable is not
available in this shell, so scene loading and appearance require the following editor test.

**Godot game test:**

1. Open `res://scenes/ui/tutorial/TutorialOverlayPrototype.tscn` and use **Run Current Scene** (`F6`).
2. At 1920x1080, confirm the mock panel is visible through the dimmed screen, with a pulsing border and
   arrow pointing at its button. The callout must remain fully on-screen and must not cover the target.
3. Click the dimmed area outside the target/callout. Nothing behind the overlay should respond.
4. Press **Continue**. The copy should change to `CONTINUE PASSED` even though the scene tree is paused.
5. Press **Skip Tutorial**. The overlay should disappear and the status should say gameplay resumed.
6. Click **Mock Gameplay Target**. The status should confirm that the post-overlay click was received.
7. Repeat after resizing to a smaller supported size, suggested 1280x720. The callout, arrow, target
   opening, buttons, and text must stay on-screen without overlap that prevents interaction.

**Initial Checkpoint 0 feedback:**

- **Result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED`
- **Focus hole and pulsing border:** `[ ] the border is visible around mock gameplay target but not pulsing`
- **Arrow position/direction:** `[ ] it should point to the highlighted border`
- **Callout position and text readability:** `[x]`
- **Outside clicks blocked:** `[ ] mouse pointer invisible`
- **Continue worked during hard pause:** `[ ] not tested`
- **Skip restored gameplay:** `[ ] mouse pointer invisible`
- **Mock target worked after resume:** `[ ] idem`
- **Custom cursor remained visible and aligned:** `[ ] no`
- **Approved to implement Checkpoint 1:** `[ ] YES  [x] NO`

**Correction summary — 2026-09-01:**

- The global custom cursor now uses `ProcessMode.Always`, so its sprite continues following the mouse
  while a tutorial hard-pauses the scene tree. This is a global UI correction and also benefits future
  genuinely-paused menus.
- The focus-border pulse now ranges from 35% to 100% opacity, runs slightly faster, and uses a six-pixel
  border. The initial 72% to 100% range was too subtle during the game test.
- The arrow now computes the nearest point on the highlighted rectangle from the callout and places the
  arrowhead exactly on that border. Its line ends behind the arrowhead rather than extending into the
  highlighted control.
- The callout layout, blocker geometry, hard-pause flow, and user-edited level scenes were not changed.

**Checkpoint 0 retest feedback (fill after testing):**

- **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
- **Focus hole visible and border clearly pulsing:** `[x]`
- **Arrowhead touches the highlighted border:** `[x]`
- **Callout position and text readability:** `[x]`
- **Custom cursor visible, moving, and aligned during pause:** `[x]`
- **Outside clicks blocked:** `[x]`
- **Continue changed the popup during hard pause:** `[x]`
- **Skip closed the overlay and restored gameplay:** `[x]`
- **Mock target worked after resume:** `[x]`
- **Approved to implement Checkpoint 1:** `[x] YES  [ ] NO`

### Checkpoint 1 — director and authoring API

- Implement models, builder, director, event bridge, and target registry.
- Run a three-step test sequence: Continue, target click, event completion.
- Verify missing-target fallback and cleanup on scene change.

**Implementation status — 2026-09-01:** Implemented, compile-verified, and accepted by the user.

**Implementation summary:**

- Added immutable tutorial models for steps, triggers, completion conditions, event context, overlay
  mode, stable IDs, missing-target timeouts, and skippability.
- Added `TutorialBuilder`, `TutorialStepBuilder`, and `TutorialScript`. Scripts support `When`, `After`,
  `Say`, `PointTo`, `HardPause`, `GuideAction`, `UntilContinue`, `UntilTargetPressed`, semantic-event
  completion, state-predicate completion, and configurable target fallback timeouts.
- The builder rejects duplicate IDs, a target-click condition without a target, and `After` references
  that do not point to an earlier step. This catches broken scripts before the tutorial starts.
- Added `TutorialEventBridge` as an explicitly started/stopped, level-scoped adapter. It retains the
  latest semantic event so a step recognizes actions completed before it becomes active.
- The bridge currently adapts successful building placement, robot selection/movement, fragment-analysis
  requests, and lift/drop requests. Request events deliberately remain distinct from confirmed success
  events, preventing later tutorials from advancing when gameplay rejects an action.
- Added `TutorialTargetRegistry` with disposable registrations for controls or callable screen-rectangle
  providers. Invalid/freed owners are discarded, and a newer registration can replace a transient UI
  target using the same semantic ID.
- Added `TutorialDirector`, which runs sequential scripts, waits for triggers, updates moving target
  rectangles, subscribes only to the active target, preserves/restores prior pause state, detects event
  or state conditions already satisfied, supports Skip, and disconnects on stop/scene exit.
- A missing target is given a short registration window. It then becomes a logged text-only fallback.
  Guided-action fallback passes input through the dimmer; target-click fallback exposes Continue. These
  paths prevent an unavailable dynamic control from trapping the player.
- Updated `TutorialOverlayPrototype` into a three-step runtime test: hard-paused Continue, guided target
  click, then semantic-event completion after deliberately exercising missing-target fallback.
- No production gameplay level, `BaseLevel`, `GameUI`, or `SaveData` integration was added in this gate.

**Automatic verification:** `dotnet build --no-restore` passes with 0 errors and the same three
pre-existing warnings. `git diff --check` passes for the tutorial and cursor changes. Godot is not
available in this shell, so runtime behavior requires the test below.

**Godot game test:**

1. Run `res://scenes/ui/tutorial/TutorialOverlayPrototype.tscn` with **Run Current Scene** (`F6`).
2. **Step 1:** confirm the popup says `STEP 1 — CONTINUE`, the tree is hard-paused, outside clicks do
   nothing, and Continue advances to Step 2.
3. **Step 2:** confirm the arrow/focus surrounds **Mock Gameplay Target**. Click **Event Source** first;
   it must be blocked because it is outside the focus hole. Click **Mock Gameplay Target**; Step 3 must
   begin immediately.
4. **Step 3:** briefly observe `Locating the highlighted control…`. After about 0.8 seconds it must
   change to a text-only fallback with no arrow/focus. The debugger should contain one expected warning
   naming `prototype.missing-target`.
5. Click **Event Source**. It must now receive input through the dimmer, publish the semantic event,
   close the overlay, and leave the status `CHECKPOINT 1 PASSED`.
6. Run the scene again and press **Skip Tutorial** during Step 1. Confirm the overlay closes, the cursor
   remains usable, and the status reports cleanup. Stop and rerun the scene once more; it must start at
   Step 1 with no duplicate callbacks, stale highlight, or retained pause.
7. Repeat the main three-step flow at one smaller supported resolution, suggested 1280x720.

**Checkpoint 1 feedback (fill after testing):**

- **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
- **Approved to implement Checkpoint 1.5:** `[x] YES  [ ] NO`

### Checkpoint 1.5 - Update the level selection menu to showcase tutorial level vs. regular level

- Change the name to Level # (tutorial)
- Use the SaveData to prompt the player to perform the tutorial, or skip it and play the regular menu if it has been detected that this level has already been started in tutorial mode

**Implementation status — 2026-09-01:** The initial game test failed on card sizing and dialog button
order. A focused correction is implemented and compile-verified; awaiting the checkpoint 1.5 retest
gate below. Checkpoint 2 has not been started.

**Resolved interaction:** The first three level IDs are tutorial-capable. Before a tutorial has ever
been started, its card says `Level # (Tutorial)`, reports `Tutorial available`, and starts tutorial mode
directly. That choice is saved immediately. On later selections, the card reports
`Tutorial previously started` and opens a three-way choice: replay the tutorial, play without tutorial,
or cancel. Levels 4–6 retain the regular one-click selection flow.

**Implementation summary:**

- Added `TutorialCatalog` with the stable IDs of Levels 1–3. This avoids coupling tutorial availability
  to the current array order and provides the future location for level-script lookup.
- Extended `SaveData` with `TutorialStartedLevelIds`, plus initialization guards so save files written
  before this field existed remain valid.
- Added `SaveManager.HasTutorialStarted` and `MarkTutorialStarted`. The latter writes only when a new ID
  is added, avoiding redundant disk writes when replaying a tutorial.
- Extended `LevelManager.ChangeToLevel` with a tutorial-mode argument and exposed
  `IsTutorialModeActive`. Every load prints the level ID and selected `tutorial` or `regular` mode for
  this checkpoint's verification. Non-level scene changes and next-level transitions do not propagate
  tutorial mode accidentally.
- Tutorial-capable level cards now show `(Tutorial)`, their saved tutorial state, and either
  `Start tutorial` or `Choose mode`. Non-tutorial cards are unchanged.
- Added a confirmation dialog for previously-started tutorials with **Play Tutorial**,
  **Play Without Tutorial**, and **Cancel**. The dialog switches to the native cursor while its Window
  is open and always restores the custom cursor before loading or closing.
- The current user edits to the Level 1 and Level 2 scenes were not modified.

**Automatic verification:** `dotnet build --no-restore` passes with 0 errors and the same three
pre-existing warnings. `git diff --check` passes for the changed source, scene, and documentation files.
Old save compatibility is implemented defensively but still needs the restart test below.

**Godot game test:**

1. Start the game and open **Play**. Confirm Levels 1–3 are labeled `Level # (Tutorial)` and Levels 4–6
   are not. A tutorial not yet selected should show `Tutorial available` and `Start tutorial`.
2. Choose an unstarted tutorial level. It should load immediately without a dialog. The Output panel
   must say it loaded in `tutorial mode`. Gameplay itself remains unchanged until Checkpoint 2.
3. Return to the main menu and open **Play** again. The same card should now show
   `Tutorial previously started` and `Choose mode`, proving the state was written.
4. Select that card and press **Cancel** in the dialog. Confirm the dialog closes, the custom cursor is
   restored, and no level loads.
5. Open it again and choose **Play Without Tutorial**. Confirm the level loads and Output says
   `regular mode`.
6. Return again, choose **Play Tutorial**, and confirm Output says `tutorial mode`.
7. Select a non-tutorial level (Level 4–6). It must load directly in regular mode without showing the
   tutorial dialog.
8. Restart the whole game and revisit **Play**. Previously-started tutorial cards must retain their
   saved status, and an older existing save must load without errors.

**Checkpoint 1.5 feedback (fill after testing):**

- **Result:** `[ ] PASS  [x] FAIL  - REASON : see screenshot the box is way too large, and move "CANCEL" button to the rightmost`
- **Approved to implement Checkpoint 2:** `[x] YES  [x] NO`

**Correction summary — 2026-09-01:**

- Split `Level # (Tutorial)` over two centered lines and reduced the heading from 48 px to 36 px so
  the longer tutorial label no longer determines an oversized card width.
- Set each level card to a compact 300 px minimum width and made the level grid responsive: four
  columns at 1500 px and wider, three at 1050–1499 px, two at 720–1049 px, and one below 720 px.
  The grid recalculates when the game window is resized.
- Explicitly ordered the replay dialog actions as **Play Tutorial**, **Play Without Tutorial**, then
  **Cancel**, making **Cancel** the rightmost action independently of the default dialog insertion
  order.
- Recompiled with `dotnet build --no-restore --no-incremental`: 0 errors and the same three
  pre-existing warnings. `git diff --check` also passes.

**Checkpoint 1.5 focused retest:**

1. Open **Play** at the resolution used for the failed screenshot. Confirm all four cards in the first
   row fit inside the panel without clipping either edge and are substantially narrower than before.
2. Confirm Levels 1–3 display a centered two-line `Level #` / `(Tutorial)` heading and that the rest
   of each card remains readable.
3. Resize to approximately 1280x720. Confirm the grid changes to three columns, remains centered, and
   does not clip cards. If convenient, narrow it further and verify the two-column layout.
4. Select a previously-started tutorial. Confirm the buttons read left-to-right: **Play Tutorial**,
   **Play Without Tutorial**, **Cancel**.
5. Press **Cancel** and confirm the dialog closes, no level loads, and the custom cursor returns.

**Checkpoint 1.5 retest feedback (fill after testing):**

- **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
- **Card sizing/layout comments:**
- **Dialog button-order comments:**
- **Approved to implement Checkpoint 2:** `[x] YES  [ ] NO`

## In-level content coverage contract

The bullets on lines 270–298 are the authoritative content contract. A level is not complete merely
because its first interaction works: every source line below must be implemented and pass its own gate.
The earlier broad checkpoints missed several bullets and incorrectly asked Level 1 to place a base.
They are replaced by these level-specific checkpoints:

| Checkpoint | Level | Source lines | Required content |
| --- | --- | --- | --- |
| 2 | Level 1A | 270–272 | Deploy a rover; explain mission/time/resources; recognize the pre-placed base. |
| 3 | Level 1B | 273 | Direct rover movement and bridge capability. |
| 4 | Level 1C | 274 | Gather wood, return it to base, explain base recharge, retain wood on rover, construct a bridge. |
| 5 | Level 1D | 275–276 | Start/stop autonomous exploration; use trace and anomaly information. |
| 6 | Level 2A | 280–282 | Open deployment, select/place the base, then deploy a rover. |
| 7 | Level 2B | 283–284 | Select/explore with rover; gather three minerals, return them, convert them, deploy a drone. |
| 8 | Level 2C | 285–288 | Direct/exploratory drone move, anomaly/monolith objective, capability comparison, lift rover to monolith. |
| 9 | Level 3A | 293–294 | Move rover into sample range; battery, communication coverage, and return-to-base concepts. |
| 10 | Level 3B | 295–296 | Select rover, identify sample availability, press Analyse Sample. |
| 11 | Level 3C | 297 | Staged fragment-analysis interface guidance. |
| 12 | Level 3D | 298 | Complete or exit analysis and use the directional result in the world. |
| 13 | All | — | Copy/accessibility/audio polish and the remaining persistence decision. |

### Checkpoint 2 — Level 1A: orientation and rover deployment

**Coverage:** Lines 270–272. Later Level 1 instructions remain gated behind Checkpoints 3–5.

**Implementation status — 2026-09-01:** The initial Godot test failed because the pre-placed base was
rendered behind terrain and the rover-placement presentation obscured the map. A focused correction is
implemented and compile-verified; awaiting the retest gate below. Checkpoint 3 has not been started.

**Implementation summary:**

- Integrated the existing `TutorialDirector`, event bridge, target registry, and overlay into
  `BaseLevel.tscn`. They remain dormant in regular mode and start only when
  `LevelManager.IsTutorialModeActive` is true.
- Added `Level1Tutorial.cs` as the first real typed tutorial script. Its six steps cover the mission,
  time/resources HUD, the existing base, selecting the Rover card, successfully placing a Rover, and
  a completion message.
- Corrected the obsolete base-placement plan: Level 1 now detects its pre-placed base, refreshes the
  deployment UI before the tutorial starts, points at that base in world space, and never asks the
  player to place another one.
- Registered stable targets for the status panel, deployment panel, Base/Rover/Drone buttons, and the
  pre-placed base. Dynamic deployment-button registrations are rebuilt whenever the available cards
  change and disposed during cleanup.
- Rover deployment advances on the semantic `BuildingPlaced` event filtered to a real Rover. Selecting
  the card or attempting an invalid placement cannot satisfy that step.
- Added level-scoped cleanup for the director, event bridge, and target registrations. Level 2 and
  Level 3 remain listed as tutorial-capable but intentionally warn that their scripts are not yet
  implemented if launched before their checkpoints.

**Automatic verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors and the
same three pre-existing warnings. `git diff --check` passes. A Godot executable is unavailable in this
shell, so scene parsing and visual/input behavior require the editor test.

**Checkpoint 2 Godot game test:**

1. From **Play**, start Level 1 in tutorial mode. Confirm the first popup is **Level 1: Rover Movement**
   and explains the exploration mission and that the base is already deployed.
2. Continue to **Mission Status**. Confirm the left HUD is highlighted and the text explains time,
   materials, and returned wood. Continue must work while gameplay remains paused.
3. Continue to **Base Already Online**. Confirm the focus box tracks the visible base in the world and
   no step asks you to select or place a base.
4. Continue to **Deploy a Rover**. Confirm the Rover card's **Select** button is highlighted. Clicking
   elsewhere must be blocked; clicking the highlighted button must enter rover-placement mode and
   advance exactly once.
5. During **Place the Rover**, confirm world input is available. Try one invalid placement: the tutorial
   must remain on this step. Then place a Rover on a valid tile near the base; only the successful
   placement must advance to **Rover Ready**.
6. Continue from **Rover Ready**. Confirm the overlay disappears, the level remains playable, and no
   duplicate callback or warning appears in Output.
7. Restart Level 1 in tutorial mode and use **Skip Tutorial** from an early step. Confirm pause, overlay,
   targets, and input are cleaned up. Also open Level 1 without tutorial and confirm no overlay appears.

**Checkpoint 2 feedback (fill after testing):**

- **Result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED`
- **Mission/HUD/base comments:** The focus box located the pre-placed base area, but the base itself was
  invisible before and after rover deployment.
- **Rover selection/placement comments:** Remove the self-evident “Only the highlighted control…” copy.
  The centered, dimmed placement step made the world too difficult to see and use; place the callout at
  the top-right and do not dim this action step.
- **Skip/regular-mode cleanup comments:**
- **Approved to implement Checkpoint 3:** `[ ] YES  [x] NO`

**Checkpoint 2 correction summary — 2026-09-01:**

- Diagnosed the invisible base as a draw-order regression, not failed deferred placement. Adding four
  tutorial children before the original gameplay children shifted Level 1's inherited `Base` at child
  index 8 ahead of `YSortRoot`, allowing terrain to render over it. The base still existed—explaining
  why rover placement near it succeeded.
- Moved the tutorial registry, bridge, director, and overlay declarations to the end of `BaseLevel.tscn`.
  This preserves the original indices/order of gameplay canvas children, so the pre-placed Base once
  again renders after the terrain without modifying the user's Level 1 scene.
- Removed “Only the highlighted control is active while this instruction is visible.” from the Rover
  selection copy.
- Extended the typed tutorial step model with reusable `UndimBackground()` and
  `PlaceCallout(TutorialCalloutPlacement.TopRight)` options. The **Place the Rover** step uses both:
  blockers disappear, world input remains available, and the callout is anchored at the viewport's
  top-right with its normal safe-area margin.
- Moved **Skip Tutorial** into the callout header's top-right corner and styled it as a flat red
  `SKIP X` close action with a `Skip tutorial` tooltip. The lower action row now contains only
  contextual actions such as
  **Continue**.
- Recompiled with `dotnet build --no-restore --no-incremental`: 0 errors and the same three pre-existing
  warnings. `git diff --check` passes.

**Checkpoint 2 focused retest:**

1. Start Level 1 in tutorial mode. Before advancing, confirm the pre-placed Base is visibly rendered on
   its grass platform.
2. On **Base Already Online**, confirm the cyan focus box surrounds the visible Base—not empty terrain—
   and the callout remains readable.
3. On **Deploy a Rover**, confirm the copy now ends after “Select the Rover deployment card.”
4. Click the highlighted Rover button. On **Place the Rover**, confirm the background is completely
   undimmed, the callout is fixed at the top-right, and the rover preview and valid placement tiles are
   easy to see.
5. Make one invalid placement, then a valid placement. Confirm only the valid placement advances to
   **Rover Ready**, and both the Base and Rover remain visible afterward.
6. On multiple steps, confirm the red **Skip X** button stays at the panel's top-right and
   still closes the tutorial and restores normal input.

**Checkpoint 2 retest feedback (fill after testing):**

- **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
- **Base visibility/focus comments:**
- **Undimmed top-right placement comments:**
- **Top-right Skip Tutorial button comments:**
- **Approved to implement Checkpoint 3:** `[ ] YES  [ ] NO`

### Checkpoint 3 — Level 1B: movement and bridges

- Implement every action in source line 273: select the rover, perform direct movement, then introduce
  that ground rovers can create bridges when a route crosses water.
- Require successful movement rather than button intent before advancing.

### Checkpoint 4 — Level 1C: wood lifecycle

- Implement every action and explanation in source line 274: gather wood, carry it to base, explain/use
  wood stored at base for charging, retain wood on the rover, and successfully construct a bridge.

### Checkpoint 5 — Level 1D: autonomy, trace, and anomaly

- Implement source lines 275–276: start autonomous exploration, stop it, enable/use trace information,
  and interpret anomaly information. Completing this checkpoint completes the Level 1 content contract.

### Checkpoint 6 — Level 2A: base and rover deployment

- Implement source lines 280–282: open deployment, select and successfully place the base, then deploy
  a rover. Unlike Level 1, Level 2 must actually teach base placement.

### Checkpoint 7 — Level 2B: rover exploration to drone construction

- Implement source lines 283–284: select the rover, explore the beach, gather three minerals, return
  all three to base, transform them into material, and use that material to deploy a drone.

### Checkpoint 8 — Level 2C: drone capabilities and monolith access

- Implement source lines 285–288: directed or autonomous drone scouting, anomaly readings and the
  monolith goal, rover/drone capability comparison, and lifting the rover to the hilltop monolith.

### Checkpoint 9 — Level 3A: reach a sample safely

- Implement source lines 293–294: move a ground rover into sample range and introduce battery,
  communication coverage, and returning to base.

### Checkpoint 10 — Level 3B: request analysis

- Implement source lines 295–296: select the correct rover, identify available samples, and press
  **Analyse Sample**. Failed or stale requests must not advance the tutorial.

### Checkpoint 11 — Level 3C: staged fragment analysis

- Implement source line 297 with short, target-aware stages for the transient fragment-analysis UI.
- Verify every transient target unregisters when the interface closes or is replaced.

### Checkpoint 12 — Level 3D: result back to the world

- Implement source line 298: complete or exit analysis, return to world view, and use its directional
  result. Completing this checkpoint completes the Level 3 content contract.

### Checkpoint 13 — polish and optional persistence

- Final copy, focus margins, accessibility/contrast, and audio across all three accepted level scripts.
- Decide whether Skip/completion progress is per run, per level, or permanently persisted.

At every checkpoint, run `dotnet build --no-restore`. Visual/input behavior still needs an in-editor
Godot test because the repository has no automated UI test setup.

## Alternatives considered

### Put every tutorial signal and state directly in `GameEvents`

Simple initially, but it turns the global gameplay event bus into a mirror of every UI callback and
makes tutorials depend on implementation details. Recommended compromise: keep durable gameplay facts
in `GameEvents`, and adapt local/transient signals through `TutorialEventBridge`.

### Author every sequence as `.tres` resources

This gives inspector editing and copy changes without recompilation, but polymorphic triggers,
predicates, and dynamic targets become verbose subresources. It is a good second authoring frontend once
the step model has stabilized, not the fastest first implementation.

### Parse JSON/YAML or invent a custom tutorial language

Readable data files are attractive, but a parser, validation, payload filter syntax, diagnostics, and
Godot-object targeting would add substantial infrastructure. The typed builder provides the same
script-like flow with much less failure surface.

### Use only `SceneTree.Paused`

Appropriate for Continue popups, but incompatible with "wait until the player performs an underlying
game action." Separating hard pause from guided action avoids special-case process modes throughout the
game.

## Decisions remaining for later checkpoints

1. Should tutorial progress restart whenever a level restarts, or should Skip/completion be persisted?
2. During guided-action steps, should autonomous robots continue moving, stop temporarily, or depend on
   a per-step flag? The recommendation is a per-step flag with "temporarily stop" as the early-level
   default.
3. Should the initial tutorial copy be English only? The current game contains English UI and a French
   rules document but no localization system.
