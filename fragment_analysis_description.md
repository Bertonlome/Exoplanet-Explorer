# Fragment Analysis

## Purpose

Fragment analysis is an optional scientific reconstruction puzzle. A rover examines a monolith fragment and tries to recover a meaningful symbol and a directional marker from a noisy mineral scan.

The reconstructed figure provides two pieces of information:

1. The glyph identifies the kind of fragment being analysed.
2. The arrow points from the fragment's world position toward the monolith's world position.

The intended player experience is not simply to enable every filter. The player gradually improves a noisy signal, compares several plausible shapes, rejects false reconstructions, finds the correct filter configuration, and rotates the recovered figure into its upright orientation.

## Starting an analysis

Only a ground rover can start fragment analysis.

1. Move a rover close enough to a monolith fragment.
2. Select the rover.
3. Press **Analyse Sample**.

If there is no fragment near the rover, the game reports that there is no sample to analyse. While the analysis interface is open, input to the world camera and minimap is disabled so that clicks, scrolling, and arrow keys operate only on the analysis view.

## What is inside a scan

Each sample is generated from a seed. The same seed defines all of the following:

- the rocky background;
- mineral grains, fractures, and veins;
- the position and initial rotation of the true figure;
- the correct processing settings;
- the correct scan-channel combination;
- incomplete and obscured signal strokes;
- noise lines and false glyphs.

The scan is larger than its visible viewport. Consequently, the true glyph may not be visible when analysis begins. The player may first need to zoom out or pan across the sample.

Even with no useful filters selected, signal lines remain faintly visible. They are rendered in the same fracture-and-deposit style as the natural mineral veins and have randomly erased sections. This makes the artificial signal difficult to distinguish from the rock until the reconstruction improves.

## The true figure

The true signal consists of three parts:

- a hexagonal frame;
- a fragment-specific glyph drawn upright inside the frame;
- an arrow extending from the edge of the hexagon.

The entire figure is placed at a random location and begins at a random rotation. The glyph, hexagon, and arrow always rotate together.

There are currently three true glyphs:

| Fragment variant | Puzzle glyph | Visual identity |
| --- | --- | --- |
| Hominid | Hominid | Two centred rectangles, a long mast, and an L-shaped branch |
| Chip | Key | A rectangular chip with pins, a central mast, and a small terminal rectangle |
| Television | Television | A divided rectangular screen with two legs and feet |

The direction arrow is calculated from the fragment's grid position to the monolith's grid position. Once the full figure is correctly oriented, the arrow therefore indicates the direction in which the player should search for the monolith.

## Processing filters

The first three controls are signal-processing filters. Each has an on/off toggle and a level from **1** to **5**:

- **Polarization** reconstructs erased portions of lines. At a good setting, strokes become more continuous and easier to follow.
- **Spectral** enhances important signal strokes by increasing their width and shifting them toward the enhancement colour.
- **Surface topography** changes contrast. At a good setting it darkens the background, suppresses mineral noise, and makes the signal brighter.

For each processor, the generated puzzle randomly decides whether that processor is required. If it is required, one of its five levels is the exact target.

Processor feedback is progressive:

- exact level: full contribution;
- one level away: medium-good contribution;
- two levels away: weak contribution;
- farther away: no contribution to reconstruction and potentially a detrimental visual effect;
- required processor switched off: a small bypass contribution only;
- unnecessary processor switched on: detrimental effect.

This scoring makes nearby settings look related rather than causing an abrupt visual jump. Moving a slider toward its target progressively improves the reconstruction, while moving it too far away can wash out the signal, expose more noise, or lower signal opacity.

## Scan channels

The final three toggles select the physical scan channels:

- **Electromagnetic**;
- **Resonance**;
- **X-Ray**.

Each puzzle has a randomized required combination. By default, neither “all channels off” nor “all channels on” is accepted as the generated answer. Required channels add their share of the signal; selecting an unnecessary channel applies a penalty. Thus, scan channels are partly additive, but enabling everything does not guarantee a clean result.

The processing score and channel score are multiplied to obtain the overall reconstruction quality. Both groups must therefore be configured well for the complete true figure to emerge.

## Progressive reconstruction

Every signal stroke has its own reveal threshold. Ordinary geometry appears earlier, while important strokes generally require a higher reconstruction quality. As the player converges on the solution:

