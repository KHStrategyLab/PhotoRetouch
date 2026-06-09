# K Retouch Pro / PhotoRetouch - ORDER_33 ShapeBalance Map And Mask Warp

This file is UTF-8.

## Stage

ShapeBalanceMap stabilization / synchronized image and mask warp.

## Status

Implemented / First pass pending real portrait QA.

## Goal

ORDER_33 stabilizes the ShapeBalance map created in ORDER_32.

The key rule is that the image and all masks must move through the same TransformMap. SkinRetouch now receives the balanced image and balanced masks, not the original-coordinate image and masks.

## Implemented

- Expanded `ShapeBalanceMap` with source/target image sizes, `GlobalTransform`, local warp regions, protected feature regions, warp strength metadata, creation time, and map version.
- Split global face roll/pitch-like adjustment into `ShapeBalanceGlobalTransform`.
- Kept eye-level, nose-center, chin-center, and weak head-turn balance as `LocalWarpRegions`.
- Added protected regions around eyes, lips, nostrils, hair boundary, beard, and glasses so local warp strength is damped around hard details.
- Image and masks continue to use the same `ShapeBalanceMap`.
- HardProtect masks are normalized after warp.
- SoftProtect and RetouchAllow are rebuilt after warp so HardProtect remains the highest-priority exclusion.
- `BalancedMaskQualityValidator` now includes `WarpAlignmentScore`.
- `NostrilBalanceObservation` now records before/after nostril shift and a first-pass safety flag.
- `BalancedImageBundle` keeps both the source snapshot and balanced snapshot for debug comparison.
- SkinRetouch stage changes reuse the per-photo cached `BalancedImageBundle`; ShapeBalance is not rerun for Skin Stage-only changes.
- ReAnalyze forces SnapshotMask and ShapeBalance regeneration.

## Debug Outputs Added

- `debug_shape_global_transform.png`
- `debug_shape_local_warp_regions.png`
- `debug_shape_warp_strength_map.png`
- `debug_shape_protected_regions.png`
- `debug_shape_original_landmarks.png`
- `debug_shape_image_before_after.png`
- `debug_shape_mask_before_after.png`
- `debug_shape_hardprotect_before_after.png`
- `debug_shape_nostril_before_after.png`
- `debug_shape_warp_alignment_score.png`
- `debug_balanced_mask_quality_report.json`

Existing ShapeBalance debug files from ORDER_32 remain active.

## Still Not Implemented

- Direct nostril size matching.
- Strong face-shape warp.
- Multi-face ShapeBalance.
- ShapeBalance public UI/toolset/preset controls.
- Dedicated richer eyebrow and mouth-corner landmark handling.

## Next Step

Proceed to `ORDER_34_SHAPEBALANCE_UI_TOOLSET_PRESET` after checking real portrait output for mask alignment, HardProtect preservation, and excessive face identity change.
