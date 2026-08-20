# Fragment Analysis Rover Autonomy — Implementation Specification

> **Planning annotation status — 2026-08-14:** This workspace copy preserves the attached
> specification and adds implementation notes inline. No gameplay implementation is part of
> this document-only pass. Each numbered capacity below is a gated vertical slice: we implement
> that capacity, run the available build/algorithm checks, you perform the stated Godot test and
> fill in its result, and only then do we move to the next capacity.

## How to use this implementation plan

Annotations use the following labels:

* **Automatic:** I can create or edit the C# files, `.tscn` scene structure, resources, node
  lookups, and signal wiring directly. You do not need to recreate those edits in the Godot UI.
* **Godot check:** no editor setup is required unless an annotation explicitly says otherwise;
  this is the runtime or visual check only you can perform in the installed Godot editor.
* **Decision:** an option that changes behavior or scope. The recommended option is the working
  default, but implementation pauses at the affected checkpoint until you fill in the answer.
* **Gate test:** the acceptance record for one capacity. Fill `Result`, `Observed`, and any notes
  after testing. `PASS` authorizes the next checkpoint; `FAIL` keeps work on the same checkpoint.

The repository currently has no automated test project and no Godot executable available from
this development shell. I can run `dotnet build` here. For runtime behavior and layout, you will
launch a level in Godot and fill the gate statement. If we choose a test add-on, that setup is
called out explicitly below.

Planning baseline: `dotnet build --no-restore` passes on 2026-08-14 with zero errors and three
pre-existing warnings (two nullable-annotation warnings in `BuildingComponent.cs` and one unused
variable warning in `SaveManager.cs`). This is the compile baseline for checkpoint comparisons.

## Foundation checkpoint 0 — shared autonomy contract

**Implementation status — 2026-08-14:** Implemented and compile-verified; awaiting the user gate
test below. Capability 1.1 has not been started.

This checkpoint is implemented before capacity 1.1 because every later slice needs the same
mode, state, observation, command, overlay, and override contracts.

**Proposed code and scene shape**

* Add `FragmentAutonomyMode` (`Off`, `Supporter`, `Performer`) and typed records for control state,
  visible scan primitives, features, regions, structures, metrics, history entries, orientation
  hypotheses, arrow candidates, and Rover action/status text.
* Add `FragmentAnalysisRover` as the coordinator. It receives a sanitized observable scan snapshot,
  a source-aware control adapter, and—per resolved decision F-02—a private immutable truth-oracle
  snapshot. It is never passed the mutable `FragmentPuzzle` itself.
* Add `FragmentRoverOverlay : Control` as a sibling drawn above `FragmentCanvas`. Annotations are
  therefore editable data and a separate visual layer, never pixels baked into the puzzle.
* Add `FragmentRoverSettings : Resource` for thresholds, confidence cutoffs, navigation speed,
  preview duration, history limits, and overlay colours. Attach its subresource in
  `FragmentAnalysisUI.tscn`.
* Route both player and Rover changes through one command path labelled with an action source.
  This is what makes manual override, history, undo, UI synchronization, and transparency
  consistent instead of adding special cases to every control.
* Extend `FragmentAnalysisState` with Rover state that is safe to resume (mode if selected,
  annotations, inspection history, locks, tested configurations, accepted hypotheses, pause
  state). Do not persist an in-flight tween or timer; restore it paused.
* Add a compact autonomy panel containing the three-way mode selector, current/next action,
  target, result, locks, pause, history navigation, and context-sensitive accept/reject controls.

**Primary checkpoint entry points:** `FragmentAnalysisRover.Initialize(...)`,
`FragmentAnalysisRover.SetMode(...)`, and `FragmentAnalysisUI.DispatchAnalysisCommand(...)`.
Small data-only constructors/helpers are completed within this foundation gate; later capability
logic remains stubbed or absent until its own gate.

**Automatic:** all new scripts, the overlay/autonomy-panel nodes, radio-button grouping, exported
settings, signal connections, save/restore changes, and compile checks. The current scene connects
signals in C#, so no manual Node-dock signal wiring is necessary.

**Godot check:** open Fragment Analysis at the smallest and largest resolutions you support;
confirm the original manual controls still work in `OFF`, the mode selector is readable, and
closing/reopening restores the agreed mode/state. Layout tuning after your screenshot or notes is
also part of this checkpoint.

**Decision F-01 — allocation granularity:**

* **A (recommended):** one global `OFF / SUPPORTER / PERFORMER` selection for the whole analysis,
  with the capability table still preventing forbidden performer actions.
* **B:** one mode per numbered capacity (more experimental control, much larger UI/state surface).
* **C:** a global default plus per-capability overrides in an advanced panel.
* **Answer:** `[ F-01: C ]`

**Decision F-02 — hidden solution boundary:** the opening paragraph says the Rover “may directly
read” the hidden solution, while later requirements explicitly forbid deriving filters, rotation,
and arrow detection from it. Which rule should govern production behavior?

* **A (recommended):** the Rover never reads `Correct*`, line roles, true glyph, correct rotation,
  or monolith direction. It works only from the same currently rendered primitives and outcomes a
  player could observe; an oracle may exist only in deterministic tests.
* **B:** allow a disabled-by-default debug/Wizard-of-Oz oracle, visibly labelled when enabled.
* **C:** allow hidden truth for the green performer capacities except where a section explicitly
  forbids it.
* **Answer:** `[ F-02: I think for easier implementation you could have an autonomy module that knows the hidden truth, but act as if it does not. For yellow you have a per-task reliability parameter default .5 that can be tuned]`

**Resolved implementation interpretation:** the coordinator receives a private read-only
`FragmentAutonomyTruth` snapshot, while player-facing evidence and action descriptions continue to
behave as though they were derived from observation. Each task has a tunable Yellow reliability,
default `0.5`; Green is reliable, Orange remains support/approval-dependent, and Red cannot perform.
This decision supersedes later statements that prohibit all hidden-truth access, but not statements
that prohibit exposing the answer or bypassing the normal puzzle controls.

**Decision F-03 — automated-test approach:**

* **A (recommended):** keep dependencies unchanged; add deterministic, callable C# test-harness
  methods/scenes for algorithms, run `dotnet build` here, and use the manual Godot gates below.
* **B:** add GdUnit4 through Godot AssetLib and build a conventional test suite. This requires you
  to install/enable the add-on because Godot is not available in this shell.
* **C:** compile plus manual tests only.
* **Answer:** `[ F-03: C, I have never used actual tests in 8 years of programming so I don't want to complicate things now.]`

**Decision F-04 — persistence scope:**

* **A (recommended):** match current behavior: persist per fragment for the lifetime of the
  `BaseLevel` instance, but do not add global save-game serialization in this feature.
* **B:** also serialize all Rover analysis state through the global save system now.
* **Answer:** `[ F-04: A]`

**Foundation gate test (fill after implementation)**

* **Expected:** existing manual analysis is unchanged in `OFF`; `SUPPORTER` and `PERFORMER` can be
  selected; the empty Rover overlay/panel is visually aligned; manual actions are source-labelled;
  closing and reopening follows F-04; there are no C# build errors.
* **Result:** `[x] PASS  [ ] FAIL`
* **Observed:** `[Checkpoint 0 accepted by the user; test reported OK.]`
* **Tested build/scene/resolution:** `[Godot runtime test completed; exact scene/resolution not recorded.]`
* **Notes or screenshot path:** `[No checkpoint-0 issue reported.]`
* **Implementation build:** `PASS — dotnet build --no-restore, 0 errors; 3 pre-existing warnings`
* **Approved to implement 1.1:** `[x] YES  [ ] NO`

## Purpose

Extend the existing **Fragment Analysis** minigame with a Rover autonomy module that can either:

* **support the player while the player performs the analysis**, or
* **perform parts of the analysis itself while the player supervises or supports it**.

The implementation must preserve the current manual analysis system. Rover autonomy should operate through the same puzzle state, visible scan information, processing controls, navigation controls, reconstructed structures, and orientation state available to the player.

The Rover may directly read the hidden puzzle solution (like a wizard of Oz) to determine the correct filters, channels, glyph, rotation, or monolith direction. But , its behavior should be derived from observable scan features, processing outcomes, geometric analysis, search history, and explicit player input.

---

# Capability model

The capability ratings used below are:

* **Green:** can perform reliably without assistance.
* **Yellow:** can perform, but not with complete reliability.
* **Orange:** requires support to perform effectively.
* **Red:** cannot perform the capacity.

For each capacity, distinguish between:

* **Player performer:** the player carries out the task and the Rover may support.
* **Rover performer:** the Rover carries out the task and the player may supervise, redirect, or support.
* **Observability:** information that must be visible for coordination.
* **Predictability:** Rover intent or future action that must be understandable before it happens.
* **Directability:** actions the player must be able to select, constrain, correct, reject, or override.

---

# 1. Establish fragment sample

## 1.1 Sense sample availability

### Player capacity

The player has **orange** capacity. The player cannot reliably determine from visual inspection alone whether a fragment is currently valid and within analysis range.

### Rover support capacity

The Rover has **orange** support capacity.

When the player is responsible for establishing sample availability, the Rover should assist by identifying valid nearby fragments.

### Rover performer capacity

The Rover has **green** performer capacity.

The Rover can determine whether fragments are within the valid analysis range using game-state and spatial information.


### Implementation requirements

**Observability**

* Show nearby fragments that satisfy the analysis-range requirements.
* Clearly distinguish not yet analyzed samples

**Directability**

* If several valid fragments are present, allow the player to select which fragment should be considered for analysis.

#### Inline implementation plan — checkpoint 1.1

**Implementation status — 2026-08-14:** Implemented, compile-verified, and accepted by the user.

**Capacity entry point:** add
`GridManager.GetSampleLocationsAroundPosition(Vector2I gridPosition)` and retain the existing
singular method as a compatibility wrapper. The plural query returns every valid fragment in a
stable order with no duplicates. A small availability model combines position with `NeverAnalysed`
versus `PreviouslyAnalysed`; checkpoint 1.2 adds the richer lifecycle labels.

In `SelectedRobotUI`, show valid samples in a compact selector/status list when the ground rover is
in range; decision 1.1-B intentionally adds no world highlights. The selected entry, not an
arbitrary first match, is sent to the existing analysis request flow. An unavailable/aerial rover
sees no selectable target. Re-evaluate the list when the selected rover moves so stale targets
cannot be chosen. The list also refreshes after analysis state is saved, changing the entry from
`NOT YET ANALYSED` to `PREVIOUSLY ANALYSED` for the lifetime of the level.

**Automatic:** completed C# query/model changes, compatibility wrapper, dynamic selector/status UI,
and movement/state-save signal wiring. The UI is created in code, so no Godot scene setup is
required and it is not vulnerable to the editor overwriting newly added `.tscn` nodes. Per R-01,
no development test fixture was added.

**Godot check:** no node or signal setup. Run the zero-, one-, and two-sample cases and confirm the
selector remains legible and does not interfere with normal rover selection. Testing two samples
requires a scene containing two fragments within the rover's Manhattan-1 range.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three reported warnings are pre-existing warnings in `BuildingComponent.cs` and
`SaveManager.cs`.

**Decision 1.1-A — valid analysis range:**

* **A (recommended):** preserve current behavior: the rover's own tile plus four orthogonal
  neighbours (Manhattan distance 1).
* **B:** include diagonal neighbours.
* **C:** use an exported configurable radius; specify radius and distance rule in the answer.
* **Answer:** `[ 1.1-A: A]`

**Decision 1.1-B — multiple-sample presentation:**

* **A (recommended):** both world tile highlights and a compact text selector/status list.
* **B:** selector/status list only.
* **C:** world highlights only, click a highlighted fragment to select it.
* **Answer:** `[ 1.1-B: B]`

**Gate test 1.1 (fill after implementation)**

* **Expected:** zero valid samples shows none; one shows the correct position; multiple samples are
  all shown and individually selectable; previously analysed and new samples look different; an
  aerial rover and an out-of-range fragment cannot be selected.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed (include the positions tested):** `[OK]`
* **Build/scene/resolution:** `[OK]`
* **Approved to implement 1.2:** `[x] YES  [ ] NO`

---

## 1.2 Decide to initiate analysis

### Player capacity

The player has **green** performer capacity and can decide independently whether a sample should be analysed.

### Rover support capacity

The Rover has **green** support capacity.

The Rover should provide information that helps the player make that decision without making the decision for them.

### Rover performer capacity

The Rover has **orange** performer capacity.

The Rover may determine that analysis appears appropriate, but its decision depends on mission intent and should therefore remain approved by the player.

### Player support capacity

The player has **orange** support capacity when the Rover performs.

### Implementation requirements

**Observability**

* Show the analysis status of the fragment.
* At minimum indicate whether the fragment is:

  * available for analysis;
  * currently being analysed;
  * previously analysed;
  * restored from an existing analysis state;
  * previously solved, if that state is available.

When the Rover proposes analysis, expose that intention to the player.

The player should understand whether the Rover is recommending or attempting to initiate analysis rather than having analysis begin unexpectedly.

#### Inline implementation plan — checkpoint 1.2

**Implementation status — 2026-08-14:** Revised from the user observation, compile-verified, and
accepted in the focused follow-up test.

**Capacity entry point:** add a level-owned `GetFragmentAnalysisStatus(Vector2I)` query and a Rover
`ProposeAnalysis(FragmentSampleAvailability)` action. Use `Available`, `Analysing`,
`PreviouslyAnalysed`, and `Solved` as primary states; use `Restored` as a badge on the current
reopened session rather than an incompatible fifth state.

Correct the current solved-history semantics while adding the status model: `WasSolved` is captured
from only the current controls and can be overwritten to `false` after a solved puzzle is changed.
Add monotonic `WasEverSolved`, set it once the normal puzzle solved condition fires, and never let
the Rover infer it from its own metrics. A generic mandatory-support proposal displays the target,
current status, an observable reason such as “unanalysed sample in range,” and approve/dismiss
controls. Dismissal returns to idle and does not open the analyzer.

Per the 1.2 test observation, do not expose Supporter/Performer allocation in `SelectedRobotUI`.
Allocation applies inside the Fragment Analysis task; world-level availability is always presented
as generic mandatory Rover support.

**Automatic:** lifecycle/status model, monotonic solved flag, generic proposal panel, C# wiring, and
state restoration changes.

**Implemented behavior:** `BaseLevel` is authoritative for `Available`, `Analysing`,
`PreviouslyAnalysed`, and `Solved`. An active reopened session receives a `Restored` badge. The
selected-rover panel presents one mode-independent `ROVER PROPOSAL · WAITING FOR PLAYER`; it does
not expose OFF / SUPPORTER / PERFORMER. The proposal exposes target, status, and an observable
reason and can be dismissed without opening analysis. Approval revalidates that the target remains
in range before using the existing request flow. Per decision 1.2-A, selecting a ground rover with
an available sample can create the proposal; simply moving into range does not create an
unsolicited proposal. `WasEverSolved` is monotonic for the current generated puzzle and resets only
when Reload creates a new puzzle.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three reported warnings are pre-existing warnings in `BuildingComponent.cs` and
`SaveManager.cs`.

**Godot check:** no setup. Exercise each status, including solve → change a control → close → reopen,
and verify the display remains “Solved” with a “Restored” badge when applicable.

**Decision 1.2-A — proposal trigger / mission intent:** no higher-level Rover mission planner exists
in the repository today.

* **A (recommended):** propose only when a ground rover is selected and a valid sample becomes
  available; never auto-open unsolicited.
* **B:** also propose immediately when rover movement enters range.
* **C:** wait for a future mission-planner event; implement status display now but no proposal.
* **Answer:** `[ 1.2-A: A]`

**Decision 1.2-B — proposal approval interaction:**

* **A (recommended):** explicit `APPROVE ANALYSIS` / `DISMISS` buttons, with no timeout.
* **B:** visible countdown with a cancel button before opening.
* **C:** immediate automatic opening. This conflicts with the stated player-approval requirement
  and will only be implemented if that requirement is intentionally changed.
* **Answer:** `[ 1.2-B: A]`

**Gate test 1.2 (fill after implementation)**

* **Expected:** all lifecycle states are truthful; generic mandatory Rover intent is visible before
  opening; dismiss performs no action; approval is required; a once-solved fragment remains marked
  solved after controls are changed and the session is restored.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed:** `[The "approved analysis only show up if I select performer/supporter in the dropdown list button, remove the "Performer/Supporter" at this stage form the selectedRobotUI and assume a generic proposal to analyse (mandatory support) because it is outside analysis.]`
* **Build/scene/resolution:** `[OK]`
* **Approved to implement 1.3:** `[x] YES  [ ] NO`
* **Follow-up applied:** `[x] Removed the allocation dropdown from SelectedRobotUI; the proposal is
  now generic mandatory support and appears without selecting Supporter or Performer.]`
