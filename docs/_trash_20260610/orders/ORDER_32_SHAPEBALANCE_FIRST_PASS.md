# K Retouch Pro / PhotoRetouch - ORDER_32 ShapeBalance First Pass

This file is UTF-8.

## Stage

ShapeBalance first connection / face direction, roll, and balance before SkinRetouch.

## Status

Implemented / First pass pending portrait QA.

## Direction Change

ShapeBalance now runs before SkinRetouch.

Current pipeline direction:

```text
Original image
-> Original-coordinate SnapshotMask
-> ShapeBalance
-> Balanced image and Balanced masks
-> SkinRetouch in Balanced coordinates
-> HardProtectFinalRestore
```

ShapeBalance and SkinRetouch remain separate modules. ShapeBalance is geometry/warp work. SkinRetouch remains tone, texture, and detail work.

## Implemented First Pass

- Added `ShapeBalanceOptions`, `ShapeBalancePreset`, `ShapeBalanceAnalysisReport`, `ShapeBalanceReport`, `ShapeBalanceMap`, `BalancedImageBundle`, and `NostrilBalanceObservation`.
- Added `FaceSymmetryAnalyzer` for face roll, yaw-like bias, pitch-like bias, eye-level delta, nose-line tilt, chin-center delta, left-right score, suggested strength, and nostril observation.
- Added `ShapeBalanceMapBuilder` with weak global roll correction and local eye-level, nose-center, chin-center, and head-turn balance regions.
- Added `ShapeBalanceProcessor` to warp the image and all major masks with the same `ShapeBalanceMap`.
- Added `BalancedMaskQualityValidator` for first-pass safety scoring after the shape warp.
- Added `ShapeBalanceDebugExporter` for vector overlays, centerline overlays, balanced landmarks, balanced mask overlay, before/after compare, nostril observation, and JSON report.
- Connected SkinRetouch preview/reanalyze path to run on `BalancedImage` and `BalancedSnapshot`.
- Kept `HardProtectFinalRestore` as the last SkinRetouch processor stage.

## Conservative Limits

- ShapeBalance defaults are weak.
- Strong face reshaping is not implemented.
- Brow height and mouth-corner balance are report/debug placeholders until richer landmarks exist.
- Nostril size matching is not implemented. Only left/right nostril observation is recorded.
- Multi-face processing remains out of scope.

## Debug Outputs

ShapeBalance debug files are saved beside existing mask debug output:

- `debug_shape_face_centerline.png`
- `debug_shape_eye_level_delta.png`
- `debug_shape_eyebrow_balance.png`
- `debug_shape_mouth_corner_balance.png`
- `debug_shape_nose_line.png`
- `debug_shape_chin_center.png`
- `debug_shape_balance_vectors.png`
- `debug_shape_map_overlay.png`
- `debug_shape_balanced_landmarks.png`
- `debug_shape_balanced_mask_overlay.png`
- `debug_shape_before_after_compare.png`
- `debug_shape_nostril_observation.png`
- `debug_shape_balance_report.json`

## Next Step

Proceed to `ORDER_33_SHAPEBALANCE_MAP_AND_MASK_WARP` after real portrait QA confirms that the first-pass warp does not damage eyes, lips, nostrils, hair edges, glasses, or mask alignment.
