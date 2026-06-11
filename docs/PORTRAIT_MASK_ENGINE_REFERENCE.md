# PORTRAIT MASK ENGINE REFERENCE

This document defines the current portrait analysis and guide-engine direction for PhotoRetouch.

Read this document when working on:

- `FaceSnapshotMaskSet`
- face detection
- landmarks
- guide masks
- debug overlay export
- snapshot reuse
- preview-safe analysis flow

## Current Direction

The current engine has been reset to an early-stage pipeline:

```text
source image
-> face detection
-> landmark estimation
-> anchor/guide generation
-> guide preview and debug export
```

The current goal is not a finished retouch safety engine.

The current goal is to make analysis, guide masks, and preview overlays simpler and easier to inspect.

## Rules

- Do not strengthen retouch filters before the guide output is trustworthy.
- Analyze one photo once and reuse the snapshot while preview values change.
- Stage and slider changes must not rebuild the snapshot by themselves.
- FaceBox is an initial detection container only.
- Visible correction should not happen automatically from detected landmarks.
- Landmark and guide data are preparation layers for future manual tools.

## Current Runtime Pieces

- `OpenCvFaceAnalyzer` provides the initial face box and anchor points.
- `StandardMaskWarpEngine` is currently the guide-engine entry point.
- `FaceAnalysisResult` stores box, landmarks, confidence, angle, and warnings.
- `FaceMaskSet` currently acts as the shared guide-mask container for preview/debug surfaces.
- `MaskQualityReport` is still used as a simple runtime health report.
- `SnapshotMaskCacheKey` stores image id, image size, face box, face angle, crop version, and mask version.

## Guide Output Expectations

The active guide output should include:

- Face box
- Landmarks
- Skin guide mask
- Eye guide mask
- Eyebrow guide mask
- Lip guide mask
- Inner-mouth guide mask
- Nose guide mask
- Hair guide mask
- Beard guide mask
- Glasses guide mask
- Final guide overlay

These masks are guide data. They are not a promise of final correction quality.

## Cache Rules

- Rebuild the snapshot for new image load, explicit re-analysis, crop or rotation changes, manual face-position changes, or source file changes.
- Do not rebuild the snapshot only because stage, preview, or slider values changed.

## Debug Rules

- Required debug images should focus on analysis and guide readability.
- Debug export is useful only when it helps inspect the current guide quality.
- Avoid adding noisy debug paths that do not help face-location or landmark verification.