* **Focused follow-up result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`

---

## 1.3 Initiate the Analyse Sample function

### Player capacity

The player has **green** performer capacity.

The existing manual **Analyse Sample** action remains available.

### Rover support capacity

The Rover has **red** support capacity. No Rover-support behavior is required for a player-performed initiation.

### Rover performer capacity

The Rover has **green** performer capacity.

The Rover may programmatically open the Fragment Analysis interface when higher-level logic has already determined that analysis should begin.

### Player support capacity

The player has **red** support capacity for the execution itself.

### Implementation requirements

No additional interdependence interface is required for the execution of this capacity.

Reuse the existing analysis-opening logic rather than creating a separate Rover-specific analysis system.

#### Inline implementation plan — checkpoint 1.3

**Implementation status — 2026-08-14:** Implemented, compile-verified, and accepted by the user.

**Capacity entry point:** extract one validated `OpenFragmentAnalysis(...)` path in `BaseLevel`.
Manual selection and an approved Rover proposal both submit the same request containing fragment
position and action origin. Immediately before opening, re-check that the requester is a ground
rover and the selected fragment is still in range. Then reuse the existing scene instantiation,
spatial context, seed/variant selection, per-position restore, and state-save callback.

There will be no Rover-specific `FragmentPuzzle`, generator, canvas, or alternate solve path.
Action origin is retained only for transparency/telemetry and must not alter the generated puzzle.

**Automatic:** refactor `GameEvents`, `BaseLevel`, and `SelectedRobotUI`; keep a compatibility event
adapter if another caller is found; add origin/status text to the autonomy panel.

**Implemented behavior:** the fragment request signal now carries fragment position, requesting
rover, and `Player` versus `Rover` action origin. `BaseLevel.OpenFragmentAnalysis(...)` is the only
opening path. Before changing any existing analysis UI it rejects a missing, aerial, lifted, or
stale selected rover; a fragment outside the requester's current Manhattan-1 range; and a duplicate
request while analysis is already active. Manual Analyse and approved proposals both use this path
and then share the same scene instantiation, seed/state restoration, canvas, generator, and save
callback. The analyzer header displays `PLAYER` or `ROVER` origin for transparency, and the origin
is retained in `FragmentAnalysisState` without influencing puzzle generation. No compatibility
adapter was needed because the repository contained no other callers.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three reported warnings are pre-existing warnings in `BuildingComponent.cs` and
`SaveManager.cs`.

**Godot check:** no setup. Open the same saved fragment once manually and once through an approved
Rover proposal and compare seed, glyph appearance, all controls, rotation, pan, zoom, and history.

**Gate test 1.3 (fill after implementation)**

* **Expected:** manual and Rover-approved initiation reach the same interface and restore identical
  state; a stale/out-of-range request is rejected with feedback; no unexpected analysis window is
  opened; `OFF` retains today's manual flow.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed (include fragment position/seed):** `[OK]`
* **Build/scene/resolution:** `[OK]`
* **Approved to implement 2.1:** `[x] YES  [ ] NO`

---

# 2. Locate meaningful signal in the sample

## 2.1 Sense sample features

### Player capacity

The player has **yellow** capacity.

The player can visually perceive lines, fractures, veins, geometric structures, and other features, but faint or noisy structures can be missed.

### Rover support capacity

The Rover has **green** support capacity.

The Rover should perform machine-oriented feature detection and visually assist the player.

Potential detected features may include:

* connected strokes;
* intersections;
* unusually straight geometry;
* geometric boundaries;

### Rover performer capacity

The Rover has **yellow** performer capacity.

The Rover can detect candidate features but cannot guarantee that detected features belong to the true signal.

The player does not directly support this sensing capacity when the Rover performs.

### Implementation requirements

**Observability**

* Rover-detected features must be visually highlighted on the scan.
* Rover annotations must remain distinguishable from the actual fragment image.

**Directability**

* The player must be able to:

  * add a feature the Rover failed to identify;
  * select a Rover-detected feature;
  * dismiss a Rover-detected feature.

Maintain a data representation of Rover-detected and player-defined features rather than baking highlights directly into the puzzle image.

#### Inline implementation plan — checkpoint 2.1

**Implementation status — 2026-08-14:** Revised four times from failed user gates; awaiting the
fourth focused follow-up test.

**Capacity entry points:**

* `FragmentCanvas.CaptureObservableScan()` returns anonymous, puzzle-normalized primitives that
  faithfully match what the active controls currently render: visible segment portions, apparent
  width/colour/intensity, and natural vein/noise geometry. It omits signal/distractor role,
  importance, glyph identity, filter keys, correct rotation, and monolith position/direction.
* `FragmentFeatureDetector.DetectFeatures(FragmentObservableScan)` finds connected strokes,
  near-intersections, long/straight runs, and closed/geometric boundaries with confidence values.
* `FragmentAnalysisRover.ApplyFeatureEdit(...)` selects, accepts, dismisses, or restores a
  feature while preserving provenance (`Rover` or `Player`) and stable IDs.

Refactor the draw calculation just enough that rendering and observation consume the same neutral
visual primitives; do not duplicate a second approximation of visibility. Draw Rover features as
high-contrast magenta annotations in `FragmentRoverOverlay`. Per the third user gate, use no
interaction modes: a click selects the nearest feature, a drag pans, and the wheel zooms. Manual Add
Feature is removed.

**Automatic:** observable DTO/API, detector, persistent feature model, overlay rendering and hit
testing, toolbar nodes, input arbitration, deterministic algorithm checks, and a source-code guard
that Rover files do not access `Puzzle`, `Correct*`, `IsPuzzleSolved`, glyph identity, line role, or
monolith truth.

**Implemented behavior:** `FragmentCanvas.CaptureObservableScan()` now reuses the active drawing
visibility path to collect anonymous normalized line primitives, including visible reconstruction,
inactive/noise strokes, and mineral veins. Whole-scan capture ignores pan and zoom but reflects
processing, channel, rotation, generated geometry, and resize changes. `FragmentFeatureDetector`
deterministically keeps the strongest visible copy of overlapping strokes, groups endpoint-connected
segments into whole multi-stroke features, and scores groups using observable length, intensity,
coherence, and intersections. It ranks at most 10 groups rather than exposing up to 128 individual
segments. This allows bright reconstructed structures to outrank and replace fading inactive
fractures as processing improves. For structured, closed, or branching groups it highlights only
the most salient 40% of strokes instead of tracing a complete glyph; simple open crack chains remain
represented as one whole feature. It receives no semantic puzzle fields.

`FragmentAnalysisRover` refreshes Rover candidates when observable controls change, preserves
player-defined features and prior accept/dismiss dispositions for matching geometry, assigns stable
session IDs, and persists everything in `FragmentAutonomyState`. The overlay renders proposed Rover
features as high-contrast magenta marks over a black annotation rail with separate feature markers;
accepted Rover features become amber and player features remain solid green.
The panel provides `SCAN FEATURES`, overlay visibility, selected feature
confidence/provenance/status, a selector containing visible and dismissed features, and
`ACCEPT / DISMISS / RESTORE`. A single click selects, drag pans, and wheel zooms without a mode
switch. Pan/zoom no longer overwrites the last feature-scan result text. Compact backward/forward
arrows browse the last five non-view actions without permanently occupying the panel with their
contents, and the Rover panel is widened from 330 to 430 pixels while remaining vertically
scrollable at smaller resolutions. Global Rover `OFF` immediately hides all
Rover-discovered overlays and removes them from the active feature selector while retaining saved
review state for later re-enabling.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three reported warnings are pre-existing warnings in `BuildingComponent.cs` and
`SaveManager.cs`. A source guard found none of `.Puzzle`, `Correct*`, `GlyphType`,
`MonolithPosition`, `MonolithDirection`, line role, distractor identity, or `IsPuzzleSolved` in
`FragmentAnalysisRover.cs`, `FragmentFeatureDetector.cs`, or `FragmentRoverOverlay.cs`. Per R-01,
no automated test project was added.

**Godot check:** no setup. Use a known seed, toggle several filter/channel configurations, and test
the numbered accept/dismiss queue with overlays both enabled and hidden. Accept one feature, change
processing/channel settings, and confirm it remains numbered and amber after the automatic rescan.

**Decision 2.1-A — observation representation:**

* **A (recommended):** neutral analytic render primitives produced by the same draw pipeline. This
  is deterministic and testable while exposing only visible appearance.
* **B:** literal downsampled raster capture and image processing. This is more literal but slower,
  harder to test, and risks a readback hitch after slider changes.
* **C:** analytic primitives initially, with a raster detector added as a later research variant.
* **Answer:** `[ 2.1-A: A]`

**Decision 2.1-B — detection scope:**

* **A (recommended):** the whole virtual scan at the current processing configuration; the Rover
  can nominate an off-screen region, then navigate to it.
* **B:** only the current viewport; the Rover must build knowledge by exploring incrementally.
* **Answer:** `[ 2.1-B: A]`

**Decision 2.1-C — interaction gesture (superseded by user gate):**

* **A (recommended):** drag from one endpoint to another to add a straight feature; select nearest
  overlay stroke for accept/dismiss.
* **B:** freehand polyline drawing.
* **C:** support both in the first pass.
* **Answer:** `[ 2.1-C: A]`
* **Final override:** `[No modes and no Add Feature. Click selects; drag pans; wheel zooms.]`

**Gate test 2.1 (fill after implementation)**

* **Expected:** detected features are repeatable for the same seed/settings, visually distinct from
  the scan, and change only when observable geometry changes; select/dismiss/restore works; switching
  overlay visibility or editing features never changes puzzle pixels, controls, or solved state;
  no hidden-solution field is read by Rover code.
* **Result:** `[] PASS  [X] FAIL  [ ] BLOCKED`
* **Seed/configuration and observed edits:** `[It seems like only some of the cracks get highlighted by the autonomy, the actual glyph never have a stroke highlighted. autonomy highlight is indistiguishable from what appear with parameters adjustments, the dashed cyan just makes it look like the polarization is not correct. when zooming panning, the scan features result text disappear which is not good.]`
* **Build/scene/resolution:** `[ I don't think that rover detection updates with changes in processing/channels, ideally it should realize a lot of the initial feature detected are just meaningless cracks. Because these cracks are made of line segment, the autonomy detects up to 5 feature per crack, it should consider a whole crack at once otherwise it is useless because the player has to sort way too many segments. If I dismiss a stroke and change focus I can no longer restore that stroke which is a problem. left click + drag should automatically go back to panning instead of having to select the pan radio button.]`
* **Approved to implement 2.2:** `[x] YES  [ ] NO — user requested progression to 2.2]`
* **Follow-up applied:** `[x] Grouped whole connected features; strongest visible strokes win;
  ranked cap reduced to 32 groups; magenta/black annotation style; feature selector includes
  dismissed entries; scan result survives pan/zoom; ADD returns automatically to PAN.]`
* **Focused follow-up result:** `[] PASS  [x] FAIL  [ ] BLOCKED REASON: accepted features shoudl have another color code, . zoom/drag to pan while in select mode does not change mode to pan, it shoud. history should be 5 last moves and all visible, the right "autonomy" banner can take up 30% more width. when activating only polarization, at once, all of the glyphs are highlighted by the autonomy which is too much, it makes the puzzle too easy'`
* **Second follow-up applied:** `[x] Accepted Rover features are amber; Select zoom/drag pans and
  falls back to PAN; five-action history is visible and persisted; panel width is 430 px; detection
  is capped at 10 ranked groups and structured groups expose only their most salient 40% of strokes.]`
* **Second focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED BLOCKED REASON: There shouldn't be modes at all for panning vs. selecting, if a single click lands on a feature, select it, while allowing drag and zoom to pan, remove add feature entirely as it is not needed. selectioning OFF on rover autonomy allocation should hide all of the autonomy discovered features at once.`
* **Third follow-up applied:** `[x] Removed PAN/SELECT/ADD modes and Add Feature; unified click,
  drag-pan, and wheel-zoom interaction; global OFF hides Rover annotations and Rover entries in the
  feature selector without discarding their stored review state.]`
* **Third focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The history should just be a backward/forward arrow, no need to explicit the content of the history. if I accept a rover-detected feature, and then adjust a processing/channel it removes it from the "accepted" back to just proposed, it should persist. the autonomy should number the features visibly and auto-focus on the first so the player can just accept/dismiss and consume all of the proposed features much more easily`
* **Fourth follow-up applied:** `[x] Replaced the expanded five-line history with backward/forward
  arrow navigation; reviewed feature IDs and accepted/dismissed dispositions survive observable
  rescans through tolerant spatial matching and unmatched reviewed annotations are retained;
  Rover annotations display visible numbers; the first proposal is selected and centered after a
  scan, and accept/dismiss advances and centers the next proposal automatically.]`
* **Fourth focused follow-up result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: ]`

---

## 2.2 Interpret potential signal regions

### Player capacity

The player has **yellow** capacity.

The player can judge whether an area may contain artificial signal structure, but noise and distractors can produce false interpretations.

### Rover support capacity

The Rover has **green** support capacity.

The Rover should group detected features into candidate regions that appear likely to contain meaningful structure.

### Rover performer capacity

The Rover has **yellow** capacity.

The Rover may independently nominate candidate regions but cannot guarantee they contain the true signal.

### Player support capacity

The player has **yellow** support capacity.

The player can improve Rover interpretation through semantic visual judgment.

### Implementation requirements

**Observability**

* Rover candidate regions must be visibly marked.

**Directability**

* The player must be able to:

  * accept a candidate region;
  * dismiss a candidate region;
  * manually add a new candidate region.

Candidate regions should be stored independently from the underlying puzzle so their state can be updated during analysis.

#### Inline implementation plan — checkpoint 2.2

**Implementation status — 2026-08-14:** Revised twice after failed user gates and
compile-verified; awaiting the third focused follow-up test.

**Capacity entry points:** `FragmentRegionDetector.GroupCandidateRegions(...)` clusters active
features by endpoint connectivity, intersection density, proximity, straightness, and boundedness;
`FragmentAnalysisRover.ApplyRegionEdit(...)` accepts, dismisses, restores, or manually adds a
region. Store normalized bounds, contributing feature IDs, confidence, provenance, and disposition
separately from the puzzle and feature detector output.

The overlay uses translucent amber for proposed regions, green for accepted, and muted/hidden for
dismissed. Region edits do not implicitly delete features. Changing the underlying scan may update
confidence, but it must not silently reverse an explicit player acceptance or dismissal.

**Automatic:** clustering, data/state persistence, overlay styles, region hit testing, edit toolbar,
and scene wiring.

**Implemented behavior:** `FragmentRegionDetector.GroupCandidateRegions(...)` deterministically
clusters non-dismissed observable features using padded normalized bounds and spatial proximity,
ranks the clusters by feature confidence, density, and boundedness, and publishes at most six
anonymous candidates. Proposed Rover regions are translucent amber, accepted Rover regions are
green, and player-drawn regions are bright green; every visible region has a stable `R#` label.
The region selector includes dismissed regions for restoration and displays provenance,
disposition, confidence, and contributing-feature count.

Explicit player review survives rescans: tolerant spatial matching carries accepted/dismissed
dispositions forward, unmatched reviewed regions remain stored, and player regions are never
replaced by detector output. The first proposal is selected and centered automatically; accept or
dismiss advances to the next proposal. `DRAW REGION` arms exactly one normalized click-drag and
then returns to normal click-select/drag-pan behavior. Region geometry and selection are cloned in
`FragmentAutonomyState`, so the existing close/reopen state path persists them independently of
the puzzle and feature lists. Global/task OFF hides Rover regions without deleting review state.

Per the first user gate, candidate grouping is now an explicit snapshot created only by `GROUP
REGIONS`; processing/channel adjustments continue updating observable features but never generate
or regroup regions automatically. Accepting a Rover region or drawing a player region applies it
as a persistent crop: the view zooms to fit and centers it while features outside it become dismissed, including
newly observed outside features after later parameter adjustments. Dismissing a region
dismisses the features enclosed by it. These feature removals are non-destructive review decisions:
the existing dismissed-feature selector and `RESTORE` control can recover an individual feature.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three warnings are pre-existing in `BuildingComponent.cs` and `SaveManager.cs`. The Rover
hidden-field source guard passes. No automated test framework was added per R-01.

**Godot check:** no setup. In Support mode, group regions, then accept one and dismiss another. Arm
`DRAW REGION`, drag one rectangle, and confirm normal drag-to-pan resumes immediately afterward.
Change filters, pan, and zoom; close/reopen the fragment and verify region geometry, numbering,
dispositions, and alignment persist. Switch global allocation OFF: Rover regions should hide while
the player-drawn region remains visible and drawing another manual region remains available.

**Decision 2.2-A — first-pass region shape:**

* **A (recommended):** normalized axis-aligned rectangles, added by click-drag. They remain simple
  to edit and map cleanly to navigation/inspection coverage.
* **B:** arbitrary lasso polygons.
* **C:** fixed grid cells only.
* **Answer:** `[ 2.2-A: A]`

**Gate test 2.2 (fill after implementation)**

* **Expected:** candidate regions correspond to clusters of current features; accepted, dismissed,
  and player-added regions remain independent and survive close/reopen; no region edit changes raw
  scan geometry or source features; overlays stay aligned at every pan/zoom.
* **Result:** `[] PASS  [x] FAIL  [ ] BLOCKED`
* **Observed:** `[OK]`
* **Build/scene/resolution:** `[ok]`
* **Approved to implement 2.3:** `[ ] YES  [x] NO REASON: dismissing a region should remove the features inscribed within it, the player should be able to use the region like a crop, to recenter on an object of interest and thus discarding the unnecessitated features, the player should also be able to draw regions. when tweaking parameters the robot should not generate new candidate region except if a dedicated button is pushed`
* **Focused follow-up applied:** `[x] Regions are generated only by GROUP REGIONS; dismissing a
  region dismisses its enclosed features; accepting or drawing a region centers it and dismisses
  outside features like a crop; feature restore remains available; DRAW REGION remains one-shot.]`
* **Focused Godot check:** `[Adjust processing/channel controls and confirm the region list does
  not change until GROUP REGIONS is pressed. Dismiss a region and confirm its enclosed feature
  overlays disappear. Accept another region and confirm it is fitted/centered while outside
  features disappear. Draw a region and confirm the same crop behavior, then drag normally to pan.]`
* **Focused follow-up result:** `[] PASS  [x] FAIL  [ ] BLOCKED REASON: double click on a region should enter resize region mode]`
* **Second follow-up applied:** `[x] Double-clicking a visible region enters resize mode with four
  corner handles. The next drag resizes from the nearest corner, updates normalized bounds and
  contributing feature membership, reapplies an active crop, persists the result, and exits resize
  mode. Right-click or Escape cancels without changing geometry.]`
* **Second focused Godot check:** `[Double-click a Rover and a player-drawn region; confirm four
  handles appear. Drag near each corner and verify the opposite corner anchors, overlays remain
  aligned, and crop membership updates. Close/reopen to verify resized bounds persist. Enter resize
  again and verify both right-click and Escape cancel without changing the rectangle.]`
* **Second focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: Move feature sensing UI section below cadidate region ]`

---

## 2.3 Decide where to inspect

### Player capacity

The player has **yellow** capacity.

The player can choose inspection areas but may overlook unexplored regions or repeatedly inspect the same area.

### Rover support capacity

The Rover has **green** support capacity.

The Rover should track spatial inspection history and assist the player in deciding where to look next.

### Rover performer capacity

The Rover has **yellow** capacity.

The Rover may independently choose a promising next inspection region.

### Player support capacity

The player has **yellow** support capacity.

The player can redirect the Rover based on semantic interpretation.

### Implementation requirements

**Observability**

* Represent regions as:

  * unexplored;
  * inspected;
  * candidate/interesting where applicable.

The player should be able to understand which portions of the virtual sample have already been examined.

**Directability**

* The player must be able to:

  * accept a Rover-proposed inspection region;
  * dismiss it;
  * add or designate another region.

Maintain inspection history across autonomous and manual navigation.

#### Inline implementation plan — checkpoint 2.3

**Implementation status — 2026-08-14:** Scope reduced after the first user gate, implemented, and
compile-verified; awaiting the focused follow-up test.

**Scope override:** checkpoint 2.3 is region sequencing only. The 8 × 8 coverage grid, exploration
history manager, unexplored/inspected overlay, target proposal ranking, crosshair, accept/dismiss
target workflow, and player target designation introduced in the first pass have been removed.
Candidate-region creation, acceptance, dismissal, drawing, cropping, and resizing remain owned by
2.2.

**Capacity entry point:** `FragmentRegionSequenceView.SetContent(...)` receives only the current
neutral observable scan plus non-dismissed candidate regions. Once at least two visible regions
exist, it automatically opens a SIDE-BY-SIDE comparison: each half of the full analysis canvas is
an undistorted zoom extract of one region, labeled with its stable `R#`. Processing/channel changes
refresh both extracts so subsequent analysis can continue with both areas in sight. With more than
two regions, backward/forward arrows sequence through stable ID-ordered pairs. The player can turn
SIDE-BY-SIDE off to return to the normal canvas for drawing, selecting, or resizing regions.

The sequence respects allocation visibility: Rover regions disappear from it when Rover region
interpretation is OFF, while player regions remain eligible. It is a derived presentation of the
persisted 2.2 regions and introduces no separate inspection state.

**Automatic:** code-native split comparison renderer, analytic segment clipping, aspect-preserving
region transforms, automatic activation at two regions, pair sequencing, processing refresh, and
panel/scene wiring.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three warnings are pre-existing in `BuildingComponent.cs` and `SaveManager.cs`. The hidden-field
source guard passes.

**Godot check:** no setup. Draw or retain two separated regions and confirm SIDE-BY-SIDE opens
automatically with two full-height, undistorted `R#` extracts. Change processing/channel controls
and confirm both extracts update. Turn SIDE-BY-SIDE off, resize one region, and re-enable it to
confirm the extract updates. Add a third and fourth region and verify the arrows show stable pairs;
dismiss a region and verify it leaves the sequence.

**Decision 2.3-A — inspection granularity:**

* **A (recommended):** exported 8 × 8 normalized coverage grid, visually smoothed into region
  overlays; tune the resource later without changing saved coordinate semantics.
* **B:** rectangular viewport footprints only, with no grid discretization.
* **C:** specify another initial grid size: `[ columns × rows: ]`.
* **Answer:** `[ 2.3-A: let's say rover or player draw 2 regions, at this stage the two region should show up taking full size of canva one next to the other, like a zoom extract, so that the rest of the analysis can go on with those two regions in sight.]`

**Gate test 2.3 (fill after implementation)**

* **Expected:** two non-dismissed regions automatically appear as labeled, undistorted, side-by-side
  zoom extracts filling the analysis canvas; processing changes update both; arrows sequence any
  additional region pairs; disabling comparison restores normal region editing; no coverage grid,
  inspection manager, or separate target state remains.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed:** `[ok]`
* **Build/scene/resolution:** `[ok]`
* **Approved to implement 2.4:** `[ ] YES  [x] NO I think we should dramatically reduce the scope of 2.3, it should just be a sequencing for region not a full coverage indicator/manager`
* **Focused follow-up applied:** `[x] Removed the complete first-pass coverage/target system and
  replaced it with automatic two-up region sequencing and pair navigation.]`
* **Focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The proposed feature should appear in the side by side zoom]`
* **Second follow-up applied:** `[x] Side-by-side extracts now render the 2.1 feature annotation
  layer after the observable scan: proposed Rover features are dashed magenta, accepted Rover
  features are amber, player features are green, the selected feature is emphasized, dismissed
  features stay hidden, every visible feature retains its `F#` label inside each crop, and Rover
  feature marks still obey global/task OFF visibility.]`
* **Second focused Godot check:** `[Create two regions containing proposed features and verify the
  same dashed magenta strokes and F# labels appear in both the normal canvas and their corresponding
  split extracts. Accept, select, dismiss, and restore features and confirm both extracts update
  their colour/emphasis/visibility. Switch Rover feature sensing OFF and confirm Rover marks vanish
  from the extracts without changing the underlying scan.]`
* **Second focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: from the side by side view I should be able to accept/dismiss proposed regions and receive a visual feedback from there, dismissed region should disappear]`
* **Third follow-up applied:** `[x] Each proposed split pane now has direct ACCEPT and DISMISS
  controls. Clicking a pane selects its region; acceptance changes its border/status to green,
  selection gets a white outer border, and dismissal removes the pane immediately and repaginates
  the remaining stable region sequence.]`
* **Third focused Godot check:** `[Open two or more proposed regions in SIDE-BY-SIDE. Click a pane
  body and confirm its white selection border and matching region panel selection. Click its inline
  ACCEPT and confirm a green border/ACCEPTED badge. Click inline DISMISS on another proposal and
  confirm it disappears immediately; with additional regions, confirm the next stable R# fills the
  vacancy, and with fewer than two remaining, confirm the normal editable canvas returns.]`
* **Third focused follow-up result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED — behavioral gate accepted;
  editable-panel follow-up handled below.]`
* **Fourth follow-up applied:** `[x] Moved the complete right-side Rover panel hierarchy, layout,
  labels, buttons, tooltips, initial visibility, sizing, and separators into the editable
  scenes/ui/FragmentAutonomyPanel.tscn scene. FragmentAnalysisUI.Autonomy.cs now instantiates that
  scene and resolves its unique-name controls instead of constructing the panel programmatically.
  Only capability override rows remain generated because their contents come from the capability
  catalog.]`
* **Fourth focused Godot check:** `[Open FragmentAutonomyPanel.tscn directly in the Godot editor,
  change a harmless layout property such as the root minimum width or Content separation, run the
  analyser, and confirm the change appears while all mode, feature, region, sequence, history,
  pause, and task-allocation controls still work.]`
* **Fourth focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: There is a problemn with side by side regions, if I tweak some parameters, features are detected, I ask to group region, so see them side by side, if I accept the first one, all of the features disappear at once.]`
* **Fifth follow-up applied:** `[x] Separated single-region crop acceptance from multi-region
  comparison acceptance. ACCEPT from the normal canvas retains 2.2 crop behavior; ACCEPT from an
  inline split-pane control or the panel while SIDE-BY-SIDE is visible now only marks the region
  accepted, preserving all detected features and the other comparison panes.]`
* **Fifth focused Godot check:** `[Detect features, group at least two proposed regions, and enter
  SIDE-BY-SIDE. Accept the first region using its inline control and confirm its pane turns green
  while every feature annotation in both panes remains. Repeat using the right-panel ACCEPT for
  another proposed region. Disable SIDE-BY-SIDE, accept a proposed region normally, and confirm the
  original 2.2 crop behavior still applies only in that single-canvas workflow.]`
* **Fifth focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: ]`
* **General region-acceptance rule:** `[Accepting a region activates crop semantics over the union
  of every non-dismissed region, not only the region just accepted. Features inside other pending
  or previously accepted regions remain intact; only features outside the complete retained region
  set are dismissed. Dismissing a region still removes its enclosed features and removes that area
  from future crop protection.]`
* **General-rule Godot check:** `[Create three separated proposed regions containing features.
  Accept the first and confirm features remain in all three. Accept the second and confirm the first
  and third remain intact. Dismiss the third and confirm only its enclosed features disappear. Then
  change processing/channel settings and confirm accepted-region protection persists.]`
* **General-rule follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: In side by side view, features pending acceptance are not shown with a distinct style so the player does not know which one will be accepted]`
* **Sixth follow-up corrected:** `[x] Region acceptance never accepts its enclosed features. In
  SIDE-BY-SIDE, the one currently selected proposed feature—the pending feature affected by the
  feature ACCEPT/DISMISS controls—is instead marked with a thick cyan dashed line and an explicit
  PENDING · F# label. Other proposed features retain their normal magenta dashed style. Accepting,
  drawing, or resizing a region leaves every retained feature's own disposition unchanged.]`
* **Sixth focused Godot check:** `[Create two regions containing several proposed features and enter
  SIDE-BY-SIDE. Select different proposed features with the feature selector and confirm only the
  selected one is cyan and labelled PENDING · F#, while the others remain dashed magenta. Accept a
  region and confirm none of its features turn amber or become accepted. Repeat with a player-drawn
  region and after resizing an accepted region.]`
