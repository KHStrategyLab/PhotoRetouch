# PhotoRetouch Edit Screen Trigger Policy

This file is UTF-8.

## Core Rule

Photo selection is a viewing step.

Selecting a photo should not immediately run heavy retouch filters.

Default flow:

```text
Select photo
-> Enter single-photo edit view
-> Show only the selected image in the center
-> Wait for the user to open a right edit panel section
-> Prepare image load / face analysis / SnapshotMask / MaskQualityReport from the right-panel trigger
-> Start preview rendering only when the user changes Stage, ShapeBalance, SkinRetouch, sliders, presets, or debug view
```

## Single Image Edit View

The edit view works on one image at a time.

Center:

- Selected photo
- Before / After
- Optional debug overlay

Right panel:

- Shape Balance
- Skin Retouch
- Stage
- Sliders
- Preset
- Debug Mask
- Export

Batch processing must remain a separate screen or separate workflow.

## Trigger Rules

On photo selection:

- Show the selected image using the configured preview setting.
- Reset preview pan/zoom to fit.
- Do not prepare `SnapshotMask` yet.
- Do not prepare ShapeBalanceMap, ShapeDragSession, FastWarpPreview, SkinRetouch, or ExportRender.
- Do not run ShapeBalance or SkinRetouch output generation automatically.

On right edit panel section opened:

- Prepare `OriginalImage` work source.
- Run FaceAnalyzer through `SnapshotMaskBuilder`.
- Prepare `SnapshotMask`, `SkinToneMask`, `HardProtectMask`, `SoftProtectMask`, `FaceOnlyWarpMask`, and ShapeBalance analysis.
- Prepare the FastPreview source cache.
- Do not generate ShapeBalanceMap or SkinRetouch output until the user actually changes a tool value.

On Shape slider dragging:

- Do not rebuild SnapshotMask.
- Do not rerun FaceAnalyzer or FaceParsing.
- Do not run full-resolution ShapeBalance repeatedly.
- Do not rerun the full SkinRetouch pipeline.
- Use GuideOnly/FastPreview behavior while dragging, then render QualityPreview on release.

On Shape-related changes:

- Rebuild ShapeBalanceMap.
- Rebuild BalancedImage and BalancedMaskSet.
- Re-run SkinRetouch.
- Update Preview.

On Skin-related changes:

- Do not rebuild SnapshotMask.
- Do not rerun ShapeBalance.
- Reuse BalancedImageBundle.
- Re-run SkinRetouch only.
- Update Preview.

On Before / After:

- Switch view only.
- Do not rerun analysis.
- Do not rerun filters.

## Implementation Status

The selection flow now resets the selected photo preview to a clear unfiltered preview using the configured preview setting and no longer calls `ApplyPhotoAdjustmentsAsync` or SnapshotMask preparation automatically when one photo is selected.

`PrepareEditingForSelectedPhotoAsync` starts the preparation step when a mask/shape-related right panel section is opened.

Skin Stage changes reuse the cached ShapeBalance bundle from ORDER_33.
