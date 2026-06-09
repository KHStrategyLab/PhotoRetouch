# PhotoRetouch Edit Screen Trigger Policy

This file is UTF-8.

## Core Rule

Photo selection is an edit preparation step.

Selecting a photo should not immediately run heavy retouch filters.

Default flow:

```text
Select photo
-> Enter single-photo edit view
-> Show only the selected image in the center
-> Activate the right edit panel
-> Prepare image load / face analysis / SnapshotMask / MaskQualityReport
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
- Prepare `SnapshotMask` in the background.
- Do not run ShapeBalance or SkinRetouch output generation automatically.

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

The selection flow now resets the selected photo preview to a clear unfiltered preview using the configured preview setting and no longer calls `ApplyPhotoAdjustmentsAsync` automatically when one photo is selected.

`EnsureSnapshotMaskForSelectedPhotoAsync` remains active as the preparation step.

Skin Stage changes reuse the cached ShapeBalance bundle from ORDER_33.