* **Sixth focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The back arrow history should work consistently with any action, if I dismissed a region and it was an error, clicking the back arrow should bring that region back, in normal and side-by-side view]`
* **Seventh follow-up applied:** `[x] The history arrows are now real five-action undo/redo controls,
  rather than a browser for action-label text. History snapshots include feature and region lists,
  dispositions, selections, crop state, autonomy allocation, and analysis control configuration.
  Back restores a dismissed region and its enclosed feature states; Forward reapplies the dismissal.
  Restoring a snapshot refreshes the normal overlay, selectors, and SIDE-BY-SIDE region sequence.
  Pan and zoom are intentionally excluded so navigation gestures do not consume the five slots.]`
* **Seventh focused Godot check:** `[In normal view, dismiss a region and click Back: confirm the
  region and its enclosed features return. Click Forward: confirm they are dismissed again. Repeat
  from SIDE-BY-SIDE and confirm the pane disappears, returns with Back, and disappears with Forward.
  Then test Back/Forward after accepting or restoring a feature, accepting/restoring a region,
  grouping regions, scanning features, and changing a processing/channel control. After undoing,
  perform a new edit and confirm Forward is disabled because the old redo branch was discarded.]`
* **Seventh focused follow-up result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: ]`
* **History processing/channel requirement:** `[x] Every discrete polarization, spectral, and
  surface level adjustment, and every polarization, spectral, surface, electromagnetic, resonance,
  and X-Ray toggle, creates an undoable history state. Back/Forward restores both the visible UI
  value and the feature/region review state associated with that observable configuration. Rotation
  changes use the same history path; pan and zoom remain intentionally excluded.]`
* **Processing/channel history check:** `[Change one channel toggle and one processing level. Click
  Back once and confirm the processing level plus its feature display return to the previous state.
  Click Back again and confirm the channel toggle plus its feature display return. Click Forward
  twice and confirm both changes are reapplied in order, including the visible buttons/sliders.]`
* **Processing/channel history result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: ]`

---

## 2.4 Navigate the sample

### Player capacity

The player has **green** capacity.

Existing manual pan and zoom controls remain available.

### Rover support capacity

The Rover has **green** capacity to assist navigation.

### Rover performer capacity

The Rover has **green** capacity.

It can autonomously pan and zoom toward selected candidate regions.

### Player support capacity

The player has **yellow** capacity to supervise or modify autonomous navigation.

### Implementation requirements

**Observability**

* Make the currently inspected region apparent.
* When autonomous navigation is active, show the Rover's intended next target.

**Predictability**

* Before or during autonomous movement, make it clear where the Rover intends to navigate.

**Directability**

* The player must retain manual pan and zoom.
* Manual input must be able to change the Rover's target.
* Manual intervention should override or interrupt autonomous navigation when necessary.

Autonomous navigation must respect the same virtual-canvas bounds and zoom limits as manual navigation.

#### Inline implementation plan — checkpoint 2.4

**Implementation status:** Implemented; awaiting Godot acceptance test.

**Capacity entry point:** add `FragmentCanvas.FocusRegionAsync(Rect2 normalizedRegion, float zoom,
CancellationToken)` (or the Godot tween equivalent behind the same contract). It converts the
normalized target into the existing view transform, clamps through the same bounds/min/max zoom as
manual controls, announces the target, draws the destination ghost, and eases to it. It emits view
changes with `Player`, `Rover`, or `Restore` origin.

Mouse drag, wheel, arrow keys, a new target selection, mode `OFF`, or `PAUSE` cancels the active
Rover motion immediately. Cancellation never snaps back. Supporter mode previews the target and
offers `GO`; Performer mode navigates after the configured preview unless interrupted.

**Implemented shape:** selecting a non-dismissed region now creates a navigation proposal whenever
2.4's effective allocation is Supporter or Performer. A separate white dashed destination ghost and
`NEXT TARGET · R#` label appear without changing the region's review disposition. The editable Rover
panel scene provides `GO`/`GO NOW`, `CANCEL`, and a navigation-intent label. Supporter waits for `GO`;
Performer waits for `ActionPreviewSeconds`, then starts a cubic ease using
`NavigationDurationSeconds`. Movement uses `FragmentCanvas.NavigateToNormalizedRect(...)`, which
calculates the same fit zoom and clamps against the same minimum/maximum zoom and pan bounds as
manual navigation. Global/task OFF retains the earlier immediate player focus behavior.

Drag, mouse-wheel zoom, overlay pan/zoom, and arrow keys cancel the canvas tween in place and notify
the Rover. The selected 2.4-A policy clears the target, reports `OVERRIDDEN BY PLAYER`, pauses
autonomy, and requires explicit Resume. Processing/rotation input during movement uses the same
override path. Selecting a different region replaces the destination without snapping to the old
one. PAUSE, mode changes, navigation-allocation changes, history restore, and explicit CANCEL also
terminate the active tween. SIDE-BY-SIDE closes when movement actually begins, while its selected
pane and the panel target label communicate intent during preview.

**Automatic:** safe public view API, coordinate conversions, target preview, tween/cancellation,
origin signals, control bindings, and bounds checks.

**Automation result:** all C# behavior and `FragmentAutonomyPanel.tscn` controls are wired
automatically. Godot editor setup: none; the exported preview/duration and target colour can be
visually tuned later if desired.

**Godot check:** no setup. Try every manual navigation input during motion and at every canvas edge
and zoom extreme.

**Decision 2.4-A — manual override policy used by all later autonomous actions:**

* **A (recommended):** cancel the current action and pause autonomy, showing `OVERRIDDEN BY PLAYER`;
  the player explicitly resumes.
* **B:** cancel only the current action, immediately re-plan around the new player state.
* **C:** pause for navigation/rotation but immediately re-plan for processing changes.
* **Answer:** `[ 2.4-A: A ]`

**Gate test 2.4 (fill after implementation)**

* **Expected:** intent and destination appear before movement; accepted movement reaches the target
  without exceeding current bounds/zoom limits; drag, wheel, arrows, target change, pause, and mode
  change each interrupt immediately; manual pan/zoom always remains usable.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed (include interruption types tested):** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 3.1:** `[ ] YES  [x] NO upon pressing group regions I am directly thrown into side-by-side view which interfere with robot navigation. Also reviewing region, if one is accepted, the rover should still navigate to the pending region so that we can accept/dismiss them `
* **2.4 navigation-review follow-up applied:** `[x] GROUP REGIONS now only makes the
  SIDE-BY-SIDE control available; it never activates the comparison view automatically. The player
  must opt into that view explicitly. Accepting a region now advances `SelectedRegionId` to the next
  Rover-proposed region (wrapping through the stored region order) and sends the 2.4 navigation
  preview there. If no proposal remains, the accepted region stays selected. This is identical for
  the normal panel ACCEPT and the inline SIDE-BY-SIDE ACCEPT.]`
* **2.4 focused follow-up check:** `[Press GROUP REGIONS and confirm the normal canvas remains
  visible while SIDE-BY-SIDE becomes enabled. In Support mode, select/accept the first proposed
  region and confirm the next proposed region becomes selected with a destination preview; press GO
  and confirm navigation reaches it. Repeat through all proposals and confirm the last acceptance
  does not target an already reviewed region. Repeat in Performer mode and with inline acceptance
  after manually enabling SIDE-BY-SIDE.]`
* **2.4 focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: After accepting dismissing all pending region, navigate back to the first accepted region, or open side by side if two regions were accepted and go into accept/dismiss feature mode for seamless transition]`
* **2.4 region-to-feature handoff applied:** `[x] When no proposed regions remain, the Rover now
  completes region review and selects the first remaining proposed feature within accepted regions.
  With one accepted region it returns navigation to that region through the normal Supporter or
  Performer path. With two or more accepted regions it opens SIDE-BY-SIDE at that point—not during
  initial grouping—and preserves both accepted panes for comparison. The feature selector receives
  keyboard focus and its ACCEPT/DISMISS controls operate on the visibly selected pending feature.
  If no feature remains, the status explicitly reports that instead of selecting reviewed data.]`
* **2.4 handoff Godot check:** `[Resolve every proposed region with a mix of ACCEPT and DISMISS.
  With one accepted region, confirm the Rover targets it again and the first pending feature inside
  it is selected for feature review. With at least two accepted regions, confirm SIDE-BY-SIDE opens
  only after the last region decision, both accepted panes remain, and one pending feature is clearly
  selected. Use feature ACCEPT/DISMISS repeatedly and confirm review advances without returning to
  candidate-region mode. Repeat with no accepted regions and confirm no invalid navigation occurs.]`
* **2.4 handoff result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: Two details to be fixed, 1. the side-by-side feature pending acceptance style is great, the normal view one is too subtle, we should normalize. 2. the group regions from the rover sometimes create too wide regions, restrict it a little more]`
* **2.4 final visual/grouping follow-up applied:** `[x] The normal canvas now uses the same thick
  cyan dashed stroke, cyan endpoint marks, and `PENDING · F#` label as SIDE-BY-SIDE for the selected
  proposed feature. Both views read the shared exported `PendingFeatureColor`. Automatic grouping
  now uses smaller padding and proximity gaps and refuses transitive merges whose combined width,
  height, or area exceeds conservative normalized limits; a long chain of nearby cracks can no
  longer absorb otherwise separate clusters into one oversized region.]`
* **2.4 final focused check:** `[Compare the selected pending feature in normal and SIDE-BY-SIDE
  views and confirm its colour, weight, dashes, and PENDING label communicate the same state. On
  several seeds/configurations, press GROUP REGIONS and confirm nearby coherent features still group
  while chains of loosely related cracks remain split into tighter regions.]`
* **2.4 final result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: ]`

**2.4 navigation-tween cancellation regression:**

* **Observed:** `[The white `NAVIGATING TO` target appeared, but the canvas did not animate and no
  error was logged.]`
* **First attempted fix:** `[x] The canvas now clears stale drag state before navigation and
  immediately completes a destination already equal to the current view. Retest showed that this
  was not sufficient: execution could still remain indefinitely active with no camera change.]`
* **Definitive execution-path correction:** `[x] Navigation no longer depends on a SceneTree Tween.
  `FragmentCanvas` now owns explicit cubic ease-in/out interpolation in `_Process`, runs in Always
  process mode, updates pan/zoom and overlays each frame, and emits the existing completion event at
  progress 1.0. This makes animation progress and completion part of the canvas state rather than
  an opaque tween that could silently stop.]`
* **Status correction:** `[x] A resize-triggered observable feature refresh can still occur during
  navigation, but it no longer overwrites the Rover's Executing/Navigating status with an unrelated
  Idle/Detected-features message. The navigation target remains authoritative until completion or
  explicit player cancellation.]`
* **Focused retest:** `[In Support mode select a region, press GO, and do not move the pointer;
  confirm pan/zoom completes and the white target clears. Repeat while moving the pointer after GO,
  then in Performer mode after leaving SIDE-BY-SIDE. Finally target the already-focused region and
  confirm it completes immediately rather than remaining stuck on NAVIGATING.]`
* **Regression result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**2.4 cross-monitor responsive-navigation follow-up:**

* **Evidence:** `[Navigation completed on a QHD monitor but remained Executing on the laptop.
  Laptop-only control actions briefly narrowed/restored the canvas, the header and canvas exceeded
  screen width, and the problem reproduced without an error.]`
* **Confirmed cancellation chain:** `[x] On the narrower viewport, accumulated header minimum widths
  overflowed the root layout and triggered canvas resize. Resize intentionally refreshed observable
  features; refresh then requested focus on the selected feature; `FocusNormalizedPoint(...)`
  cancelled the active region navigation while leaving its white intent/status active. Stable QHD
  layout never entered that chain.]`
* **Navigation priority:** `[x] Automatic feature-refresh focus is suppressed while region
  navigation is in progress, with a second guard at the UI focus receiver. Explicit player feature
  selection still overrides navigation through the existing manual-override path.]`
* **Responsive layout:** `[x] Below 2200 logical pixels the redundant sample lifecycle header label
  is hidden, Rover/Compare buttons use compact copy, and fixed label minimums are released. The main
  title and spacing are smaller, the canvas minimum is reduced from 960×540 to 640×360, and the
  Rover panel minimum is reduced from 430 to 380. Larger displays still expand both areas normally.]`
* **Resize-safe destination:** `[x] If the canvas legitimately resizes during navigation, its target
  pan/zoom is recalculated from the same normalized region without restarting elapsed time; resize
  can no longer prevent completion.]`
* **Laptop/QHD retest:** `[Start the game directly on the laptop display. Open analysis, show the
  Rover panel, switch OFF/SUPPORT/PERFORM, GROUP REGIONS, and navigate to several differently sized
  regions. Expected: no horizontal clipping, no half-second aspect-ratio jump, visible pan/zoom,
  and completion every time. Move the running window to QHD and repeat, then start directly on QHD
  and move it to the laptop; behavior and framing should remain equivalent.]`
* **Cross-monitor result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

---

# 3. Improve the signal

## 3.1 Sense changes

### Player capacity

The player has **yellow** capacity.

The player can visually detect improvements and degradation but may miss small changes or have difficulty comparing configurations.

### Rover support capacity

The Rover has **yellow** support capacity.

The Rover should quantify changes in measurable signal characteristics.

### Rover performer capacity

The Rover has **green** capacity.

It can consistently compare current measurable signal properties against previous configurations.

### Implementation requirements

**Observability**

* After processing adjustments, show measured changes in relevant properties such as:

  * signal strength;
  * signal continuity;
  * signal-to-noise relation;
  * background noise;
  * structural visibility;
  * other useful reconstruction metrics implemented by the algorithm.

The metrics do not need to correspond to the hidden puzzle reconstruction score and should not expose the actual solution.

#### Inline implementation plan — checkpoint 3.1

**Implementation status:** Implemented; awaiting Godot acceptance test. Depends on checkpoint 2.1's observable scan. The safe
parameter executor planned under 3.4 is deliberately implemented after 3.2 and before 3.3.

**Capacity entry point:** `FragmentSignalMeasurer.Measure(FragmentObservableScan,
FragmentRegion?)` computes only appearance-derived proxies:

* signal strength from visible stroke intensity/width density;
* continuity from normalized endpoint gaps and connected run length;
* structural visibility from connected components, intersections, and coherent boundaries;
* signal-to-noise proxy from coherent/clustered versus short isolated geometry;
* background noise from isolated-stroke and vein density.

Return named values with units/ranges and sample coverage, then show current value and delta from the
previous measured configuration. Debounce repeated slider events and cache by observable-snapshot
revision so measurement does not run per drawn line or per frame. Never expose or rename the hidden
reconstruction score as a Rover metric.

**Implemented shape:** `FragmentSignalMeasurer.Measure(...)` is a pure deterministic calculator
over `FragmentObservableScan` primitives. It clips geometry to either the full normalized sample or
the selected non-dismissed region and reports 0–100 appearance values for strength, continuity,
signal/noise, background noise, structural visibility, and coverage, plus the contributing primitive
count. It uses visible length, intensity, width, endpoint connectivity, intersections, and isolated
short strokes only; no semantic line role, glyph identity, correctness field, or solution API is
available to it.

The Rover caches by observable revision plus target ID/bounds and debounces processing/channel
changes using the exported `MeasurementDebounceSeconds` setting. The editable panel scene shows a
whole-scan block and, when selected, a region block. Each block includes the current value and a
signed delta from its preceding comparable measurement. Changing targets starts a fresh target
comparison instead of presenting a misleading cross-region delta. Metrics recalculate after target
selection, grouping, review edits, reload, and history restore; turning 3.1's effective allocation
OFF replaces them with an explicit measurement-off state.

**Automatic:** pure metric calculator, settings thresholds, cache/debounce, status-panel rows,
deterministic fixtures, and hidden-access guard.

**Automation result:** calculator, cache/debounce, Rover events, editable `.tscn` labels, and UI
formatting are automatic. Godot editor setup: none; only optional colour/layout tuning remains.

**Godot check:** no setup. On a fixed seed, record metrics for several deliberately different
settings, repeat them, and check readability and responsiveness.

**Decision 3.1-A — metric scope:**

* **A (recommended):** display whole-scan metrics plus a second set for the selected/current target
  when one exists.
* **B:** whole virtual scan only.
* **C:** current viewport/target only.
* **Answer:** `[ 3.1-A: C, narrowed by test feedback to the selected region only (not the free
  viewport and not the whole scan) ]`

**Gate test 3.1 (fill after implementation)**

* **Expected:** identical observable input produces identical metrics; changed visible geometry can
  produce understandable deltas; controls remain responsive; labels describe measured appearance,
  never correctness, hidden score, glyph, or solution settings.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Seed/configurations and metric observations:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 3.2:** `[ ] YES  [x] NO can you move the observable metrics to fit after the bottom processing sliders? and only for the selected region.`
* **3.1 layout/scope follow-up applied:** `[x] Whole-scan measurement and display have been removed.
  The Rover now calculates metrics only when a non-dismissed region is selected. The compact
  two-line `SELECTED REGION R#` readout was moved out of the Rover sidebar and into the main analysis
  control panel immediately below the polarization, spectral, and surface sliders. With no selected
  region it asks for one; with 3.1 allocated OFF it explicitly reports measurement off.]`
* **3.1 focused follow-up check:** `[Confirm the Rover sidebar contains no metric block. Select no
  region and confirm the message below the sliders requests one. Select R1 and confirm only R1's
  metrics appear there. Select a different region and confirm the label switches targets without a
  cross-region delta. Adjust channels and processing levels and confirm values/deltas update for the
  selected region while the compact readout remains below the sliders.]`
* **3.1 focused follow-up result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: I don't understand all of the metrics used, and there are too many, I think we should collapse them all into a signal/noise ratio and show only that to the player, the rover knowing when all features within a region are fully visible should use that in the signal/noise ratio, if there is a glyph within the region and the glyph is 100% visible, the rover should display a signla-to-noise ratio of 1.00 ]`
* **3.1 single-ratio follow-up applied:** `[x] The player now sees exactly one selected-region
  measurement: `SIGNAL / NOISE`, formatted from 0.00 to 1.00 with an optional delta of that same
  value. The removed strength, continuity, background, structure, coverage, and primitive-count
  fields are no longer calculated or exposed. The observable ratio combines (1) completeness—the
  visible intensity-weighted fraction of every retained detected feature remembered by or currently
  inside the region—and (2) purity—the fraction of visible regional geometry accounted for by those
  retained features. A dismissed feature is treated as noise rather than signal. The ratio is 1.00
  when every retained feature is fully visible and every visible stroke is accounted for.]`
* **No-hidden-glyph clarification:** `[The Rover still cannot inspect whether a feature is secretly
  the true glyph. Therefore 1.00 means a completely visible, noise-free *observable candidate
  structure*, not proof of the puzzle's true glyph or solution. This preserves F-01. The same rule
  naturally yields 1.00 for a fully visible glyph only when its retained detected features account
  for all visible geometry in the selected region.]`
* **3.1 single-ratio Godot check:** `[Select a region and confirm only `SIGNAL / NOISE: 0.00–1.00`
  appears below the sliders. Reveal retained features progressively and confirm the value rises as
  their strokes become stronger/more complete; introduce unmatched visible strokes or dismiss a
  still-visible feature and confirm the value falls. On a region where every retained feature is
  fully opaque and all visible strokes are accounted for, confirm exactly 1.00. Return to the same
  configuration and confirm the same value.]`
* **3.1 single-ratio result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: two details to fix. 1. the features detected by the rover are sometimes one feature but it's almost the entire glyph, it should be more like individual strokes, not for cracks though, as they are almost one line with some turning between segments they should be mostly considered one feature, but it feels unnatural for the glyph, and sometimes a single feature will contain 2/3 of a glyph + 1 full vein, which is abnormal. 2, can you move the signal/noise ratio more on the right, it is over the last slider right now. Put this section in the same HBox as the sliders so that it behave well]`
* **3.1 latest review applied:** `[x] Endpoint proximity alone no longer combines observable
  primitives. A combined stroke must also have compatible direction (turns up to about 35°),
  intensity, and width. This keeps gently turning crack/vein chains together while splitting
  perpendicular glyph junctions and preventing visually dissimilar veins from being absorbed into
  a glyph feature. The detector still uses observable geometry only. The signal/noise readout is
  now the rightmost responsive column in the same HBox as the three processing sliders; all four
  columns expand and their minimum widths/separation were reduced to avoid overlap.]`
* **3.1 latest review check:** `[Re-scan several fragments. Confirm glyph candidates are generally
  individual continuous strokes, gently turning cracks remain mostly one feature, and no feature
  visibly combines most of a glyph with a dissimilar full vein. Resize/show the Rover panel and
  confirm the signal/noise column remains to the right of the sliders without overlaying them.]`
* **3.1 latest review result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

---

## 3.2 Interpret processing effects

### Player capacity

The player has **yellow** capacity.

The player can judge whether a processing change appears beneficial but may confuse a visually convincing distractor with genuine improvement.

### Rover support capacity

The Rover has **yellow** support capacity.

The Rover should interpret quantitative changes and indicate whether an adjustment appears to have improved or degraded the measurable signal.

### Rover performer capacity

The Rover has **green** capacity.

It can compare processing configurations consistently.

### Player support capacity

The player has **yellow** support capacity.

The player can use visual and semantic judgment to revisit previous Rover-generated interpretations.

### Implementation requirements

**Observability**

* Indicate whether the latest adjustment:

  * improved measured signal;
  * degraded measured signal;
  * produced little meaningful change.

Do not present this assessment as proof that the true puzzle solution has been found.

**Directability**

* Store previous tested configurations and their associated measurements.
* Allow the player to return to a previously tested configuration.

The history should include enough state to restore:

* filter toggles;
* filter levels;
* scan channels;
* rotation if relevant;
* associated Rover measurements.

#### Inline implementation plan — checkpoint 3.2

**Implementation status:** Implemented; awaiting Godot acceptance test. Checkpoint 3.1's two latest
review items were also addressed before this implementation.

**Capacity entry points:**

* `FragmentAnalysisUI.CaptureControlState()` returns an immutable configuration containing all six
  toggles, three levels, displayed rotation, and view state where needed.
* `FragmentAnalysisRover.RecordProcessingMeasurement(...)` stores the exact configuration, its
  signal/noise ratio, origin, target region, classification, delta, and monotonic sequence number,
  while deduplicating identical consecutive target/configuration pairs.
* A configurable 0.02 signal/noise threshold classifies each comparison as `Improved`, `Degraded`,
  or `LittleChange`. The visible copy consistently says **MEASURED CHANGE**, never that the true
  solution is closer or found.
* `FragmentAnalysisRover.RestoreProcessingConfiguration(...)` restores all six toggles, three
  levels, and rotation through the unified command path, then restores the entry's stored metric
  exactly instead of silently measuring and relabelling it as new.

The existing five-action back/forward controls continue to undo feature, region, processing, and
other actions and now snapshot the displayed measurement as well. A separate **TESTED
CONFIGURATIONS** selector retains up to 256 measured processing states, provides **RESTORE CONFIG**,
and lets the player bookmark entries. Restoring a tested configuration also reselects its region
when that region still exists and does not create a duplicate measurement entry.

**Automatic:** immutable state/history types, comparison thresholds, exact restore, panel/buttons,
serialization into the per-fragment state, and round-trip checks.

**Godot check:** no setup. Record at least three distinct configurations, navigate backward and
forward, close/reopen, and compare every control, displayed rotation, and metric value.

**Decision 3.2-A — retained history:**