1. faint broken traces become noticeable;
2. erased sections are restored;
3. more strokes cross their reveal thresholds;
4. important parts become wider and brighter;
5. background veins and generic noise become less prominent;
6. the hexagon, glyph, and directional arrow become readable as one figure.

This gradual reveal is the main feedback mechanism. The player is expected to judge the image rather than receive the correct numerical settings directly.

## Distractors

The sample contains several kinds of interference:

- random scratches and line fragments;
- natural mineral veins;
- three invented false glyphs: a trident, a diamond eye, and an angular spiral;
- zero, one, or two real glyphs belonging to the other fragment variants.

Every structured distractor has its own independently randomized processing and channel key. A wrong configuration can therefore reconstruct a convincing false glyph rather than merely producing featureless noise. Real-glyph decoys also use a false arrow direction.

The distractor keys are generated to differ from the true solution and, where possible, from each other. At the true solution, most distractor noise is suppressed, although a configurable fraction can remain visible so the final scan does not look unnaturally perfect.

## Rotation and navigation controls

- **CW +10°** rotates the whole reconstructed figure clockwise by 10 degrees.
- **CCW -10°** rotates it counter-clockwise by 10 degrees.
- The mouse wheel zooms around the cursor position.
- Left-click and drag pans the scan like a camera.
- The directional arrow keys also pan the scan. Their motion is camera-style: the sampled surface moves opposite the viewing direction.
- Zoom and pan are clamped to the virtual canvas, so the player cannot navigate beyond the sample.
- **Reload** generates a new puzzle seed and therefore a new scan and solution.
- **Quit** closes the interface and returns input to the game world.

## Solving condition

The puzzle is logically solved only when all three conditions are true at the same time:

1. the Electromagnetic, Resonance, and X-Ray toggles exactly match the puzzle's required scan-channel combination;
2. the enabled state and level of all three processing filters exactly match their required configuration;
3. the displayed figure rotation is within the configured tolerance of the correct rotation.

The default correct rotation is **0 degrees**, meaning the internal glyph is upright and the arrow reflects the true fragment-to-monolith direction.

At present, `FragmentCanvas.IsPuzzleSolved()` computes this condition and the state records whether it was solved. There is not yet a dedicated solved animation, confirmation message, reward, or level-progression event connected to it. The current gameplay reward is the information visible in the reconstructed glyph and arrow.

## Saving and resuming

Analysis state is stored separately for each fragment grid position when the player presses **Quit**. Reopening the same fragment restores:

- the puzzle seed and glyph type;
- every filter toggle;
- all three processing levels;
- rotation;
- zoom and pan.

Because the seed and control settings are restored, the rock, lines, distractors, hidden solution, and arrow remain stable, and a solved configuration remains solved. `WasSolved` is also captured as state metadata, although no separate solved-state behaviour currently consumes it. Analysing a different fragment creates or restores a different state.

This state is currently retained by `BaseLevel` for the lifetime of that level instance. It is not presently serialized through the global save-game system.

## Implementation structure

The mechanic deliberately separates puzzle data from rendering:

```text
fragment position + monolith position + fragment variant + seed
                              |
                              v
                 FragmentPuzzleGenerator
                              |
                              v
                      FragmentPuzzle
                              |
                              v
                      FragmentCanvas
                              |
                              v
                  FragmentAnalysisUI controls
```

- `FragmentPuzzleGenerator` creates deterministic puzzle data from the sample seed and spatial context.
- `FragmentPuzzle` stores the true glyph, lines, veins, solution keys, rotation, direction, and distractor definitions.
- `FragmentCanvas` evaluates the current settings, calculates reconstruction quality, transforms the figure, and draws the result.
- `FragmentAnalysisUI` owns the buttons and sliders, forwards their state to the canvas, and captures/restores analysis state.
- `BaseLevel` opens the interface and keeps one `FragmentAnalysisState` per fragment position.
- `FragmentGenerationSettings` exposes puzzle composition, reveal behaviour, viewport, solution randomization, and rotation parameters in the Godot Inspector.
- `FragmentRockSettings` exposes the procedural rock palette, noise frequencies, grain, veins, opacity, and stroke widths.

The data-first structure means the visual presentation can be changed without changing the hidden solution, and future gameplay systems can inspect or validate the puzzle without depending on individual graphical nodes.
