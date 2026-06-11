# PhotoRetouch Current State

Last updated: 2026-06-11

This document is the active project baseline.

## Product Direction

PhotoRetouch is a Windows WPF portrait retouching app for real photo-studio work.

The current reset point is an early-stage engine:

- Load a photo.
- Detect the face area.
- Estimate key landmarks.
- Build guide masks and guide overlays.
- Keep the existing panel UI and preview shell alive.

The current goal is not feature expansion. The current goal is to return the engine to a simple, inspectable guide pipeline.

## Current Priority

The active priority is GUIDE PIPELINE.

Current work is focused on:

- Face location detection.
- Landmark reliability.
- Guide mask generation.
- Transparent guide preview.
- Snapshot reuse for the same photo.
- Keeping the UI stable while the engine core is simplified.

## Program Policy

PhotoRetouch is not an automatic AI face-correction program.

Detected landmarks, ratios, masks, and confidence scores do not apply visible edits by themselves. They exist to locate facial structure, create guide regions, support preview/debug views, and prepare future manual tools.

User action is required before visible correction is applied.

Rules:

- Do not precompute or apply full-face retouch when a tab opens.
- Do not globally beautify, smooth, reshape, resize, or correct asymmetry by default.
- Do not force ideal facial proportions.
- Default state is detect only if needed, preserve identity, preserve shape, and show guide information safely.
- FaceBox is only an initial detection container.
- Final visible work should eventually come from anchored local tools, not from automatic whole-face processing.

## Engine Direction

The current runtime direction is:

```text
Photo load
-> face detection
-> landmark estimation
-> anchor/guide generation
-> preview/debug overlay
```

Current runtime concepts:

- `FaceAnalysisResult` stores face box, landmarks, confidence, angle, and debug warnings.
- `FaceSnapshotMaskSet` stores one analyzed result per photo.
- `FaceMaskSet` is currently used as the shared guide-mask container for preview/debug and future manual tools.
- Preview should remain visible and responsive even while retouch logic is reduced.

## User Experience Rules

The user is a working photographer. The app should be judged by working feel, not only by benchmark numbers.

Rules:

- Avoid fan-heavy processing during simple UI actions.
- Slider movement must not feel stuck.
- The preview must not blur or jump just because a panel opens.
- Same photo plus same guide settings should reuse cached analysis when possible.
- Switching away from a photo and back should restore that photo's session state until refresh.
- Reset should happen only on app start, refresh, explicit reset, or source image reload.
- Do not surprise the user with sudden layout changes or hidden processing.

## Current Working Status

Implemented or partially implemented:

- WPF desktop UI.
- Photo list and single-photo editing flow.
- Preview render tier model.
- C# preview engine path.
- Curve editor first pass.
- ShapeBalance scaffolding.
- Face detection and landmark analysis path.
- Snapshot analysis cache.
- AUTO MASK / guide preview surface.
- Per-photo session state memory.

Not final:

- Guide quality tuning.
- Landmark QA on real portraits.
- Stable guide overlay naming.
- Export pipeline polish.
- Batch workflow.
- Installer/package flow.

## Documentation Policy

This documentation set is the active baseline.

Older order documents remain historical reference only.