* **A (recommended):** latest 256 distinct configurations per fragment; preserve accepted/manual
  bookmarks when pruning older unbookmarked entries.
* **B:** unbounded for the current level session.
* **C:** specify another limit: `[ entry count: ]`.
* **Answer:** `[ 3.2-A: A — assumed from the recommended default because no answer was entered. The
  limit is Inspector-configurable; bookmarked entries are never selected for pruning. ]`

**Implementation notes:**

* `[x] Processing history is deep-copied into `FragmentAutonomyState`, so F-04 close/reopen
  persistence includes configurations, metrics, effects, targets, and bookmarks.`
* `[x] Signal/noise and measured-change labels share the responsive rightmost processing column.`
* `[x] `MaximumHistoryEntries` defaults to 256; `ProcessingEffectThreshold` defaults to 0.02 and
  both can be tuned on the existing `FragmentAutonomySettings` resource in the Inspector.`
* `[x] Codex completed all code and scene wiring. Godot UI setup: none.`
* `[x] `dotnet build --no-restore --no-incremental` passes with 0 errors. The same three unrelated
  pre-existing warnings remain in `BuildingComponent.cs` and `SaveManager.cs`.`

**Gate test 3.2 (fill after implementation)**

* **Expected:** improved/degraded/little-change labels follow configured metric thresholds and make
  no truth claim; backward/forward restores toggles, levels, channels, rotation, and associated
  metrics exactly; closing/reopening follows F-04 without duplicate history entries.
* **Result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED the history and wording and label work, but look at the attached screenshot and you'll see that for a small region containing a full glyph, the optimal configuration I get where all features are clearly visible highlighted by the background and/or as active feature detected by the rover only yields a S/N of .14`
* **Observed/history entries checked:** `[Use one retained region. Note the baseline S/N, then make
  at least three changes across a level, channel toggle, and rotation. Confirm each distinct state
  appears once with baseline/improved/degraded/little-change wording. Select an older entry and
  press RESTORE CONFIG; verify every toggle, level, rotation, selected region, and shown stored S/N.
  Bookmark one entry. Close/reopen the same fragment and confirm entries/bookmark persist without a
  duplicate. Confirm the five-action arrows still undo/redo non-processing edits as before.]`
* **Build/scene/resolution:** `[Build passes; run the Godot check above and note layout or runtime
  results here.]`
* **Approved to implement dependency checkpoint 3.4:** `[ ] YES  [x] NO in side by side view we should have a lock next to "accepted" which allow to fix the rendering of this view and tweak parameters for other regions`

**3.2 signal/noise calibration follow-up:**

* **Screenshot diagnosis:** `[x] The .14 result was not consistent with the agreed meaning of the
  ratio. The first implementation multiplied raw rendered alpha into completeness and matched
  current primitives to remembered features only when both endpoints were almost identical.
  Consequently, fully readable semi-transparent strokes and a continuous line replacing a dashed
  line were incorrectly counted as mostly absent.]`
* **Correction applied:** `[x] Completeness is now geometric stroke coverage. Each remembered
  retained feature is sampled along its path and matched to observable, direction-compatible
  current geometry within a small tolerance. This handles dashed-to-continuous reconstruction and
  no longer mistakes render alpha for percentage visibility. Features contribute equally so one
  long crack cannot overwhelm a compact glyph. Visible geometry belonging to explicitly dismissed
  features remains the noise penalty. Thus all retained features fully covered, with dismissed
  geometry suppressed, reports 1.00—without identifying which candidate is the hidden true glyph.]`
* **History correction:** `[x] A discrete processing change is entered into the five-action history
  after its debounced measurement, so the snapshot contains the new stored S/N rather than the
  preceding value and is not duplicated.]`
* **Focused retest:** `[Return to the configuration pictured in the attached screenshot. Confirm
  the fully covered retained glyph/features now approach or reach 1.00. Reduce processing until
  portions disappear and confirm the ratio falls. Restore the optimal tested configuration and
  confirm its stored ratio is reproduced exactly. If a dismissed feature remains visibly present
  in the region, confirm it prevents 1.00 until that geometry is suppressed or the disposition is
  restored.]`
* **Focused follow-up result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: When choosing for candidate region we should not yet see the feature sensing section (collapsed), when the region are accepted we should collapse the candidate region section and deploy the feature sensing section, each section should be collapsable and default hidden we will unveil as we do the sequence, so first will be region deployed all of the other should be collapsed]`

**3.2 progressive workflow-panel UI follow-up:**

* **Initial presentation:** `[x] `CANDIDATE REGIONS` is expanded when a new analysis workflow
  begins. `TESTED CONFIGURATIONS`, `REGION SEQUENCE`, and `FEATURE SENSING` show only their compact
  headers and start collapsed. The existing `TASK ALLOCATION` section also remains collapsed by
  default.]`
* **Progressive transition:** `[x] Completing candidate-region review automatically collapses
  `CANDIDATE REGIONS` and expands `FEATURE SENSING`, so feature controls are not presented before
  the region decision stage. If Back/Restore makes a region proposed again, the UI returns to the
  Candidate Regions stage and collapses Feature Sensing.]`
* **Directability:** `[x] Each workflow header is a button with an explicit `▶`/`▼` state. The
  player can expand or collapse Tested Configurations, Candidate Regions, Region Sequence, and
  Feature Sensing independently without changing analysis data or autonomy allocation.]`
* **Godot setup:** `[None. The scene hierarchy, header controls, signal wiring, and phase transition
  are authored automatically.]`
* **Progressive-UI retest:** `[Open a fresh analysis and confirm only Candidate Regions is expanded.
  Manually toggle every header and verify its controls hide/show independently. Return to Candidate
  Regions, complete all region decisions, and confirm it closes while Feature Sensing opens. Use
  Back or restore a region to Proposed and confirm the panel returns to the region stage. Close and
  reopen a completed fragment and confirm it opens at Feature Sensing rather than showing the
  already-completed candidate workflow.]`
* **Progressive-UI result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: much better for the UI now, still some changes required: side-by-side navigation menu and quit button should be on top of the side-by-side view, not in the side panel. when regions have been locked in side by side with a good config and the player goes back to normal view, the locked view should appear with an indicator, so that the player can inspect the locked good configuration from elsewhere than side-by-side view]`

**3.2 comparison-toolbar and normal-reference follow-up:**

* **Comparison-local controls:** `[x] Previous pair, current pair/page, next pair, and `QUIT
  SIDE-BY-SIDE` now occupy a reserved toolbar across the top of the comparison canvas. The old
  Region Sequence menu and separator are removed from the Rover side panel. A compact `COMPARE
  REGIONS` button in the analyzer header reopens the comparison whenever at least two retained
  regions are available.]`
* **Normal-view locked references:** `[x] Leaving side-by-side no longer hides the useful frozen
  results. Each locked region is redrawn over its corresponding normal-view region from the stored
  observable snapshot, including its stored feature annotations. The live rendering is obscured
  only inside that boundary, and a cyan `LOCKED REFERENCE · R#` frame makes the frozen state
  unmistakable. Unlocked regions continue displaying the current live configuration.]`
* **Interaction/persistence:** `[x] The locked normal-view region remains selectable through the
  existing region interaction. Unlock, resize, dismiss, undo/redo, and close/reopen continue using
  the same persistent locked-view state; there is no second reference model.]`
* **Godot setup:** `[None. Toolbar construction, event wiring, normal overlay rendering, clipping,
  and header entry control are automatic.]`
* **Focused retest:** `[Open side-by-side and confirm the side panel has no Region Sequence menu.
  Use the top-canvas arrows, lock a good region, then press QUIT SIDE-BY-SIDE. Confirm the exact
  frozen configuration remains visible at that region in normal view with a cyan LOCKED REFERENCE
  label while other regions react to parameter changes. Use COMPARE REGIONS in the main header to
  reopen comparison, unlock the reference, quit again, and confirm that region returns to live
  rendering.]`
* **Toolbar/reference result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.2 allocation-mode null-target regression:**

* **Observed error:** `[Pressing SUPPORT or PERFORM before a region was selected raised a
  `NullReferenceException` in `FragmentAnalysisRover.RefreshSignalMetrics(...)`.]`
* **Root cause/fix:** `[x] On the first measurement, both the absent previous target ID and absent
  current target ID compared equal as nullable values. That true branch then dereferenced the
  nonexistent previous report. The comparison now first requires a non-null previous report, and
  the target feature list also has an explicit empty-list fallback when no region exists.]`
* **Regression test:** `[Open a fresh fragment with no candidate region selected. Switch OFF →
  SUPPORT → PERFORM → OFF, then repeat after grouping/selecting a region. Expected: no exception;
  before selection the readout says Select a region, and after selection it shows the region S/N.]`
* **Regression result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.2 side-by-side reference-lock follow-up:**

* **Comment reviewed:** `[x] An accepted region in side-by-side view now shows a clickable `LOCK`
  control immediately beside its `ACCEPTED` badge. Locking captures that pane's current observable
  primitives, rendered colors/widths, feature annotations, and region framing. Its badge changes to
  `LOCKED`; clicking it again returns the pane to the live rendering.]`
* **Independent comparison behavior:** `[x] Processing/channel/rotation changes continue updating
  unlocked panes while each locked pane remains an exact visual reference. Selecting, accepting,
  or dismissing features remains independent from the rendering lock. Resizing or dismissing a
  region removes its stale lock because its comparison boundary is no longer valid.]`
* **Persistence/history:** `[x] Locked-view snapshots contain neutral observable data only, are
  deep-copied into the per-fragment Rover state, survive close/reopen under F-04, and are restored
  by the existing five-action undo/redo controls.]`
* **Godot setup:** `[None. Codex completed the code and runtime drawing/hit-area wiring.]`
* **Focused retest:** `[Open side-by-side with two accepted regions. Press LOCK beside the first
  region, select the second, and change several processing levels/channels. Confirm the first pane
  remains pixel-for-pixel stable while the second changes. Unlock the first and confirm it catches
  up to the live rendering. Lock it again, close/reopen the same fragment, and confirm it is still
  locked. Finally use Back/Forward and confirm the lock transition is reversible.]`
* **Reference-lock result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

---

## 3.3 Decide processing configuration

### Player capacity

The player has **yellow** capacity.

The player can search processing configurations manually but may not efficiently explore the full parameter space.

### Rover support capacity

The Rover has **yellow** support capacity.

It may recommend promising parameter changes based on observed processing history.

### Rover performer capacity

The Rover has **yellow** capacity.

It may autonomously search processing configurations but cannot reliably determine that a high-scoring reconstruction is semantically correct.

### Player support capacity

The player has **yellow** support capacity.

The player should be able to constrain Rover search using their own visual interpretation.

### Implementation requirements

**Predictability**

* Before changing the processing configuration autonomously, the Rover must announce which parameter it intends to adjust next.

Examples:

* Polarization level;
* Spectral level;
* Surface Topography level;
* processor enabled state;
* Electromagnetic channel;
* Resonance channel;
* X-Ray channel.

**Directability**

* The player must be able to pause/resume skip backward/forward autonomous configuration search.
* The player must be able to **lock parameters**.

A locked parameter must not be modified by autonomous search until the player unlocks it.

The autonomous-search algorithm must therefore operate on the subset of currently unlocked parameters.

#### Inline implementation plan — checkpoint 3.3

**Implementation status:** Implemented; awaiting Godot acceptance test. The narrow checkpoint 3.4
dependency (one visible command path, preview/apply, manual override, and cancellation) was completed
with this checkpoint so the planner has no direct Canvas mutation path.

**Capacity entry point:** `FragmentConfigurationSearch.PlanNextAdjustment(...)` consumes current
observable metrics, tested history, rejected/skipped candidates, and locks. It performs a
deterministic, one-parameter-at-a-time coordinate search with backtracking: test neighbouring
levels/toggles, retain measurably useful branches, explore untried alternatives, and avoid repeated
configurations. It never receives the puzzle or hidden correct values.

The plan result identifies exactly one next parameter, old/new value, rationale, and expected
measurement goal. Supporter mode only recommends and waits for `APPLY`; Performer mode announces,
waits the agreed preview interval, then calls the checkpoint 3.4 executor. `PAUSE/RESUME`, history
back/forward or skip, and per-parameter locks all cause a fresh plan over the currently unlocked
subset.

**Automatic:** search state machine, deterministic candidate ordering, locks beside existing
controls, pause/resume/history/skip UI, transparency copy, persistence, and search invariants.

**Implemented:** `FragmentConfigurationSearch` performs deterministic coordinate search using only
the current visible control state and the selected region's measured S/N history. It proposes one
toggle or one level step at a time, avoids tested and skipped configurations, and backtracks one
step at a time toward the best measured branch when the current branch is exhausted. Its public API
does not receive `FragmentPuzzle`, truth, glyph, correct settings, or semantic line roles.

The editable `FragmentAutonomyPanel.tscn` now contains a `CONFIGURATION SEARCH` section with an
explicit `START/STOP SEARCH`, announced plan and rationale, `APPLY`, `SKIP`, tested-configuration
`BACK/FORWARD`, and six locks. `POL`, `SPEC`, and `SURF` each lock both that processor's enabled
state and its level; `EM`, `RES`, and `X-RAY` lock individual channels. Locks, skipped candidates,
and running-search state are deep-copied into the existing per-fragment persistence model.

Support mode always waits at `APPLY`. Perform mode announces the exact old/new value for the
exported one-second preview and then uses the shared UI command sink, so the existing controls visibly
change. S/N is remeasured before another plan is announced. Pause, OFF, a lock change, region change,
history navigation, or any manual processing change cancels the pending timer. Manual processing
changes pause active search; `BACK/FORWARD` restore the adjacent tested configuration and pause so
the restored state cannot be immediately overwritten.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the three warnings are the pre-existing nullable-annotation warnings in `BuildingComponent.cs` and
unused-variable warning in `SaveManager.cs`. The hidden-field source guard passes. No automated test
framework was added under decision F-03.

**Godot check:** no setup. Select a retained region with an S/N measurement. In Support mode, press
`START SEARCH`; verify the announced parameter remains unchanged until `APPLY`, then verify exactly
that existing control changes. Use `SKIP` and confirm the announced configuration is not applied or
immediately proposed again. Lock `POL`, `SPEC`, `SURF`, `EM`, `RES`, and `X-RAY` in turn and confirm
neither member of a locked processor group nor a locked channel is proposed. Use `BACK/FORWARD` and
confirm tested configurations are restored and search pauses. Resume, then manually change the
announced control and confirm the player value wins and search pauses. Finally repeat in Perform
mode: each proposal must remain visible for about one second, apply once, wait for S/N, and only then
announce another step. Close/reopen once and confirm the locks and tested/skipped search state persist.

**Decision 3.3-A — search strategy:**

* **A (recommended):** explainable coordinate search, one parameter per step, with metric-guided
  backtracking and no hidden target.
* **B:** systematic exhaustive search of up to 1,728 configurations. This is complete but slow and
  harder to coordinate with a player.
* **C:** randomized exploration with a reproducible seed.
* **Answer:** `[ 3.3-A: A]`

**Decision 3.3-B — lock granularity:**

* **A (recommended):** one lock covers both enabled state and level for each processor; one lock per
  scan channel.
* **B:** separate enabled-state and level locks for each processor.
* **Answer:** `[ 3.3-B: A]`

**Decision 3.3-C — backward/forward wording:**

* **A (recommended):** `BACK` restores the previous tested configuration; `FORWARD` redoes a state
  after Back when available; `SKIP` separately rejects the announced candidate and plans another.
* **B:** Back/Forward move through planned candidates without restoring history.
* **Answer:** `[ 3.3-C: A]`

**Decision 3.3-D — performer pacing:**

* **A (recommended):** exported 1.0-second preview before each automatic change, reset whenever the
  next action changes.
* **B:** performer advances only when the player presses `STEP`.
* **C:** specify another preview duration: `[ seconds: ]`.
* **Answer:** `[ 3.3-D: A in performer mode, B in supporter mode (human-driven)]`

**Gate test 3.3 (fill after implementation)**

* **Expected:** every plan is announced before execution; locked parameters never change; repeated
  candidates are avoided unless backtracking requires them; pause/resume/back/forward/skip all have
  the selected semantics; Supporter never applies; Performer respects preview and override; no
  hidden field participates in planning.
* **Result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The right panel should not be scrollable horizontally we should see all of it. The controls for 3.3 should live just above the manual controls for processing/channels, as a one-liner to not take too much vertical space.`
* **Observed/search steps and locks tested:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 4.1:** `[ ] YES  [x] NO`

**3.3 compact-toolbar follow-up:**

* **Comment reviewed:** `[x] The configuration-search controls no longer live in the Rover side
  panel. They are now a single compact toolbar directly above the existing manual processor/channel
  row, so the locks and actions are visually adjacent to the parameters they govern without using
  additional panel height.]`
* **Toolbar contents:** `[x] The one-line bar contains the current proposal, tested-history
  Back/Forward, Start/Stop, Supporter Apply, and Skip. The proposal uses horizontal ellipsis if the
  window becomes too narrow; no controls wrap into a second row.]`
* **Panel width correction:** `[x] Horizontal scrolling is disabled in the Rover panel. Generated
  processing-history, region, and feature dropdown entries no longer request the width of their
  longest item, eliminating the hidden overflow while retaining vertical scrolling.]`
* **Implementation verification:** `[x] dotnet build --no-restore --no-incremental passes with 0
  errors; the same three unrelated existing warnings remain. git diff --check passes.]`
* **Focused retest:** `[On both the laptop and QHD displays, open the Rover panel and confirm there
  is no horizontal scrollbar and every panel control is reachable. Confirm the single ROVER SEARCH
  toolbar appears immediately above the manual processing/channel controls without wrapping. In
  Support mode, select a region, press START, test one lock, SKIP, APPLY, Back, and Forward, and
  confirm the behavior from the original 3.3 gate is unchanged.]`
* **Focused follow-up result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.3 inline lock-icon follow-up:**

* **Requested presentation:** `[x] Removed the separate lock group from the search toolbar. Each
  manual processor/channel CheckButton now has its own adjacent 28×28 pixel-art lock toggle.]`
* **Assets/state:** `[x] `assets/ui/locked_opened.png` represents an unlocked parameter and
  `assets/ui/locked_closed.png` represents a locked parameter. The icon is nearest-filtered and
  scaled from the supplied 64×64 source. Its tooltip names the affected parameter, reports the
  current state, and explains the next click.]`
* **Semantics unchanged:** `[x] The lock beside Polarization, Spectral, or Surface covers both its
  enabled state and level. Electromagnetic, Resonance, and X-Ray each have an independent channel
  lock. Existing persistence, replanning, and manual override behavior is unchanged.]`
* **Focused retest:** `[Confirm every manual processor/channel control has one adjacent open-lock
  icon. Click each icon and confirm it changes to the closed-lock asset, updates the LOCKED status,
  and prevents that parameter (including the associated level) from being proposed. Click again to
  unlock. Confirm the ROVER SEARCH bar contains no duplicate lock menu.]`
* **Inline-icon result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.3 long-search freeze safety follow-up:**

* **Observed:** `[A complete game freeze occurred in Performer search after approximately 50
  configurations while the two-region comparison view was open.]`
* **Loop audit:** `[x] `FragmentConfigurationSearch` contains no unbounded planner loop: its loops
  are bounded by stored history and a finite list of at most twelve immediate neighbours. The
  union-find loop in feature grouping also terminates because parents only point toward roots.
  However, zero preview plus zero measurement debounce could previously form a synchronous
  apply → measure → plan recursion if those exported settings were changed from their defaults.]`
* **Likely cumulative pressure:** `[x] Each processing trial recaptured observable geometry,
  regrouped features, measured retained feature segments against visible segments, refreshed the
  side-by-side view, cloned short undo history, and rebuilt the tested-configuration dropdown. The
  retained reviewed-feature set can grow across changing configurations, so the S/N matching cost
  was not strictly bounded even though the planner itself was.]`
* **Hard guards added:** `[x] Planning, applying, and measurement now have re-entrancy guards;
  autonomous preview and measurement always cross at least one non-zero delay; an executing action
  has a five-second watchdog; planning has a 25 ms budget check; repeating the exact same state
  transition more than twice triggers a safety pause; and Performer pauses for review after 40
  continuous tests. RESUME starts a fresh 40-test work interval without discarding tested history.]`
* **Work bounds added:** `[x] Feature detection ranks and limits expensive pairwise grouping to 384
  observable primitives. S/N measurement considers at most 384 visible segments and aborts after
  500,000 comparisons. Budget exhaustion is not recorded as a measurement: it displays
  `S/N MEASUREMENT SAFETY LIMIT`, pauses search, and keeps the UI responsive.]`
* **Cumulative UI reduction:** `[x] The tested-configuration OptionButton is rebuilt only while its
  section is expanded. Side-by-side scan refreshes requested by a parameter event are deferred and
  coalesced with the feature refresh from the same change.]`
* **Configurable defaults:** `[MaximumContinuousSearchSteps = 40;
  MaximumRepeatedSearchTransition = 2; MinimumAutonomousStepDelaySeconds = 0.05;
  ProcessingActionTimeoutSeconds = 5; PlannerTimeBudgetMilliseconds = 25;
  MaximumMeasurementComparisons = 500,000.]`
* **Focused retest:** `[Reproduce the same two-region side-by-side Performer search. Leave TESTED
  CONFIGURATIONS collapsed. Expected: after 40 applied tests the Rover enters SAFETY PAUSE and the
  window, STOP, mode buttons, and manual controls remain responsive. Press RESUME and continue past
  50 total tested configurations. If a dense configuration reaches the measurement budget instead,
  expect S/N MEASUREMENT SAFETY LIMIT and a responsive paused UI. Record which safety message
  appeared and whether memory/CPU recovered.]`
* **Freeze-safety result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.3 extended-search safety follow-up:**

* **Observation:** `[x] A reported freeze followed an attempted increase of the uninterrupted search
  interval from 40 to 100 tests. The planner contains no unbounded loop: candidate generation,
  history traversal, feature grouping, and S/N comparisons are all bounded, and autonomous tests
  cross a frame delay. The 40-test pause is specifically the cumulative-work guard established after
  the earlier freeze near 50 configurations, so increasing the interval can re-enter that pressure
  range even though the search algorithm is still making finite progress.]`
* **Correction:** `[x] Forty tests is now an absolute ceiling for one uninterrupted work interval.
  Both the displayed limit and the executor use the same clamped value, and the Inspector range no
  longer advertises unsafe values above 40. RESUME resets only the interval counter and retains the
  measured/tested configuration history, so searches can still progress beyond 40 total tests with
  an explicit responsive checkpoint.]`
* **Focused retest:** `[Start Performer search with all parameters unlocked. Confirm the toolbar never
  reports a denominator above 40, the Rover enters SAFETY PAUSE at TEST 40/40, and STOP, RESUME, mode,
  and manual controls remain responsive. Press RESUME and confirm the counter restarts while prior
  tested configurations remain in history.]`
* **Extended-search safety result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.3 diversified branch-order follow-up:**

* **Observation:** `[x] The coordinate search previously enumerated neighbours in the fixed UI
  order Polarization → Spectral → Surface → Electromagnetic → Resonance → X-Ray. Starting from all
  OFF therefore favoured the controls on the left and could postpone useful combinations involving
  later channels.]`
* **Change:** `[x] Each planning step now gives all currently available, unlocked, untested
  one-parameter neighbours a reproducibly shuffled priority. The priority is keyed by the current
  configuration, target region, and number of tested/skipped configurations, so it changes as the
  search advances and mixes parameter families instead of walking the UI from left to right.]`
* **Predictability and safety:** `[x] This remains decision 3.3-A's observation-driven coordinate
  search: one announced parameter changes per step, locks and skipped/tested exclusions remain
  authoritative, and metric-guided backtracking is unchanged. The shuffle uses a stable local hash,
  not runtime randomness or hidden puzzle truth, so the same puzzle, target, controls, and history
  produce the same next proposal.]`
* **Focused retest:** `[Set every processor and channel OFF, retain/select a region, and start
  Performer search. Across the first several new tests, confirm proposals are drawn from different
  parameter families rather than always exhausting Polarization, Spectral, then Surface in display
  order. Confirm exactly one parameter still changes per test. Stop and repeat from the identical
  fragment/search state to confirm the order is repeatable; lock two parameters and confirm neither
  appears. A full-ON configuration may now occur earlier, but is not guaranteed because an S/N
  improvement can deliberately cause metric-guided backtracking.]`
* **Branch-order result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: The search appeared to stop
  after roughly 30 iterations without an obvious explanation.]`
* **Stop reviewed:** `[x] A Performer search intentionally enters `SAFETY PAUSE` after exactly 40
  applied tests, as configured by `MaximumContinuousSearchSteps`. This is the freeze guard added in
  the previous follow-up, not completion of the configuration space. `RESUME` begins another
  40-test work interval while preserving tested configurations and S/N history.]`
* **Clarity correction:** `[x] The compact search toolbar now displays `TEST current/limit` throughout
  a run. When paused it shows the Rover's actual pause reason—including `SAFETY PAUSE`—and explicitly
  says `PRESS RESUME TO CONTINUE`, instead of replacing the reason with generic `Search paused` text.
  The full message is also available as the label tooltip if narrow-screen ellipsis clips it.]`
* **Focused retest after clarification:** `[Start from all OFF and let Performer run uninterrupted.
  Confirm the counter reaches exactly TEST 40/40, the toolbar reports SAFETY PAUSE, and controls
  remain responsive. Press RESUME; confirm the counter resets for a new interval while the tested
  history remains, and diversified proposals continue. If it stops before 40, record the exact
  toolbar/status message because that indicates a different safety guard or true search completion.]`
* **Clarification retest result:** `[x] PASS  [] FAIL  [ ] BLOCKED REASON: fill after test ]`

**3.3 processing-slider hitbox follow-up:**

* **Change:** `[x] Polarization, Spectral, and Surface now use a 28-pixel minimum HSlider height.
  This enlarges each native Control rectangle—and therefore its complete click/drag hitbox—without
  changing values, ticks, step behavior, or adding custom input handling.]`
* **Scope:** `[x] These are the only HSlider nodes currently in the project. The setting lives on the
  slider nodes in `FragmentAnalysisUI.tscn`; the global sci-fi theme was left unchanged so other
  future sliders are not unexpectedly enlarged or visually distorted.]`
* **Focused retest:** `[Try clicking and beginning a drag several pixels above and below each thin
  visible track. Confirm all three sliders respond throughout the taller area, retain integer levels
  1–5, and do not overlap the label or the bottom edge of the analyser at laptop resolution.]`
* **Slider-hitbox result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

---

## 3.4 Adjust processing parameters

### Player capacity

The player has **green** capacity.

The existing controls remain usable manually.

### Rover support capacity

The Rover does not support execution of player parameter changes.

### Rover performer capacity

The Rover has **green** capacity.

It can programmatically manipulate all processing parameters.

### Player support capacity

The player has **yellow** support capacity.

The player must remain able to supervise and intervene during autonomous adjustment.

### Implementation requirements

**Observability**

* Every autonomous parameter change must be explicitly visible.
* Existing UI controls should update to reflect autonomous changes rather than maintaining a separate hidden Rover state.

**Predictability**

* The Rover must indicate the next intended adjustment before applying it.

**Directability**

* The player must be able to:

  * pause autonomous adjustment;
  * undo an adjustment;
  * manually change a parameter at any time.

Manual adjustment should override the relevant Rover action.

#### Inline implementation plan — dependency checkpoint 3.4

**Implementation status:** Implemented as the execution dependency of checkpoint 3.3; awaiting its
dedicated Godot acceptance test. No second parameter-search system will be added here.

**Difference from checkpoint 3.3:**

* **3.3 decides:** selects one untested adjustment, respects locks, announces it, compares measured
  S/N history, and chooses whether to explore or backtrack.
* **3.4 executes:** applies that already-selected adjustment through the same visible controls used
  by the player, identifies its origin, and guarantees pause, override, cancellation, and undo.
* **Result loop:** `3.3 plan → 3.4 preview/apply → 3.1 measure → 3.2 record/compare → 3.3 plan`.

**Implemented command boundary:** `FragmentAnalysisUI.DispatchAnalysisCommand(...)` is the single
mutation path for the three processor toggles, three levels, three scan channels, and later rotation
commands. A `FragmentAnalysisCommand` includes the parameter, value, and `Player`, `Rover`, or
`Restore` origin. The dispatcher updates the existing button/slider and `FragmentCanvas` renderer
endpoint together, refreshes its visible label, then emits one `FragmentAnalysisChange` containing
the previous/current control snapshots and origin. Its re-entrancy guard prevents programmatic UI
signals from being mistaken for player input.

**Implemented Rover execution:** checkpoint 3.3 publishes the exact proposed parameter and value,
waits the configured Performer preview, and calls only the `IFragmentAnalysisCommandSink`; the Rover
does not receive or mutate `FragmentCanvas`. After the command, it waits for the observable S/N
measurement before planning again. Supporter mode waits for player `APPLY` rather than executing on
a timer.

**Implemented intervention:** a player button/slider event enters the same dispatcher with `Player`
origin. If preview, execution, or search is active, `FragmentAnalysisRover.OnAnalysisChanged(...)`
cancels the pending adjustment, pauses search, reports `PLAYER`/`OVERRIDDEN`, and leaves the player's
new value intact. `PAUSE`, mode `OFF`, allocation changes, locks, and history navigation also cancel
the pending preview. There is no hidden Rover configuration to drift from the displayed controls or
from `FragmentAnalysisState` capture.

**Undo clarification:** the Rover panel's general `UNDO/REDO` restores complete pre/post action
snapshots, including processing controls, and pauses an active search before restoration. The compact
search toolbar's `BACK/FORWARD` has a different purpose: it navigates specifically among measured
tested configurations. Both restoration paths use `Restore` origin so they do not masquerade as a
new player override or Rover test.

**Automatic:** command adapter, origin/re-entrancy handling, visible current/next action, preview,
pause/cancel/undo, all existing-control synchronization, and per-parameter execution checks.

**Implementation verification:** the command sink exposes no puzzle/solution object to the Rover;
all nine processing mutations in `FragmentAnalysisRover` dispatch a typed command through that sink.
No Godot editor setup is required.

**Godot check:** use Performer with a retained region and start search. During separate announced
previews, manually toggle a processor, move a level slider, and toggle a scan channel; each player
value must remain and search must pause as `OVERRIDDEN`. Resume and allow Rover proposals covering a
processor toggle, level, and channel to apply; each existing widget and rendering must visibly change
exactly once after preview. Press `PAUSE` during another preview and confirm nothing applies. Allow
one Rover adjustment, press the general panel `UNDO`, then `REDO`, and distinguish those results from
the search toolbar's tested-configuration `BACK/FORWARD`. Close/reopen and verify displayed values
match the saved Canvas result.

**Gate test 3.4 (fill after implementation)**

* **Expected:** each Rover action changes the existing control visibly and exactly once; current and
  next action remain truthful; pause and undo work; any manual toggle/slider action wins and follows
  2.4-A; there is no separate hidden Rover configuration.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed (list parameter types/interruptions tested):** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 4.1:** `[x] YES  [ ] NO`

---

# 4. Discriminate the true signal

## 4.1 Sense reconstructed structures

### Player capacity

The player has **yellow** capacity.

The player can recognize emerging structured shapes but incomplete reconstruction and distractors may produce ambiguity.

### Rover support capacity

The Rover has **yellow** support capacity.

The Rover should detect and highlight coherent reconstructed structures.

### Rover performer capacity

The Rover has **yellow** capacity.

It can group lines into candidate structures but cannot guarantee that those structures belong to the true signal.

### Player support capacity

The player has **yellow** support capacity.

The player can correct Rover grouping based on perceptual judgment.

### Implementation requirements

**Observability**

* Highlight Rover-detected structures separately from raw feature detection.

A reconstructed structure may consist of multiple detected features grouped together.

**Directability**

* The player must be able to:

  * include additional strokes/features in a structure;
  * exclude strokes/features;
  * adjust or redefine a Rover-detected structure.

Rover structure detection must therefore use editable structure definitions rather than immutable detection output.

#### Inline implementation plan — checkpoint 4.1

**Implementation status:** Implemented; awaiting Godot acceptance test.

**Capacity entry points:** `FragmentStructureDetector.DetectStructures(...)` builds a graph from
active feature IDs and groups connected/coherent components without inspecting feature provenance or
puzzle roles. `FragmentAnalysisRover.ApplyStructureEdit(...)` handles selection and lifecycle;
`AddPlayerStructure(...)`, `ToggleSelectedStructureFeature(...)`, and `MergeStructures(...)` handle
create, include/exclude, merge, and arbitrary redefine operations while keeping stable structure IDs
and explicit feature membership. Disconnected memberships can be split/redefined by excluding them
from one definition and adding them to a new one.

Draw structure boundaries/combined paths in magenta, visually separate from cyan/green raw-feature
annotations. In `EDIT STRUCTURE` mode, clicking a visible feature toggles membership in the selected
structure; editing the definition never edits the underlying scan or original feature geometry.
Re-running detection may add proposals but cannot overwrite player-edited/accepted structures.

**Automatic:** graph detector, editable structure model, overlay, selected-structure panel/actions,
input mode, persistence, and membership invariant checks.

**Implemented detector:** `FragmentStructureDetector.DetectStructures(...)` builds a deterministic
undirected graph from non-dismissed feature geometry. Two nodes connect only when their normalized
segments touch or approach within the exported `StructureConnectionDistance` (default 0.025);
connected components meeting `MinimumStructureFeatureCount` (default 2) become proposals. Detection
is capped by `MaximumStructureFeatureCount` (default 256), although checkpoint 2.1 currently emits at
most ten feature groups. Confidence is the average observable feature confidence. The detector never
receives provenance, puzzle data, glyph identity, line roles, correct settings, or S/N truth.

**Implemented model and preservation:** structures have stable IDs, explicit feature-ID membership,
confidence, provenance, disposition, and an `IsPlayerEdited` guard. Selection and complete structure
definitions are deep-copied by `FragmentAutonomyState.Clone()`, so normal close/reopen and general
Undo/Redo preserve them. A new scan can replace unmatched untouched Rover proposals, but accepted,
dismissed, player-created, or membership-edited definitions are retained. Features referenced by an
accepted/edited structure are retained across processing-view changes; explicitly dismissing a
feature removes only that membership and does not edit scan geometry.

**Implemented UI and editing:** the Rover panel has a collapsible `RECONSTRUCTED STRUCTURES` section
with `SCAN STRUCTURES`, `OVERLAY`, selector, `NEW`, `EDIT`, `MERGE`, `ACCEPT`, `DISMISS`, and `RESTORE`.
Structures render as thick magenta dashed/solid paths behind the existing raw-feature annotations,
with `S#` labels; selected/editing state is brighter and labelled separately. `EDIT` changes the
cursor and makes a click on a visible feature toggle only its membership in the selected structure;
drag still pans, wheel still zooms, and Escape exits edit mode. Starting region drawing or entering
side-by-side mode exits structure editing to avoid gesture conflicts.

`NEW` creates an empty player definition and enters edit mode. `MERGE` is intentionally two-step:
select the target, press `MERGE`, then choose the source from the same selector; membership is united
into the target and the source becomes dismissed so `RESTORE` or general Undo can recover it. This
avoids an additional wide checklist or ambiguous automatic nearest-structure choice.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with 0 errors;
the same three unrelated pre-existing warnings remain. `git diff --check` and the structure-detector
hidden-field source guard pass. No Godot editor setup or imported art is required.

**4.1 startup wiring follow-up:** `[x] The first runtime attempt failed at
`CreateRoverPanel()` because the C# checkpoint 4.1 wiring was present while the current
`FragmentAutonomyPanel.tscn` on disk no longer contained its structure-control block. The missing
editable scene nodes were restored and all eleven unique-name references were verified. A targeted
fail-fast message now identifies a missing/out-of-date 4.1 panel scene directly instead of allowing
the later parent lookup to produce an ambiguous NullReferenceException.]`

**Godot check:** enable Support or Perform and expand `RECONSTRUCTED STRUCTURES`. Scan after visible
features exist and confirm magenta `S#` groups differ from cyan/orange/green feature marks. Select a
structure, enable `EDIT`, then click one member and one non-member: only magenta membership changes;
the feature disposition and scan line remain unchanged. Pan, zoom, and press Escape while editing.
Create a `NEW` structure, add at least two features, accept it, rescan, and confirm its membership is
not overwritten. Merge two proposals using `MERGE` then selecting the source; use `RESTORE` and
general Undo/Redo. Change processing controls, close/reopen the same fragment, and confirm the
accepted/edited definition remains aligned and selected. Dismiss a member feature and confirm it is
removed from the structure without affecting other members.

**Decision 4.1-A — first-pass structure editing UI:**

* **A (recommended):** canvas selection: choose a structure, then click highlighted features to
  include/exclude; provide `NEW`, `MERGE`, `ACCEPT`, and `DISMISS` buttons.
* **B:** feature-ID checklist in the Rover dock (precise but less spatially intuitive).
* **C:** both canvas selection and checklist.
* **Answer:** `[ 4.1-A: A]`

**Gate test 4.1 (fill after implementation)**

* **Expected:** detected structures visibly differ from raw features; include/exclude/redefine
  changes only membership; player edits are not overwritten by re-detection; accepted definitions
  survive close/reopen and remain aligned while navigating.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed/structure edits tested:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 4.2:** `[x] YES  [ ] NO`

---

## 4.2 Interpret glyph identity

### Player capacity

The player has **yellow** capacity.

Glyph recognition remains fundamentally a human semantic interpretation task.

### Rover support capacity

The Rover has **yellow** support capacity but may not identify the glyph itself.

Its role is to provide reference information that assists human interpretation.

### Rover performer capacity

The Rover has **red** capacity.

The Rover must not autonomously declare the glyph to be Hominid, Key, Television, or another semantic identity.

### Implementation requirements

**Observability**

* Provide the player with a **fragment overview/reference** showing the known fragment/glyph forms or equivalent visual reference material.

This should assist comparison between the reconstructed structure and known fragment identities.

Do not automatically label the reconstructed glyph as the correct variant.

#### Inline implementation plan — checkpoint 4.2

**Implementation status:** Implemented; awaiting the gate test. Following the answers below, this
is a single scanned-fragment overview rather than a glyph catalogue.

**Capacity entry point:** `MonolithFragment.FragmentTexture` exposes the texture already displayed
by the selected physical fragment. `FragmentAnalysisUI` passes that texture directly to one
pixel-filtered `TextureRect` in the collapsible `SCANNED FRAGMENT` section. The image does not enter
`FragmentAnalysisRover`, the observable scan, metrics, structures, or search history. There is no
glyph name, match, confidence, automatic selection, or answer output.

This preserves the intended human interpretation boundary: the player can visually compare the
whole fragment they collected with reconstructed structures, while the Rover gains no semantic
identity input. Reloading the generated scan leaves this physical-fragment reference unchanged.

**Automatic:** implemented the texture exposure, editable panel nodes, nearest-neighbour display,
collapsible section, missing-image fallback, and analyser wiring. No Godot editor setup or new art
is required.

**Godot check:** open the analyser, expand `SCANNED FRAGMENT`, and confirm it shows one enlarged
pixel-art picture matching the fragment in the world. Close/reopen the section, switch Rover modes,
and reload the generated scan; none of those actions should alter the analysis or displayed source
fragment. If possible, restart Level 3 until another fragment variant is assigned and confirm the
overview follows that variant rather than showing multiple references.

**Decision 4.2-A — reference material:**

* **A (recommended):** code-drawn canonical glyph diagrams, matching the generated line geometry
  without exposing the current identity.
* **B:** use the three existing monolith-fragment texture variants as reference cards.
* **C:** provide new reference artwork; attach the assets and I will wire/import them automatically.
* **Answer:** `[ 4.2-A: Simpler than that, the fragment that we scan has a texture onto which the right glyph is present, show the "picture" of the whole fragment to the player here]`

**Decision 4.2-B — reference set:**

* **A (recommended):** show only the three known true forms equally.
* **B:** also show known distractor forms, explicitly labelled as a broader visual catalogue.
* **Answer:** `[ 4.2-B: only show the one we already scanned]`

**Gate test 4.2 (fill after implementation)**

* **Expected:** exactly one enlarged image of the already-scanned physical fragment is visible;
  it matches the world fragment, has no identity label/ranking, does not change across generated
  scan reloads or Rover modes, and opening/closing it does not affect analysis state.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed/variants checked:** `[ fill after test ]`
* **Build/scene/resolution:** `[ dotnet build: PASS (3 unrelated existing warnings); fill runtime resolution ]`
* **Approved to implement 4.3:** `[x] YES  [ ] NO`

---

## 4.3 Decide whether a candidate is valid

### Player capacity

The player has **yellow** capacity.

The player is responsible for judging whether a reconstruction appears to be the genuine signal rather than a distractor.

### Rover support capacity

The Rover has **red** support capacity for this decision.

### Rover performer capacity

The Rover has **red** capacity.

### Implementation requirements

No Rover validity decision should be implemented.

The Rover may expose the scan, measurements, highlighted structures, or other information generated by previous capacities, but it must not output:

* "true signal";
* "correct candidate";
* "solution found";

unless the existing puzzle-solving system independently triggers its normal solved condition.

Semantic candidate validity remains a player responsibility.

#### Inline implementation plan — checkpoint 4.3 (guardrail)

**Implementation status:** Implemented; awaiting the gate test. This checkpoint intentionally adds
no Rover validity function or validity state.

`FragmentCandidateValidityPolicy` is now the single player-facing vocabulary boundary. Structure
proposals display as `CANDIDATE`; the former structure `ACCEPT` action is labelled `RETAIN`, and its
result is displayed as `PLAYER RETAINED`. Retaining means only that the player chose the candidate
for further review. Dismissed/restored structures likewise remain review actions rather than truth
claims.

Every Rover CURRENT/NEXT/TARGET/RESULT string passes through the policy before display. The policy
blocks `true signal`, `correct candidate`, `valid candidate`, `solution found`, `glyph identified`,
`correct glyph`, and `puzzle solved`, reports the attempted violation, and substitutes `PLAYER
REVIEW REQUIRED`. This is a defensive presentation guard, not a classifier.

The private immutable `FragmentAutonomyTruth` selected in F-02 remains available to the coordinator,
but checkpoint 4.3 adds no `truth.*` read. The Rover does not subscribe to `PuzzleStateChanged`, call
`IsPuzzleSolved`, or receive the solved predicate. The existing independent `FragmentAnalysisUI`
solved lifecycle remains unchanged and cannot steer Rover search or candidate disposition.

**Automatic:** neutral vocabulary policy, guarded status rendering, explicit player-retention copy,
and static dependency/source checks. No automated test framework or deterministic test scene was
added under F-03; the generated distractor geometry provides the manual scenario below.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with zero
errors and the same three unrelated existing warnings. `git diff --check` passes. Source checks find
no `truth.` read, `IsPuzzleSolved`, `PuzzleStateChanged`, or forbidden conclusion literal in the
Rover/planner/detector/measurer modules.

**Godot check:** scan and retain a visually convincing or high-S/N reconstructed structure. Confirm
the button says `RETAIN`, the selector/summary says `PLAYER RETAINED`, and status says that the
player retained a *candidate structure for review*. Switch between Off, Support, and Perform and
confirm no candidate-validity action or conclusion appears. If possible, retain a distractor-looking
structure and confirm its high S/N does not change that wording. Finally solve the puzzle manually:
the normal sample lifecycle may show `SOLVED`, but the Rover status must not announce a solution or
change the retained candidate into a correctness claim.

**Gate test 4.3 (fill after implementation)**

* **Expected:** a distractor can be highlighted and measured without any Rover correctness claim;
  player acceptance is not treated as truth; the existing puzzle solved condition alone detects the
  actual solution; Rover planning has no dependency on it.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Distractor/solution configuration observed:** `[ fill after test ]`
* **Build/scene/resolution:** `[ dotnet build: PASS (3 unrelated existing warnings); fill runtime resolution ]`
* **Approved to implement 5.1:** `[x] YES  [ ] NO`

---

# 5. Orient the figure

## 5.1 Interpret upright orientation

### Player capacity

The player has **yellow** capacity.

The player can usually recognize upright orientation but ambiguous or incomplete geometry may make this uncertain.

### Rover support capacity

The Rover has **yellow** capacity.

It may provide geometric orientation cues.

### Rover performer capacity

The Rover has **yellow** capacity.

It may propose an upright orientation but cannot guarantee semantic correctness.

### Player support capacity

The player has **yellow** capacity.

The player can approve or reject the Rover interpretation.

### Implementation requirements

**Observability**

* Show Rover-generated orientation cues.

Possible representations include:

* vertical reference;
* candidate upright axis;
* orientation line;

**Predictability**

* ghosted proposed upright pose.

**Directability**

* The player must be able to accept or reject the Rover's proposed orientation.

A rejected orientation should not be treated as valid by subsequent autonomous rotation logic.

#### Inline implementation plan — checkpoint 5.1

**Implementation status:** Implemented; awaiting the gate test. Depends on a selected non-dismissed
structure with at least one visible member feature.

**Capacity entry point:** `FragmentOrientationEstimator.EstimateHypotheses(...)` uses only selected
structure geometry: length- and feature-confidence-weighted dominant line-axis distributions. It
returns hypotheses with an axis, signed proposed pose, confidence, evidence summary, and stable ID.
It does not receive current glyph identity or `CorrectRotationDegrees`; polarity that cannot be
inferred geometrically remains explicitly ambiguous for the player.

Draw a fixed vertical reference, the candidate upright axis, and a ghosted pose in the overlay.
`ApplyOrientationEdit(Accept, id)` records the player-approved hypothesis;
`ApplyOrientationEdit(Reject, id)` records its rejection and clears any correction derived from it. Rejected
hypotheses cannot be silently reused by checkpoints 5.2–6.2 unless geometry materially changes or
the player explicitly restores them.

**Implemented:** `FragmentOrientationEstimator.EstimateHypotheses(...)` receives only the selected
structure, its currently observable feature segments, the visible sample size, and the configured
Yellow reliability. Length- and feature-confidence-weighted line directions produce a dominant
axis, its explicit 180-degree polarity alternative, and a distinct secondary axis (or perpendicular
fallback). Each stable `H#` reports signed axis degrees, confidence, weighted-support evidence, and
`POLARITY AMBIGUOUS`; it does not receive `FragmentAutonomyTruth`, glyph identity, correct rotation,
semantic roles, or the solved predicate.

The new collapsible `UPRIGHT ORIENTATION` panel provides `ESTIMATE AXES`, a three-alternative
selector, evidence text, overlay toggle, and `ACCEPT / REJECT / RESTORE`. The overlay draws a fixed
white vertical reference, a directional candidate axis with arrowhead, and a translucent dashed
copy of the selected structure rotated into that proposed upright pose. Candidate, player-accepted,
and rejected cues use visibly different colours. Cues are hidden when this capacity is effectively
Off.

Selected/accepted IDs, disposition, source structure, geometry signature, evidence, and alternatives
are deep-copied into the existing per-fragment state. Re-estimating unchanged geometry preserves
rejections instead of silently proposing them again. Selecting another structure, editing
membership, merging structures, removing a member feature, rescanning structures, or materially
changing member geometry or its Yellow-reliability setting clears every derived
orientation—including the accepted ID—so checkpoint
5.2 cannot consume stale or rejected work. Undo/Redo and close/reopen preserve valid hypotheses.

**Automatic:** estimator, confidence/evidence model, cue/ghost overlay, editable panel nodes,
accept/reject/restore actions, persistence, history restoration, invalidation guards, and source
checks. No automated test framework or synthetic test scene was added under F-03.

**Implementation verification:** `dotnet build --no-restore --no-incremental` passes with zero
errors and the same three unrelated existing warnings. `git diff --check` passes. The estimator and
all orientation call sites contain no `truth.`, `GlyphType`, `CorrectRotationDegrees`,
`IsPuzzleSolved`, or `PuzzleStateChanged` read.

**Godot check:** select a reconstructed structure and expand `UPRIGHT ORIENTATION`. Press `ESTIMATE
AXES`; confirm up to three `H#` alternatives appear and changing the selector moves the directional
axis/ghost while the white vertical reference remains fixed. The first two should share evidence but
point 180 degrees apart, making polarity uncertainty visible. Toggle `OVERLAY`, reject one, select
and accept another, then press `ESTIMATE AXES` again without editing: the rejected item must stay
rejected. Use Restore and Undo/Redo. Accept one, close/reopen the fragment, and confirm its state and
alignment persist while panning/zooming. Finally add/remove a member using structure `EDIT`: every
orientation proposal and accepted ID must clear until axes are estimated again.

**Decision 5.1-A — ambiguous hypotheses:**

* **A (recommended):** show up to three geometric alternatives with confidence/evidence; the player
  chooses one or rejects all.
* **B:** show only the highest-ranked hypothesis, then offer the next after rejection.
* **C:** show a single editable axis with no ranked alternatives.
* **Answer:** `[ 5.1-A: A]`

**Gate test 5.1 (fill after implementation)**

* **Expected:** cues and ghost are visually distinct; hypotheses are geometry-derived and describe
  uncertainty; accepted/rejected state persists; rejection prevents use by later rotation logic;
  changing the structure invalidates stale derived proposals; no hidden rotation/glyph field is read.
* **Result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED`
* **Observed/hypotheses tested:** `[ fill after test ]`
* **Build/scene/resolution:** `[ dotnet build: PASS (3 unrelated existing warnings); fill runtime resolution ]`
* **Approved to implement 5.2:** `[ ] YES  [x] NO REASON: There is a problem with previous implementation, especially the feature sensing and reconstructed structures, as a player I can see the full glyph almost perfectly while only a fifth of the strokes will be caught by the rover, which create a problem that the detected structure is almost nothing while the rest of the glyph is visible, and thus the orientation is sketchy, as it should take into account almost or the whole glyph. There should be maybe at least a way where +-1 best config of a glyph, the rover can fully reconstruct it in features --> in structures --> propose rotation transformations `

**5.1 visible-geometry reconstruction follow-up:**

* **Cause found:** `[x] The feature detector used its `Segments` collection both as the downstream
  geometry contract and as a compact overlay highlight, but deliberately retained only 40% of the
  primitives in a complex connected group. It also kept only the ten highest-scoring feature groups.
  Consequently, structure reconstruction and orientation estimation received the same partial
  geometry the player observed in the overlay even when many more strokes were currently visible.]`
* **Correction:** `[x] Every observable primitive belonging to a retained feature is now preserved in
  that feature's geometry and overlay. The bounded feature-group ceiling is raised from 10 to 48 so a
  multi-stroke glyph can survive into structure reconstruction. The existing strongest-384-primitive
  cap remains the pairwise-work safety boundary. No glyph identity, correct configuration, hidden
  line role, or correct rotation is exposed or consulted.]`
* **Focused retest:** `[Move to the best measured configuration, then also test each configuration
  differing from it by one processing/channel step. Press SCAN FEATURES, inspect the overlay, press
  SCAN STRUCTURES, select the structure covering the glyph, and press ESTIMATE AXES. Expected: when
  the full glyph is visibly reconstructed, nearly all of those visible glyph strokes remain present
  through feature and structure overlays and therefore contribute to the orientation ghost. Some
  visible noise may remain as separate candidate features/structures and must not be labelled as
  semantically incorrect by the Rover.]`
* **Reconstruction retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: fill after test reconstructed structure are barely visible compared to feature it should collapse features into a structure and thus apply a new entire color to that (maybe magenta), in upright orientation mode, it should show only the reconstructed structure isolated on a black background in normal or side-by-side mode, and not only propose 3 hypothesis, but after 5, remove visually the hypothesis and let the player decide.]`

**5.1 orientation-isolation presentation follow-up:**

* **Change:** `[x] Expanding UPRIGHT ORIENTATION now enters a dedicated review presentation for the
  selected reconstructed structure. The normal canvas is covered by black and every non-dismissed
  member stroke is redrawn as one solid, high-contrast magenta structure, fitted and centred as a
  complete object. Feature numbers, individual feature colours, regions, noise, navigation marks,
  and unrelated structures are suppressed in this presentation.]`
* **Side-by-side:** `[x] If comparison view is open, each pane likewise suppresses its raw scan and
  individual annotations and draws only the selected structure's member strokes that intersect that
  region, using the same solid magenta treatment. Closing UPRIGHT ORIENTATION restores the prior
  normal or comparison rendering without changing analysis state.]`
* **Five-second decision preview:** `[x] The selected hypothesis axis, vertical reference, and rotated
  ghost are shown for five seconds after hypotheses are generated, a different hypothesis is selected,
  the overlay is re-enabled, or orientation review is reopened. They then disappear while the isolated
  magenta structure and all selector/evidence/ACCEPT/REJECT controls remain, leaving the final decision
  to the player. Selecting an alternative deliberately starts a new five-second preview.]`
* **Focused retest:** `[Select a reconstructed glyph structure and expand UPRIGHT ORIENTATION in normal
  view. Confirm only a centred solid-magenta structure appears on black, with no feature labels or raw
  scan. Estimate axes and confirm the reference/axis/ghost disappear after about five seconds while the
  structure and decision controls remain. Select another H# and confirm its five-second preview. Open
  side-by-side and confirm both panes isolate the same selected structure. Collapse orientation and
  confirm the original scan/overlays return.]`
* **Isolation retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: the glyph seems distorted in the upright orientation isolation from background, the ghost for rover-proposed orientation is only visible a couple second which is not normal. And if the player reject all 3 hypothesis then it cannot see those hypothesis ever again, not normal. The ghost should not be displayed overlaid but as an animation tween offset to the right, then rotate to show effectively what is going to happen after rotation. There should be five rover upright orientation hypothesis, with one being the true one always.]`

**5.1 animated five-hypothesis follow-up:**

* **Clarification applied:** `[x] “After 5” is now interpreted as five hypotheses, not a five-second
  visibility timeout. Orientation cues and rejected alternatives no longer expire. All five remain in
  the selector; selecting any candidate—including a rejected one—shows its presentation again, and
  RESTORE returns a rejected candidate to the decision set.]`
* **Undistorted isolation:** `[x] Isolation fitting now converts normalized geometry back into sample
  pixel space before calculating bounds and scale. A single uniform scale is then used, preserving the
  original sample aspect ratio rather than stretching normalized X and Y equally.]`
* **Separated animated proposal:** `[x] The current magenta structure is fitted in the left half of the
  black review canvas. A separate ghost starts in the same pose in the right half and tweens through
  the selected correction to upright over 1.2 seconds. The ghost remains visible at its final pose;
  changing H# restarts the animation. It is no longer drawn directly over the source structure.]`
* **Five geometric alternatives:** `[x] The estimator now emits up to five proposals: dominant and
  reversed-dominant polarity, secondary and reversed-secondary polarity, and a tertiary observed axis
  or diagonal fallback. They remain ranked solely from visible structure geometry.]`
* **Guaranteed candidate under F-02:** `[x] Updated after testing: the resolved F-02 decision explicitly
  permits a private Wizard-of-Oz truth snapshot while requiring player-facing behavior to appear
  observational. Candidate generation therefore ensures one of the five unlabelled H# proposals
  reaches the required rotation. It is not identified, ranked, or presented as correct, so the player
  must still compare and choose. This does not make the Rover announce a solved orientation.]`
* **Focused retest:** `[Expand UPRIGHT ORIENTATION and confirm the left structure is not stretched.
  Estimate axes: confirm five H# entries appear, the selected ghost animates separately on the right
  and remains visible after completing its rotation. Reject all five, then select each rejected H# and
  confirm its ghost is still viewable; use RESTORE on one and confirm it becomes a candidate again.
  Compare each proposal visually, without expecting the Rover to identify the true semantic upright.]`
* **Animated-orientation retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The rotation should be able to be launched from compare region and thus a region must be selected for rotation, we can switch to region 2, region 3 etc.. It should be able to do that from region that has been locked in place even if the parameters differed since the locked.]`

**5.1 comparison-region orientation-source follow-up:**

* **Region is now authoritative:** `[x] ESTIMATE AXES now requires the currently selected, non-dismissed
  comparison region rather than a whole-fragment structure selection. Clicking R2, R3, and so on in
  side-by-side selects the exact source for the next orientation proposal. Changing region invalidates
  the prior hypotheses so a correction cannot silently carry across regions.]`
* **Locked historical geometry:** `[x] If the selected region is locked, orientation reconstruction,
  axis estimation, isolation rendering, and the animated proposal use a deep-copied snapshot of that
  region's features and sample scale from the moment it was locked. Later channel or processing changes
  do not alter or invalidate that proposal. An unlocked region uses the live visible geometry and is
  invalidated by a subsequent processing change.]`
* **Launch from comparison:** `[x] Pressing ESTIMATE AXES while comparison is open launches the dedicated
  black orientation animation over the comparison workspace for its selected region. The status names
  `R# · LOCKED` or `R# · LIVE`. Collapsing UPRIGHT ORIENTATION reveals the still-open comparison so a
  different region can be selected and launched; selecting another region also clears the prior source
  and returns to comparison.]`
* **State boundary:** `[x] The orientation source region, reconstructed member geometry, sample scale,
  and locked/live snapshot are deep-copied with fragment autonomy state. No current configuration is
  substituted while rendering a locked proposal.]`
* **Focused retest:** `[Open side-by-side, select R2, lock it, and press ESTIMATE AXES. Confirm the status
  identifies R2 · LOCKED and the isolated/animated geometry matches R2. Change processing parameters:
  the R2 proposal must remain unchanged. Collapse UPRIGHT ORIENTATION, select an unlocked R3, reopen and
  estimate; confirm the status identifies R3 · LIVE and its distinct reconstruction is used. Change a
  processing parameter and confirm the live R3 hypotheses clear while locked R2 can be selected and
  estimated again from its retained geometry.]`
* **Comparison-source retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: A player should not be able to accept features when in side by side view and the region concerned is not displayed currently, the rover should prioritize prompting features for regions that are already displayed, if all proposition are consumed, then it should show the new region concern. Structure are still invisible, when accepted, features underneath should disappear and a magenta new figure should be clearly visible, in normal and in side-by-side view. Side-by-side view should be compatible with structures. Side-by-side view currently do not show the orientation ghost]`

**5.1 page-scoped review and comparison-rendering follow-up:**

* **Visible-pair review priority:** `[x] While side-by-side is open, the Rover now selects pending
  feature proposals belonging to either displayed region first. Accept, dismiss, and restore are
  disabled and rejected for an off-page feature. Once the current pair has no pending proposals, the
  next proposal is selected and its region pair is brought into view automatically. Selecting a
  feature explicitly also brings its region into view before it can be edited.]`
* **Queue completion safety:** `[x] Updating the displayed pair no longer emits a feature change when
  the selected feature would remain unchanged. This prevents a recursive refresh when the current
  pair—or the complete review queue—contains no pending proposal.]`
* **Accepted-structure replacement:** `[x] Accepting a structure suppresses its individual member
  feature strokes and replaces them with one solid magenta structure figure. Accepted structures stay
  visible in the normal analyzer even when proposed-structure display is collapsed, and both proposed
  and accepted structures render in side-by-side panes using that pane's live or locked geometry.]`
* **Side-by-side orientation:** `[x] Orientation isolation is now limited to the selected source-region
  pane. That pane shows its magenta source structure, animated cyan rotated ghost, upright reference
  axis, and hypothesis label; the other pane remains a normal region comparison. The full-canvas
  orientation overlay no longer covers side-by-side view.]`
* **Automation:** `[x] Codex implemented the state/UI/render wiring. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[Open side-by-side with at least three retained regions. On the first pair,
  consume pending feature proposals and confirm the Rover stays on those two regions until their
  proposals are exhausted, then advances to the pair containing the next proposal. Confirm an
  off-page feature cannot be accepted/dismissed/restored. Accept a reconstructed structure and verify
  its member feature annotations disappear beneath one solid magenta figure in both normal and
  side-by-side views. Select a displayed region, open UPRIGHT ORIENTATION, estimate axes, and verify
  the selected pane shows the animated cyan ghost while the other comparison pane remains visible.]`
* **Page/structure/orientation retest result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: A locked region should not be editable in size in normal view, but it should be unlockable from normal view as well as side-by-side view. There is a problem with the way orientations are proposed. We only have accept/reject and when rejecting everything there is just one last option: restore, which will restore only the last one that we cain either accept/or reject. I woul prefer to see [<--] H1 [accept] [-->] to navigate between the different hypothesis ]`

**5.1 locked-region and hypothesis-navigation follow-up:**

* **Normal-view lock control:** `[x] The selected accepted region now has a full-width `LOCK` /
  `UNLOCK` control in CANDIDATE REGIONS. Its label and selected-region status reflect the current
  retained-view state, providing the same lock toggle without opening side-by-side.]`
* **Locked geometry protection:** `[x] A locked region cannot enter resize mode on double-click. If a
  region becomes locked while resize mode is active, that interaction is cancelled. `ResizeRegion`
  also rejects locked IDs, so saved bounds and locked geometry cannot be altered through another UI
  path. Unlocking restores normal resize behavior.]`
* **Orientation carousel:** `[x] Reject/restore controls and the orientation dropdown are replaced by
  `←  H#  ACCEPT  →`. The arrows wrap through all five hypotheses without changing their disposition,
  restart the animated ghost for the displayed candidate, and keep every candidate reachable.
  ACCEPT commits the displayed hypothesis; the player can continue browsing and replace that choice
  by accepting another hypothesis.]`
* **Automation:** `[x] Codex implemented the panel scene, controller wiring, interaction guard, and
  Rover validation. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[In normal view, select an accepted region and press LOCK. Confirm the label
  becomes UNLOCK, the status says VIEW LOCKED, and double-click/drag cannot resize it. Unlock it and
  confirm resizing works again. Verify the same state and toggle from side-by-side. Estimate
  orientation, then use ← and → repeatedly: confirm they wrap through all five H# proposals and each
  displays its animated ghost. Accept one, browse every other proposal, accept a different one, and
  confirm the newly accepted H# replaces the previous choice without losing access to any proposal.]`
* **Lock/carousel retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: We do not have any means of changing the region of interest in upright orientation menu, we should be able to switch between regions and do the upright orientation test. we should also be able to quit that view just like side-by-side and go back to normal fragment view]`

**5.1 orientation-region navigation follow-up:**

* **Region-of-interest carousel:** `[x] UPRIGHT ORIENTATION now begins with `←  R# · LIVE/LOCKED
  →`. It cycles through every non-dismissed region by ID, wraps at both ends, selects that region as
  the authoritative orientation source, and brings its comparison pair into view when side-by-side is
  open.]`
* **Region-safe hypotheses:** `[x] Changing R# clears the prior region's H# proposals and accepted
  orientation. The player then presses ESTIMATE AXES to generate five hypotheses from the newly
  selected region's live or locked observable geometry, so a proposal cannot silently cross region
  boundaries.]`
* **Explicit exit:** `[x] `QUIT ORIENTATION VIEW` collapses UPRIGHT ORIENTATION, closes side-by-side
  when it is open, removes orientation isolation, and returns directly to the normal fragment canvas.
  Existing features, structures, region locks, and processing history remain intact.]`
* **Automation:** `[x] Codex implemented the panel scene and controller wiring. Godot editor setup:
  none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[Open UPRIGHT ORIENTATION with three or more regions. Use the R# arrows in both
  directions and confirm they wrap, update LIVE/LOCKED correctly, and show the selected region's pair
  when comparison is open. On each R#, press ESTIMATE AXES and confirm a fresh H1–H5 set and ghost use
  that region only. Press QUIT ORIENTATION VIEW from an isolated side-by-side test and confirm the
  normal full-fragment canvas returns with annotations, locks, and history unchanged.]`
* **Orientation-region/exit retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The structures menu should be the same layout as the feature menu, when a structure has been dismissed, the orientation proposal should be done on features, not the dismissed structure.]`

**5.1 structure-review and dismissed-source follow-up:**

* **Feature-style structure review:** `[x] RECONSTRUCTED STRUCTURES now follows the FEATURE SENSING
  primary layout: scan/overlay, selected summary, selector, then `ACCEPT / DISMISS / RESTORE`.
  `NEW / EDIT / MERGE` remain available as secondary membership tools below the primary review row.
  The former `RETAIN` wording is now `ACCEPT`.]`
* **Sequential review:** `[x] ACCEPT and DISMISS automatically advance selection to the next proposed
  structure, wrapping through the stored list; the selection clears when no proposal remains.
  RESTORE selects the restored structure. Dismissed entries remain available in the selector just as
  dismissed features do.]`
* **Dismissed-source boundary:** `[x] Orientation estimation only adopts a selected structure when it
  is non-dismissed and contributes active visible features inside the selected region. A dismissed,
  empty, or out-of-region structure is ignored; the proposal instead uses a fresh geometry-only
  connected grouping derived from the region's active feature observations. Its dismissed ID,
  membership, provenance, and disposition are not copied into the orientation source.]`
* **Automation:** `[x] Codex implemented the scene ordering, review progression, and estimator guard.
  Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[Scan structures and confirm the primary controls appear in the same order as
  feature review: selector followed immediately by ACCEPT, DISMISS, RESTORE. Accept or dismiss several
  proposals and confirm selection advances to the next pending S#. Select a dismissed S# from the
  selector, open UPRIGHT ORIENTATION, select an R#, and press ESTIMATE AXES. Confirm the source and
  animated H# ghost are reconstructed from active features visible in that region, not from the
  dismissed structure. Repeat with an active structure that overlaps the region and confirm its active
  membership may be used.]`
* **Structure-layout/source retest result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: There is a failure to show the ghost in the orientation view if only one region is selected.]`

**5.1 single-region ghost follow-up:**

* **Cause corrected:** `[x] The normal orientation overlay previously looked up the hypothesis source
  only in the persistent reconstructed-structure list. A feature-derived fallback is intentionally a
  transient structure and therefore could not be found there; this affected the one-region path while
  side-by-side received the transient object directly.]`
* **Unified source rendering:** `[x] Normal and side-by-side orientation renderers now use the same
  retained `OrientationSourceStructure` and deep-copied feature snapshot. A single selected region can
  therefore show its magenta source, animated cyan H# ghost, and upright reference even when every
  reconstructed structure was dismissed.]`
* **Automation:** `[x] Codex implemented the renderer correction. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[Retain exactly one region, dismiss every reconstructed structure, select that
  R# in UPRIGHT ORIENTATION, and press ESTIMATE AXES. Confirm the normal fragment view displays the
  isolated magenta feature-derived source and animated cyan ghost for H1. Cycle H1–H5 and confirm each
  ghost renders. Then restore or accept a structure and repeat to confirm the active-structure source
  also renders.]`
* **Single-region ghost retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: ghost of orientation is still visible in normal view with the H2 upright arrow, not normal. Also it seems like it is never proposing the right orientation, it should have at least one hypothesis which is the right one]`

**5.1 orientation-exit and candidate-coverage follow-up:**

* **No normal-view cue leak:** `[x] H# ghosts, hypothesis labels, and upright reference arrows now draw
  only while the dedicated orientation isolation is active. Quitting or collapsing UPRIGHT
  ORIENTATION returns a normal annotated fragment view even when the orientation OVERLAY toggle
  remains enabled.]`
* **Stronger geometric coverage:** `[x] The five proposals now begin with both polarities of the
  source geometry's principal spatial axis, followed by distinct observed line-axis alternatives.
  This covers the upright axis implied by the pointy frame's overall extent, which was previously a
  bisector of its dominant edge directions and could be absent from H1–H5.]`
* **One valid proposal:** `[x] Consistent with the resolved F-02 autonomy-truth decision, generation
  checks whether the five observational candidates include the rotation required by the puzzle. If
  not, the last non-accepted proposal is replaced by an indistinguishable `COMPOSITE GEOMETRY AXIS`
  candidate at that rotation. Nothing in its label, confidence, ordering, or UI identifies it as the
  answer.]`
* **Locked-source correctness:** `[x] Region locks now retain the display rotation alongside their
  scan and features. A guaranteed proposal for locked geometry uses that captured rotation, so later
  manual rotation or configuration changes do not corrupt its H# set. Rotation is deep-copied through
  analysis persistence.]`
* **Automation:** `[x] Codex implemented the view-scope guard, principal-axis estimator, private F-02
  candidate coverage, and locked-rotation persistence. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[With one retained region, estimate axes and cycle H1–H5; confirm at least one
  ghost finishes in the actual upright pose. Press QUIT ORIENTATION VIEW and confirm no ghost, H#
  label, or upright arrow remains on the normal fragment canvas. Reopen, lock the region, estimate,
  note all five poses, quit and rotate/change processing, then estimate from the locked region again;
  confirm the retained geometry and its upright candidate are unchanged.]`
* **Exit/coverage retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: now the <-- accept and --> are deactivated and I don't see the H#]`

**5.1 automatic hypothesis activation follow-up:**

* **Cause corrected:** `[x] Changing R# correctly invalidated the previous region's hypotheses, but
  left the H# carousel empty until ESTIMATE AXES was pressed manually. This made `← / ACCEPT / →`
  disabled and displayed `H—`, even though the selected region was ready for analysis.]`
* **Automatic region estimate:** `[x] Opening UPRIGHT ORIENTATION with an empty H# set now immediately
  estimates the selected region. Either R# arrow also selects the new region and immediately creates
  its five proposals, so the H# carousel remains active during region-to-region review.]`
* **Manual refresh retained:** `[x] ESTIMATE AXES remains available to explicitly recalculate the
  current live or locked source. Automatic generation uses the same geometry, private F-02 candidate
  coverage, persistence, and rendering path as the manual button.]`
* **Automation:** `[x] Codex implemented the controller-flow correction. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[Open UPRIGHT ORIENTATION without first pressing ESTIMATE AXES and confirm H1
  appears with enabled hypothesis arrows and ACCEPT. Use both R# arrows repeatedly and confirm each
  new region immediately receives H1–H5 without an intermediate empty/disabled state. Cycle all H#
  proposals, confirm one reaches upright, then use ESTIMATE AXES and verify the current region remains
  selected and its carousel stays active.]`
* **Automatic-H# retest result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: __________]`

---

## 5.2 Decide rotation correction

### Player capacity

The player has **yellow** capacity.

The player can determine approximate correction but may not estimate angular error precisely.

### Rover support capacity

The Rover has **green** capacity.

It should calculate and expose geometric angular error relative to its assumed upright orientation.

### Rover performer capacity

The Rover has **green** capacity.

It can determine a correction direction and magnitude.

### Player support capacity

The player has **yellow** capacity.

The player can supervise the correction.

### Implementation requirements

**Observability**

* Show estimated angular error.

Example:
`Orientation error: 27°`

### Directability

* The player must be able to:

  * accept the correction;
  * reject it;
  * manually adjust the proposed correction.

Do not directly use the hidden correct puzzle rotation as the Rover's orientation estimate.

#### Inline implementation plan — checkpoint 5.2

**Implementation status:** Not started. Depends on a player-accepted orientation from 5.1.

**Capacity entry point:** `FragmentOrientationEstimator.CalculateCorrection(acceptedHypothesis,
currentDisplayRotation)` returns the shortest signed angular change needed to align the accepted
candidate axis with the upright reference. Normalize the result consistently to `[-180°, 180°]`
and convert sign into clear `CW`/`CCW` copy matching Godot's screen-coordinate convention.

Show `Orientation error: N°`, proposed direction/magnitude, and an editable degree control. `ACCEPT`
freezes the edited proposal for 5.3; `REJECT` clears it; manual adjustment marks it
`PlayerAdjusted` without making it semantically correct. Without an accepted 5.1 hypothesis, this
function stays unavailable and explains why.

**Automatic:** signed-angle calculation, coordinate-convention tests with synthetic axes, degree
editor, accept/reject flow, overlay update, and persistence.

**Godot check:** no setup. Check clockwise, counter-clockwise, near ±180°, zero, and manually edited
corrections.

**Gate test 5.2 (fill after implementation)**

* **Expected:** a synthetic 27° axis reports the intended signed 27° correction and direction;
  wraparound selects the shortest path; edit/accept/reject works; no correction is available after
  rejecting its source hypothesis; the value never comes from hidden correct rotation.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Angles and edits observed:** `[ fill after test ]`
* **Build/scene/resolution:** `[Resolved by the visible-application follow-up; build passed.]`
* **Approved to implement 5.3:** `[x] YES  [ ] NO`

**5.2 test-comment review:**

* **Finding:** `[x] This is the planned checkpoint boundary rather than a failed angular decision.
  Checkpoint 5.2 calculates, edits, rejects, or approves a target correction; checkpoint 5.3 owns
  applying that correction to the displayed fragment, including animation, cancellation, player
  override, and exact final placement. Applying rotation from 5.2 would bypass those required 5.3
  safeguards.]`
* **UI clarification:** `[x] The action is now labelled APPROVE FOR ROTATION, its tooltip says that
  it does not rotate the sample yet, and an approved value displays APPROVED · NOT YET ROTATED.
  Rover status likewise directs the player to the Rotate step.]`
* **5.2 boundary retest:** `[Confirm the proposal angle and ghost are correct, edit it, press APPROVE
  FOR ROTATION, and verify the value becomes read-only and clearly remains queued rather than moving
  the fragment. If that succeeds, approve 5.3 to implement the actual rotation.]`
* **Boundary retest result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED REASON: I don't see any rotation applied visually in normal view to the glyph within the region selected, nor in orientation view nor in compare side-by-side, whereas ccw -10° works just fine]`

**5.2 visible-application follow-up:**

* **Revised interaction:** `[x] Player approval now means APPLY ROTATION. It adds the approved signed
  correction to the current display angle, normalizes the exact target to [-180°, 180°], and sends
  that target through the same source-aware rotation command path used by the analysis controls.]`
* **Visible result:** `[x] The normal fragment canvas redraws at the resulting angle immediately.
  Live orientation geometry is invalidated and can be re-estimated at its new pose; deliberately
  locked comparison snapshots remain unchanged as historical references. Applying from an isolated
  or side-by-side orientation view returns to the normal canvas so the real result is not hidden
  behind that fixed snapshot.]`
* **5.3 boundary after revision:** `[x] Checkpoint 5.3 will enhance this immediate exact application
  with a timed preview/tween, progress state, mid-motion editing/cancellation, and explicit player
  override. It no longer owns the first visible application.]`
* **Focused retest:** `[Choose and accept H#, propose a non-zero correction, optionally edit it, and
  press APPLY ROTATION. Confirm the normal fragment changes by exactly the displayed CW/CCW amount,
  the top rotation value updates, and the Rover reports the final display angle. Repeat across the
  ±180° boundary. Confirm a locked side-by-side reference itself stays fixed.]`
* **Visible-application retest result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: __________]`

---

## 5.3 Rotate

### Player capacity

The player has **green** capacity.

Existing manual CW/CCW rotation remains available.

### Rover support capacity

The Rover does not need to execute rotation on behalf of a human performer.

### Rover performer capacity

The Rover has **green** capacity.

It can execute a chosen rotation precisely.

### Player support capacity

The player has **yellow** capacity.

The player should be able to supervise and adjust autonomous rotation.

### Implementation requirements

**Observability**

* Show a ghost or other comparison between the initial/current orientation and the Rover-proposed resulting orientation.

**Predictability**

* Before autonomous rotation, show:

  * intended rotation direction;
  * intended rotation magnitude.

Example:
`ROTATE CW +30°`

**Directability**

* The player must be able to modify the Rover's proposed rotation before or during execution.

Manual rotation controls remain available.

#### Inline implementation plan — checkpoint 5.3

**Implementation status:** Implemented; awaiting the gate test. Checkpoint 5.2 and the source-aware
command path are complete.

**Capacity entry point:** `FragmentAnalysisRover.ApplyApprovedRotationCorrection(...)` starts a
frame-yielding preview/tween state machine that announces `ROTATE CW/CCW N°`, draws the
current-versus-proposed ghost, waits the agreed preview, and eases the existing displayed rotation
to the exact player-approved target through the shared control adapter. It never reads the correct
target angle.

Manual CW/CCW/fine rotation remains enabled during the preview and animation. Player input cancels
the Rover tween at its current angle with no snap-back, applies the player's change, updates the
ghost/proposal, and follows the override policy chosen in 2.4-A. Editing the proposal during
execution follows the same cancel-and-replan rule, which makes the modification explicit and safe.

**Pre-existing manual-solvability issue:** initial rotation is currently a continuous random value,
manual buttons move only 10°, and solve tolerance is 3°. Therefore roughly 40% of initial angle
residues can never reach the tolerance using manual controls alone. This must be resolved here to
preserve genuine player override rather than letting precise Rover rotation become the only path.

**Automatic:** rotation preview/tween/cancellation, ghost comparison, visible rotation label, chosen
manual-solvability fix, state/history updates, and angle/cancellation checks.

**Godot check:** no setup. Test precise completion, both directions, wraparound, manual controls
during motion, edited proposals, and a seed whose starting angle is not a multiple of 10°.

**Decision 5.3-A — fix the existing manual rotation gap:**

* **A (recommended):** keep ±10° buttons and add a 1° fine-adjust slider/SpinBox; show the current
  angle. This retains coarse input while making every continuous starting rotation reachable.
* **B:** quantize generated initial rotations to multiples of 10°.
* **C:** increase solve tolerance to at least 5°.
* **D:** combine A and B.
* **Answer:** `[ 5.3-A: A]`

**Gate test 5.3 (fill after implementation)**

* **Expected:** Rover execution reaches the approved angle precisely and visibly; manual input at
  any point cancels without snap-back and remains authoritative; all generated starting angles are
  manually solvable under 5.3-A; the normal solve condition remains otherwise unchanged.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Seed/start/proposed/final angles and interruption observed:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 6.1:** `[ ] YES  [ ] NO`

**5.3 implementation follow-up:**

* **Preview and execution:** `[x] START ROTATION announces the exact CW/CCW magnitude and current →
  target display angles, retains the 5.2 comparison ghost for the configured preview interval, then
  applies a bounded SmoothStep tween through the shared Rover-origin rotation command path. Angle
  interpolation follows the approved signed shortest path across ±180°. When execution begins,
  orientation isolation closes so the real fragment tween is visible rather than hidden behind the
  preview snapshot.]`
* **Progress and cancellation:** `[x] The correction row reports PREVIEWING and ROTATING N%. During
  either phase the action changes to CANCEL ROTATION. Cancellation stops at the currently displayed
  angle with no restoration or snap-back and keeps the proposal available for editing/restart.]`
* **Manual override:** `[x] Existing CCW -10° / CW +10° controls remain enabled. Using either one—or
  the new exact-angle control—during Rover motion cancels the tween first, leaves the player's angle
  authoritative, and pauses active autonomy under the existing 2.4 override policy.]`
* **Mid-execution edit:** `[x] Editing the signed correction during preview or motion cancels at the
  current pose, updates the retained proposal relative to that pose, and requires an explicit START
  ROTATION to run the revised plan.]`
* **Manual-solvability choice A:** `[x] The previously hidden current-angle label is visible and a
  -180°..180° SpinBox provides 1° steps alongside the retained ±10° buttons. Continuous random start
  residues can therefore be brought to any integer target, including the normal 0° solution, while
  the existing 3° correctness tolerance remains unchanged.]`
* **Safety:** `[x] Preview is capped at five seconds and tween duration at 0.1–5 seconds. Per-frame
  Rover commands skip expensive feature/metric recomputation; observations refresh once at exact
  completion. Starting rotation stops any active configuration search, while pause, allocation
  changes, reload, and player input cancel safely.]`
* **Automation:** `[x] Codex implemented C# state-machine execution, command/UI synchronization,
  scene controls, responsive progress copy, cancellation, and history. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[With Rover PERFORMER, accept H#, propose a correction, and press START
  ROTATION. Confirm the ghost remains during preview, the percentage advances, and the exact target
  is reached. Repeat in both directions and across ±180°. During separate runs: press CANCEL, press
  CCW/CW, edit the correction SpinBox, and change the exact-angle SpinBox; each must stop at its
  current pose without snap-back. Resume after a manual override. Finally load a fractional random
  starting angle and use the 1° control to reach the normal solved tolerance.]`
* **5.3 focused retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: 2 things. 1: merge the upright orientation and rotation correction into one "ORIENTATION" menu. In performer, accepting hypothesis leads to automatic rotation correction and then automatic rotation. 2: There is a bug where previously identified features/structures re-appear in the original rotation after rotation correction, so the structure which was good has now duplicates (e.g. the sides of the hexagons appear in the corrected orientation and some of them also in the previous orientation which mess the overall structure), implement a way to follow transformation for features and structures and avoid duplicates]`

**5.3 unified-orientation and annotation-transform follow-up:**

* **Unified menu:** `[x] UPRIGHT ORIENTATION and ROTATION CORRECTION now share one collapsible
  ORIENTATION section. The H# region/hypothesis review, correction angle editor, direction, preview,
  cancel, and execution controls expand and collapse together; the redundant correction heading and
  separator are hidden.]`
* **Performer chain:** `[x] In PERFORMER, accepting H# automatically calculates its shortest signed
  correction and immediately starts the existing preview/tween sequence. The proposal and progress
  remain visible and cancellable. Non-Performer modes retain explicit/manual supervision and the
  ordinary CCW/CW/exact-angle controls.]`
* **Duplicate root cause:** `[x] Retained accepted or player-edited structure members intentionally
  survive rescans, but their normalized stroke geometry previously remained at the pre-rotation
  pose. Post-rotation detections therefore could not identity-match those IDs and appeared beside
  the retained originals.]`
* **Transformation fix:** `[x] At exact tween completion, every live detected feature endpoint and
  constituent segment is transformed by the same signed pixel-space delta used for the canvas.
  Structure members share their observable structure centroid as a pivot; standalone features use
  their own observable centroid. Live region bounds are rebuilt around their transformed members.
  Structure membership remains attached to preserved feature IDs, so structures follow without
  being rebuilt from stale coordinates before the completion rescan. The same transform runs for
  ordinary CCW/CW/exact-angle input; a mid-tween override uses the full delta from the tween's
  starting pose, so cancellation cannot leave annotations partway behind.]`
* **Locked-reference rule:** `[x] Locked region scans/features remain unchanged because they are
  historical comparison snapshots. Only live annotations transform; completing the rotation then
  performs one detector identity-match pass and refreshes feature, region, and structure overlays.]`
* **Automation:** `[x] Codex implemented the merged scene/controller presentation, automatic
  Performer chain, aspect-aware annotation transformation, rescan synchronization, and history-safe
  ID preservation. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs).]`
* **Focused retest:** `[In PERFORMER, expand only ORIENTATION, select a region, accept H#, and confirm
  correction preview and rotation begin without pressing another action. Cancel once during preview
  and once during tween. On a separate run, accept a reconstructed hexagon-like structure, rotate it,
  and confirm all accepted feature strokes move with it while their F#/S# identity and disposition
  remain; no old-angle duplicates may remain after the completion rescan. Open a locked comparison
  and confirm its historical pane stays fixed while the live fragment/annotations rotate.]`
* **Unified/transform retest result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: __________]`

---

# 6. Extract the monolith position cue

## 6.1 Sense the directional arrow

### Player capacity

The player has **green** capacity once the arrow is sufficiently reconstructed.

### Rover support capacity

The Rover has **green** support capacity.

It can visually isolate or highlight arrow-like geometry.

### Rover performer capacity

The Rover has **green** capacity.

It can detect a candidate directional arrow geometrically.

### Player support capacity

The player has **yellow** capacity.

The player can verify or correct the Rover's arrow detection.

### Implementation requirements

**Observability**

* Highlight the structure that the Rover currently identifies as the directional arrow.

The highlight must not modify the actual puzzle geometry.

**Directability**

* The player must be able to:

  * accept the detected arrow;
  * reject it;
  * adjust or select another structure as the arrow.

Do not derive the detected arrow directly from the hidden puzzle arrow definition for autonomous sensing.

#### Inline implementation plan — checkpoint 6.1

**Implementation status:** Implemented; awaiting the gate test. Editable features/structures from
2.1 and 4.1 are used as the observable geometry source.

**Capacity entry point:** `FragmentArrowDetector.DetectCandidates(...)` searches the accepted
feature/structure graph for a long shaft ending at a junction with two shorter head strokes at
plausible, approximately symmetric angles. Rank anonymous candidates by shaft continuity,
head-to-shaft proportions, endpoint convergence, and geometric symmetry only. It must not inspect
line role, generator arrow data, puzzle monolith direction, or current fragment-to-monolith vector.

Draw the active arrow candidate with a dedicated high-contrast directional highlight and show its
geometric evidence/confidence. `AcceptArrow`, `RejectArrow`, and `DefineArrowFromFeatures` keep
stable feature IDs and player provenance. Rejecting advances to another geometric candidate;
accepting one is required for checkpoint 6.2. Overlay edits never mutate puzzle lines.

**Automatic:** detector, ranking, arrow state, highlight, candidate navigation, manual-definition
tool, persistence, synthetic arrow/non-arrow fixtures, and hidden-access checks.

**Godot check:** no setup. Test a clear arrow, a false/distractor arrow, rejection, correction, and
close/reopen.

**Decision 6.1-A — manual arrow correction:**

* **A (recommended):** select one shaft feature and two optional head features; also allow a simple
  drag from tail to tip when fragmented heads cannot be selected.
* **B:** feature selection only.
* **C:** tail-to-tip drag only, treating the chosen vector as a player-defined arrow.
* **Answer:** `[ 6.1-A: C]`

**Gate test 6.1 (fill after implementation)**

* **Expected:** the Rover highlights geometrically plausible candidates, not hidden truth;
  accept/reject/manual correction works and persists; a rejected candidate is not immediately
  reused; overlay state does not change puzzle geometry or solved state.
* **Result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED`
* **Observed/candidates and edits tested:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved to implement 6.2:** `[x] YES  [ ] NO`

**6.1 implementation follow-up:**

* **Geometry-only detector:** `[x] FragmentArrowDetector evaluates only retained observable feature
  segments and reconstructed structure membership. It tries both shaft directions and scores two
  shorter, converging head strokes by symmetry, convergence, proportions, and shaft dominance. It
  receives no puzzle object, line role, glyph identity, stored arrow direction, or monolith
  position/direction.]`
* **Candidate workflow:** `[x] Up to eight ranked candidates are stored with stable A# identities,
  feature evidence, confidence, provenance, and Proposed/Accepted/Dismissed disposition. The Rover
  panel can scan, show/hide the overlay, step or directly select candidates, accept, reject, and
  restore them. Accepting a candidate demotes any earlier accepted arrow; rejection advances to the
  next proposal.]`
* **Rejected-candidate memory:** `[x] Dismissed geometry is retained across rescans and equivalent
  tail/tip geometry is suppressed, so DETECT ARROWS cannot immediately reintroduce the rejected
  direction under a new identifier.]`
* **Decision 6.1-A (C):** `[x] DRAW TAIL → TIP arms an explicit crosshair tool. One drag creates a
  player-provenance arrow vector without blindly accepting any underlying features. The preview and
  stored arrow are annotations only; they never alter puzzle strokes or analysis controls.]`
* **Overlay/persistence:** `[x] The selected arrow is drawn with a black-backed high-contrast shaft,
  arrowhead, tail marker, A# label, and source/confidence. Accepted, rejected-selected, Rover, and
  player-drawn states use distinct colors. Candidates and selection deep-copy through the existing
  per-fragment autonomy state and reset with a new/reloaded puzzle. Live arrow vectors also follow
  later canvas rotations through the same aspect-aware annotation transform used by features and
  structures.]`
* **Automation:** `[x] C# detector/controller/overlay code, scene hierarchy, signal wiring, and
  persistence are automatic. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs). git diff --check reports no whitespace errors.]`
* **Focused test:** `[With Rover SUPPORT or PERFORM and reconstructed visible geometry, expand
  DIRECTIONAL ARROW and press DETECT ARROWS. Step through A# candidates and confirm only the active
  candidate receives the selected highlight. Reject one, rescan, and confirm that same vector does
  not return. Accept another, close/reopen the same fragment, and confirm it remains accepted. Then
  press DRAW TAIL → TIP, drag once over any visible direction, confirm a PLAYER A# appears, and
  accept it. Toggle OVERLAY and verify only annotations disappear; the puzzle image, configuration,
  solved state, and feature dispositions must remain unchanged. Reload and confirm arrow state is
  cleared.]`
* **6.1 focused result:** `[x] PASS  [ ] FAIL  [ ] BLOCKED REASON: fill after test ]`

---

## 6.2 Interpret monolith direction

### Player capacity

The player has **green** capacity.

Once the figure is correctly oriented, the player understands that the arrow indicates the direction toward the monolith.

### Rover support capacity

The Rover has **green** support capacity.

It can transform an accepted arrow orientation into the game's world/map reference frame.

### Rover performer capacity

The Rover has **green** capacity.

It can independently calculate this transformation after an arrow and orientation have been established.

### Player support capacity

The player has **yellow** capacity.

### Implementation requirements

**Observability**

* Show how the reconstructed arrow maps onto the world map.

we should implement a way to make the relationship clear between:

1. arrow orientation inside the fragment analyzer;
2. oriented fragment reference frame;
3. world/map direction.

A possible implementation is on the world-map or on the minimap directional overlay derived from the interpreted arrow.

#### Inline implementation plan — checkpoint 6.2

**Implementation status:** Implemented; awaiting the final capability gate test. Accepted
orientation and arrow state from 5.1/6.1 form the complete input boundary.

**Capacity entry points:**

* `FragmentDirectionMapper.ToUprightDirection(acceptedArrow, acceptedOrientation)` rotates the
  player-accepted arrow vector by the player-accepted upright correction.
* `FragmentDirectionMapper.ToWorldGridDirection(...)` applies one explicit, tested conversion from
  analyzer coordinates to Godot grid coordinates and returns a normalized bearing/vector.
* `FragmentDirectionOverlay.Show(...)` presents the resulting world compass/map bearing and names
  the accepted A# + H# inputs used to produce it.

The mapper receives the fragment position only if a map ray needs an origin. It never receives the
monolith position or `Puzzle.MonolithDirection`, and the ray is not snapped or extended to a known
endpoint. If orientation or arrow acceptance changes, the bearing is marked stale until recomputed.

The current analyzer is full-screen and disables world/minimap input, so an analyzer-side read-only
compass/map inset is the lowest-coupling presentation. A persistent post-analysis minimap ray can be
emitted as accepted player knowledge if selected below.

**Automatic:** coordinate mapper, right/up/diagonal unit checks, world-compass visualization,
compass/inset nodes, optional minimap event/overlay, and persistence.

**Godot check:** no node setup. Validate known synthetic right, up, down, and diagonal arrow vectors,
then compare one reconstructed arrow with movement directions on the actual map.

**Decision 6.2-A — direction presentation:**

* **A (recommended first pass):** analyzer-side compass/map inset showing scan → upright → world
  bearing; retain the accepted bearing when the analysis is reopened.
* **B:** A plus a persistent bearing ray on the existing minimap after acceptance/closing.
* **C:** modify the live minimap only; the analysis panel shows explanatory text but no inset.
* **Answer:** `[ 6.2-A: A+B]`

**Decision 6.2-B — bearing format:**

* **A (recommended):** compass label plus degrees and normalized grid vector, for example
  `NE · 45° · (+0.71, -0.71)`.
* **B:** compass label and ray only.
* **C:** eight-way quantized direction only.
* **Answer:** `[ 6.2-B: A]`

**Gate test 6.2 (fill after implementation)**

* **Expected:** scan arrow, upright transform, and world bearing are visibly related; right/up/down/
  diagonal test vectors map to the agreed Godot grid directions; changing accepted arrow or
  orientation invalidates/recomputes the output; no monolith truth is read or endpoint revealed.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Vectors/bearings and map comparison observed:** `[ fill after test ]`
* **Build/scene/resolution:** `[ fill after test ]`
* **Approved as capability-complete:** `[ ] YES  [ ] NO`

**6.2 implementation follow-up:**

* **Coordinate mapper:** `[x] FragmentDirectionMapper aspect-corrects the accepted A# vector into
  analyzer pixel coordinates, applies the accepted H# upright correction when it has not already
  been physically applied, and converts directly into Godot grid coordinates (+X right, +Y down).
  Bearing degrees are clockwise from grid north and formatted as compass label, degrees, and a
  normalized vector.]`
* **No endpoint/truth access:** `[x] The mapper accepts only the player-accepted arrow, accepted
  orientation, neutral sample dimensions, and calculated display correction. The minimap receives
  only fragment grid position plus normalized bearing. Neither component receives Puzzle,
  MonolithPosition, MonolithDirection, a known endpoint, or snapping data.]`
* **Decision 6.2-A (A+B):** `[x] WORLD DIRECTION contains a read-only world/grid compass derived
  from accepted A# + H#. Once mapped, the existing minimap draws a cyan ray from the fragment tile
  to the map boundary. The ray remains after closing analysis and supports independent bearings
  from multiple fragments for the lifetime of the level.]`
* **Decision 6.2-B (A):** `[x] The status and inset report values such as
  NE · 45.0° · (+0.71, -0.71). The minimap ray is labelled FRAGMENT · <compass> but deliberately
  stops at the map boundary rather than revealing a target location.]`
* **Allocation/directability:** `[x] In PERFORMER, accepting both A# and H# maps automatically. In
  SUPPORT, MAP TO WORLD is the explicit player action. Changing/rejecting the accepted A# or H#, or
  rotating the live reconstruction, invalidates the prior bearing; Performer recomputes when valid
  while Support waits for another player command.]`
* **Persistence/reset:** `[x] The direction interpretation deep-copies in FragmentAutonomyState.
  Close/reopen republishes the same accepted minimap ray; Reload/new puzzle removes it. Allocation
  OFF does not erase already accepted player knowledge.]`
* **Coordinate contract checks:** `[x] A startup guard evaluates synthetic right, up, down, and NE
  vectors against E/90°, N/0°, S/180°, and NE/45° Godot-grid results. A mismatch emits an explicit
  Godot error before analysis proceeds.]`
* **Automation:** `[x] Mapper, state/controller flow, inset rendering, panel wiring, minimap ray,
  persistence, invalidation, and coordinate guards are code-authored. Godot editor setup: none.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs). git diff --check reports no whitespace errors.]`
* **Focused test:** `[Accept one H# and A# in SUPPORT, expand WORLD DIRECTION, and press MAP TO
  WORLD. Confirm the three arrows visibly connect scan → corrected/upright → grid, and compare the
  compass/degrees/vector with the arrow direction. Close analysis and confirm the cyan ray begins
  at the fragment tile and reaches only the minimap boundary. Reopen and confirm it persists.
  Reject/change A# or H# and confirm the old ray disappears until remapped; repeat in PERFORMER and
  confirm automatic recomputation. Rotate once and verify the mapped world direction remains
  consistent with the corrected arrow. Reload and confirm the ray clears. No ray may snap to or end
  at the actual monolith.]`
* **6.2 focused result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: 1. in normal view, upon accepting features, if the next feature is in the viewport, don't center the camera on it, it gets dizzy. 2. orientation --> directional arrow --> world direction is reset upon quitting the analysis it should be persistent. 3. I don't understand the three diagrams in world direction, the 3rd one seems enough, it should add that a mapping has been added to the minimap.]`

**6.2 usability/persistence correction:**

* **Feature-review camera:** `[x] Advancing after Accept/Dismiss now checks the next F# against the
  current transformed viewport before requesting focus. If it is already visible, selection and
  the pending highlight advance without moving the camera; an off-screen feature still centers so
  review cannot silently advance to invisible evidence.]`
* **Close/reopen root cause:** `[x] Restored sessions were immediately running the feature detector.
  The detector correctly regarded the retained live orientation snapshot as a geometry revision,
  invalidated H#, and thereby cascaded into clearing the accepted A#→world interpretation. A
  restored session with retained F# state now reuses its stable reviewed identities and snapshots;
  detector bootstrap runs only for a new/empty session.]`
* **Persistent chain:** `[x] H#, A#, world interpretation, and minimap bearing now survive normal
  close/reopen through the existing deep-copied FragmentAutonomyState. Reload remains the explicit
  operation that clears them.]`
* **Simplified presentation:** `[x] The scan and intermediate upright mini-diagrams are removed.
  WORLD DIRECTION now shows one larger N/E/S/W world-grid compass, the compass/degrees/vector line,
  its A# + H# source, and the explicit message MINIMAP: BEARING RAY ADDED AT FRAGMENT LOCATION.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs). git diff --check reports no whitespace errors.]`
* **Focused retest:** `[Accept or dismiss several F# candidates that are already visible and confirm
  the pending selection advances without any pan; confirm an off-screen next F# still centers.
  Complete H# → A# → WORLD DIRECTION, note the single compass/bearing and minimap-added message,
  close analysis, and confirm the cyan minimap ray remains. Reopen the same fragment and confirm H#,
  A#, compass bearing, and ray are unchanged. Reload and confirm the chain and ray clear.]`
* **Usability/persistence retest result:** `[ ] PASS  [ ] FAIL  [x] BLOCKED REASON: Now dismissing a feature caused a region deletion and upon redrawing the region I can no longer accept any feature, the buttons are deactivated, we need to correct that]`

**6.2 replacement-region feature-review correction:**

* **Root cause:** `[x] Feature editability was still constrained by the side-by-side page's cached
  region IDs. After that region was dismissed/replaced, a newly drawn player region had a new R#,
  but CanEditFeatureOnCurrentReviewPage continued checking the obsolete review scope. Features in
  the replacement region therefore appeared selected while ACCEPT/DISMISS/RESTORE were disabled.]`
* **Stale-scope guard:** `[x] Feature actions and their enabled state now prune priority R# entries
  that no longer refer to retained regions. A missing/dismissed review page can no longer block
  normal-view feature editing.]`
* **Replacement-region lifecycle:** `[x] Drawing a player region makes that R# an accepted active
  feature-review scope, records all contained F# IDs, selects its first proposed F#, and clears the
  obsolete page restriction. Rover F# candidates inside the explicitly redrawn area that had been
  dismissed by the previous region crop/dismissal are restored to Proposed for renewed review;
  features outside the selected area remain excluded.]`
* **Region/feature independence:** `[x] Dismissing an F# continues to change only that feature and
  any reconstructed-structure membership; it does not alter a candidate region's disposition or
  remove the R#. Region dismissal remains a separate explicit REGION action.]`
* **Build verification:** `[x] dotnet build --no-restore succeeds (0 errors; 3 pre-existing warnings
  in BuildingComponent.cs and SaveManager.cs). git diff --check reports no whitespace errors.]`
* **Focused retest:** `[In normal view, select an accepted R# and dismiss one F#. Confirm the R#
  boundary remains and only that F# changes disposition. Draw a replacement region over visible
  features and confirm its first pending F# is selected with ACCEPT and DISMISS enabled. Review
  several F# candidates, including one restored from the former region, and verify the controls
  remain active while selection advances. Confirm features outside the replacement region remain
  excluded.]`
* **Replacement-region retest result:** `[ ] PASS  [x] FAIL  [ ] BLOCKED REASON: The "draw arrow" does not work. In the SelectedRobotUI, remove the whole 1 sample in range + dropmenu, and simply push a message to the message Log from Rover saying "1 sample in range, not yet analysed" to reduce UI clutter.  When opening analysis for the first time there should be a popup dialog box with , MANUAL analysis, Rover SUPPORT, ROVER AUTONOMOUS radio button option, then in the sample analyzer UI, there is manual/support/rover autonomous instead of "ROVER: OFF / OFF which is to be removed. the ROVER<- toggle menu should be on the extreme right of this row and show "ROVER MENU<=", COMPARE should be within region menu not top of UI. rotation buttons should be within ORIENTATION menu, not top UI. On the ROVER autonomy side bar, these are the simplifications to be made because the UI is way too cluttered as such: 1. remove TARGET and IDLE from status. 1-bis remove all "RESTORE" buttons. 2. move the back and forward arrow to top just under "ROVER AUTONOMY" title. 3. Remove "TESTED CONFIGURATIONS" menu, it is useless. 4. Change "GROUP REGIONS" button to "GENERATE REGIONS", remove "RESTORE" option, 5. Remove "REGION SEQUENCE" menu it is useless. Merge FEATURE and STRUCTURES menu, structures should just be a single button at the end of that menu, "RECONSTRUCT STRUCTURE" which uses directly features, and structure should be selectable, editable, and deletable that's it. EDIT structure should be a double click on the structure and individual strokes should be deletable, and new stroke should be added by click and drag. The "editing structure" should be displayed in the region header, along with "DEL" to delete stroke, left-click and drag to addm, and a small save structure button so that the right-panel UI is not cluttered. 6. SCANNED FRAGMENT should not be a menu, the content of this menu should be put under the status section. 7. orientation should just have the arrow, H and "accept" then propose correction with the start rotation/reject and underneath the manual orientation buttons. 8. The directional arrow and world direction menu should be merged. The task allocation should be on the top bar of the sample analyser UI, the back, pause, skip, undo, accept, reject buttons should be removed ]`

---

# Rover autonomy architecture

The autonomy system should be separate from the existing puzzle generator and renderer.

Recommended structure:

```text
FragmentPuzzle
      |
      v
FragmentCanvas
      |
      +--------------------+
      |                    |
      v                    v
FragmentAnalysisUI    FragmentAnalysisRover
                           |
                           +-- Feature detection
                           +-- Candidate regions
                           +-- Inspection history
                           +-- Signal metrics
                           +-- Processing history
                           +-- Configuration search
                           +-- Structure detection
                           +-- Orientation estimation
                           +-- Arrow detection
                           +-- World-direction conversion
```

`FragmentAnalysisRover` should operate on public analysis-state information and derived visual/geometric data.

It should not modify `FragmentPuzzle` or its hidden solution.

The autonomy system can be OFF/SUPPORTER/PERFORMER for all tasks based on the previous capacity assessment, implement a tri-state parameter to adjust that function allocation, it can be exposed as a three-way radio button on the analysis UI.

## Architecture implementation note

The conceptual diagram above is retained, but the concrete dependency direction must prevent the
Rover from reaching `FragmentCanvas.Puzzle`, whose public object currently contains every hidden
answer. Implement this narrow boundary:

```text
FragmentPuzzleGenerator -> FragmentPuzzle -> FragmentCanvas renderer
                              |                     |
                    immutable truth       IFragmentObservationSource
                         snapshot                  |
                              |                     |
                              +----> FragmentAnalysisRover
                                             |              |
                                      overlay state    proposed commands
                                             v              v
                                   FragmentRoverOverlay  IFragmentCommandSink
                                                              |
                                                              v
                                                    FragmentAnalysisUI controls
                                                              |
                                                              v
                                                       FragmentCanvas
```

The Rover is constructed with `IFragmentObservationSource`, `IFragmentCommandSink`, and the private
immutable `FragmentAutonomyTruth` selected in F-02—not a mutable `FragmentPuzzle` or unrestricted
`FragmentCanvas`. Renderer observations still strip source roles and correct-answer fields. Direct
Puzzle mutation and accidental answer exposure remain prohibited even though the deliberate oracle
exists.

**Planned files (all automatic):**

* `FragmentAutonomyMode.cs` and `FragmentAutonomyModels.cs` — enums and immutable public contracts;
* `FragmentAutonomySettings.cs` — exported algorithm/timing/colour settings;
* `FragmentAnalysisRover.cs` — coordinator/state machine only;
* focused algorithm classes for features, regions, metrics, structures, orientation, arrows, and
  direction mapping so each can be tested without a scene tree;
* `FragmentRoverOverlay.cs` — annotations, targets, ghosts, and edit hit testing;
* extensions to `FragmentAnalysisUI.cs`, `FragmentCanvas.cs`, `FragmentAnalysisState.cs`,
  `SelectedRobotUI.cs`, `GridManager.cs`, `BaseLevel.cs`, and their two relevant `.tscn` scenes.

The mode selector is mirrored before analysis for capacities 1.1–1.3 and inside the analyzer for
all later capacities. Red capacities always hand responsibility to the player even if the global
mode is Performer. Final allocation granularity follows decision F-01.

---

# Suggested Rover state

The Rover module should maintain its own analysis state, potentially including:

```text
DetectedFeatures
CandidateRegions
InspectedRegions
DetectedStructures

CurrentNavigationTarget

SignalMetrics
PreviousConfigurations
BestMeasuredConfigurations

LockedParameters
AutonomyPaused
CurrentSearchState
NextPlannedAdjustment

OrientationHypotheses
AcceptedOrientation

DetectedArrowCandidates
AcceptedArrow
```

This state should remain separate from the deterministic puzzle definition.

## State implementation note

Use puzzle-normalized coordinates and stable IDs for persistent annotations so pan, zoom, viewport
size, and window resolution do not corrupt them. Treat collections as owned deep copies when
capturing `FragmentAnalysisState`; do not save references to live mutable Rover lists.

**Durable for the agreed F-04 lifetime:** detected/player features, region/structure definitions and
their dispositions, inspection coverage, distinct tested configurations and metrics, locks,
accepted/rejected orientation hypotheses, accepted/rejected arrows, accepted world bearing, mode,
and pause state.

**Ephemeral:** timers, tweens, cancellation tokens, hover/drag state, cached render snapshots, and
an in-progress action. Restore an interrupted/in-flight session as paused with the prior action
described, never by silently continuing it.

`BestMeasuredConfigurations` means only “best under the selected observable metrics.” It is not a
solution candidate or correctness assertion.

**Decision S-01 — Reload semantics:** Reload currently generates a new seed and therefore a new
hidden puzzle.

* **A (recommended):** ask for confirmation, then clear all Rover annotations/history/hypotheses/
  bearing for the old seed and initialize a fresh Rover session.
* **B:** reload immediately and clear Rover state, matching today's one-click behavior.
* **C:** retain Rover state across reload. This is not recommended because normalized annotations
  would refer to obsolete geometry.
* **Answer:** `[ S-01: A]`

---

# Human override principle

Whenever Rover autonomy is actively changing analysis state, manual player interaction takes priority.

The following player actions should be capable of interrupting or overriding autonomy:

* pan;
* zoom;
* processor toggle;
* processor level adjustment;
* scan-channel toggle;
* rotation;
* target-region selection;
* feature or structure editing;
* parameter locking;
* pause;
* undo;
* candidate rejection.

The Rover should then either pause or update its plan based on the new state rather than immediately undoing the player's intervention.

## Override implementation note

All mutations emit `FragmentAnalysisChanged(previous, current, parameter, origin)`. An active Rover
action ignores its own `Rover`-origin event, records `Restore` without interpreting it as player
intent, and treats every `Player`-origin event in the list above as an override. One cancellation
source controls navigation, preview timers, configuration steps, and rotation tweens, ensuring an
interrupted action cannot finish later and undo the player.

Feature/region/structure editing uses explicit toolbar modes. Entering an edit mode itself counts as
player direction and suspends incompatible navigation; this resolves the current conflict where
left-drag is already pan input. The post-override behavior is selected once in decision 2.4-A and is
applied consistently everywhere.

**Override verification:** every relevant gate includes at least one mid-action manual interruption;
the final integration test repeats all listed inputs while Performer mode is active and verifies no
delayed snap-back.

---

# Autonomy transparency principle

Do not implement the Rover as a black-box solver.

When it performs sequential analysis, expose enough state for the player to understand what it is doing.

At minimum, Rover-controlled analysis should communicate:

```text
CURRENT ACTION
NEXT ACTION
CURRENT TARGET
PARAMETERS BEING TESTED
LOCKED PARAMETERS
MEASURED RESULT
```

Example:

```text
AUTONOMOUS ANALYSIS

TARGET: Candidate Region B

CURRENT:
Testing Spectral Level 3

RESULT:
Signal continuity +12%
Background noise +3%

NEXT:
Test Spectral Level 4

LOCKED:
Polarization Level 2

[PAUSE]
```

The exact visual implementation can differ, but the underlying information must be available.

## Transparency UI implementation note

The proposed full-width layout is code/scene-authored and looks approximately like this:

```text
+------------------------------------------------------------------------+
| SAMPLE ANALYSER     AUTONOMY: (OFF) (SUPPORTER) (PERFORMER)     QUIT   |
+-----------------------------------------------+------------------------+
|                                               | ROVER STATUS           |
|  FragmentCanvas                               | Current / Next         |
|  + FragmentRoverOverlay                       | Target / Result        |
|                                               | Locks / evidence       |
|                                               | Accept / Reject        |
|                                               | Back Pause Skip Undo   |
+-----------------------------------------------+------------------------+
| Existing filters, levels, channels, locks, rotation/fine adjustment   |
+------------------------------------------------------------------------+
```

Use short text plus visual highlights, not colour alone. The Rover dock is collapsible; hiding it
does not stop autonomy, so a compact header status remains visible whenever the Rover is not Off.
`CURRENT` is updated at action start/end, `NEXT` is populated before execution, and stale target/
result values are visibly marked rather than left looking current.

**Automatic:** containers, labels, controls, accessibility tooltips, theme variations, responsive
minimum sizes, and all C# wiring. **Godot check:** visual fit, focus order, font scaling, and colour
contrast at your target resolutions.

**Decision T-01 — autonomy panel placement:**

* **A (recommended):** collapsible right dock on wide windows, with a compact status header when
  collapsed.
* **B:** full-width collapsible panel below the scan and above existing controls.
* **C:** tabbed panel that replaces the existing controls while open.
* **Answer:** `[ T-01: A on the right side of the analysis frame]`

---

# Core design constraint

The goal of this module is **interdependence**, not automated puzzle completion.

The Rover should be strongest at:

* detecting;
* measuring;
* systematically searching;
* remembering previous configurations;
* tracking inspected space;
* manipulating parameters;
* calculating geometry;
* executing precise actions.

The player should remain important for:

* semantic visual interpretation;
* correcting Rover feature/region/structure assumptions;
* judging glyph identity;
* judging whether a candidate is meaningful;
* constraining autonomous search;
* confirming ambiguous orientation;
* correcting arrow interpretation.

The implementation should therefore allow control to move naturally between:

```text
HUMAN PERFORMS
        ↓
ROVER SUPPORTS
```

and

```text
ROVER PERFORMS
        ↓
HUMAN SUPERVISES / DIRECTS / CORRECTS
```

without creating separate versions of the Fragment Analysis puzzle.

## Integration order and completion criteria

The implementation gates follow dependencies, with the single intentional reorder of 3.4 before
3.3:

| Order | Gate | Outcome |
| ---: | --- | --- |
| 0 | Foundation | Safe observation/command boundary, state, mode, overlay, empty transparency UI |
| 1–3 | 1.1 → 1.3 | Availability, proposal/approval, shared initiation |
| 4–7 | 2.1 → 2.4 | Features, regions, inspection history, navigation |
| 8–10 | 3.1 → 3.2 → 3.4 | Measurements, history/interpretation, safe execution |
| 11 | 3.3 | Locked, transparent configuration search |
| 12–14 | 4.1 → 4.3 | Editable structures, neutral references, semantic guardrail |
| 15–17 | 5.1 → 5.3 | Orientation hypothesis, correction, precise supervised rotation |
| 18–19 | 6.1 → 6.2 | Arrow detection/editing and world-direction presentation |

A gate is complete only after: relevant code is implemented; `dotnet build` passes; the selected
deterministic checks pass; the current section's user test statement is filled; and you mark
`Approved to implement ...: YES`. A failure is fixed within the same gate before any later
capability work begins.

**Final integration test (fill only after all capacity gates pass)**

* **Expected:** one saved fragment can move between Human-performs/Rover-supports and
  Rover-performs/Human-supervises without changing puzzle identity or using a separate solve path;
  all manual inputs override; transparency fields remain truthful; red semantic capacities remain
  human-only; closing/reopening follows the selected persistence policy.
* **Result:** `[ ] PASS  [ ] FAIL  [ ] BLOCKED`
* **Full scenario, seed, variant, mode transitions, and observed result:** `[ fill after test ]`
* **Build/scene/resolutions:** `[ fill after test ]`

## Planning-time repository observations

These are not autonomy behaviors, but they affect repeatable implementation/testing:

* Only the current Level 3 scene appears to place one fragment, so checkpoint 1.1's multi-sample
  test needs a dedicated fixture or a second temporary fragment.
* First-run puzzle seed and `MonolithFragment` variant are randomized; repeatable gate reports need
  a deterministic test fixture.
* `MonolithFragment.tscn` names `res://assets/monolith_fragment.png` as its default sprite texture,
  but that source PNG is absent while only its cached `.import` metadata remains. The valid
  `monolith_fragment_v1/v2/v3.png` sources are present. This can fail on a clean import.
* `FragmentPuzzleGenerator` can produce more structured distractor segments than the nominal
  `LineCount`; detectors will therefore operate on the actual observable snapshot and never assume
  a fixed configured line count.

**Decision R-01 — deterministic test content:**

* **A (recommended):** add a development-only Fragment Analysis harness with fixed seed, fragment
  variant, spatial context, and optional two-sample availability fixture.
* **B:** add test-only exported overrides to an existing level and set them manually in Godot.
* **C:** use normal randomized levels and record whatever seed/variant appears.
* **Answer:** `[ R-01: No tests]`

**Decision R-02 — missing default fragment texture:**

* **A (recommended):** repair the `.tscn` reference to a present variant during foundation work and
  verify a clean import; this is a preparatory asset-reference fix, not autonomy behavior.
* **B:** leave it outside this feature and track it separately.
* **Answer:** `[ R-02: A]`
