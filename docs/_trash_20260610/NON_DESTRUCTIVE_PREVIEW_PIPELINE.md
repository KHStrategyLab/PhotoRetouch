# PhotoRetouch Non-Destructive Preview Pipeline

This file is UTF-8.

## Core Rule

`PreviewImage` is display output only.

Never use the current preview image as if it were the original source.

The visible preview is conceptually layered:

```text
ClearBasePreview
+ TransparentCorrectionLayerStack
= VisiblePreview
```

`ClearBasePreview` follows the configured preview setting and must not become blurry just because tiers exist.

`TransparentCorrectionLayerStack` is the staged set of retouch values composited over the clear base preview.

Example:

```text
ClearBasePreview
+ ShapeBalanceCorrectionLayer
+ SkinRetouchCorrectionLayer
+ ToneCorrectionLayer
+ DebugOverlayLayer optional
= VisiblePreview
```

These layers are logical correction layers. They may be rendered into one display bitmap for performance, but the engine must still rebuild them from the source of truth instead of baking them into the previous preview.

`TransparentCorrectionLayerStack` may be refreshed as FastPreview while dragging and replaced with QualityPreview after the user stops.

## Source Of Truth

Preview rendering must be regenerated from:

- `OriginalImage`
- `SnapshotMask`
- `ManualMaskOverride`
- `FaceManualAdjustOverride`
- `ShapeBalanceOptions`
- `ShapeBalanceMap`
- `SkinRetouchOptions`
- `StagePreset`
- `MaskQualityReport`

Formula:

```text
PreviewImage =
    Composite(
        ClearBasePreview,
        RenderCorrectionLayers(
            OriginalImage,
            SnapshotMask,
            ShapeBalanceOptions,
            SkinRetouchOptions))
```

The user may change controls in any order. The engine should always rebuild the current preview from the original image and the current options.

Preview tiers change display cost only. They do not change the source coordinate space for SnapshotMask, ShapeBalance, or SkinRetouch computation.

## Module Working Base

Each tool may treat the confirmed result from the previous module as its working base.

This is a staged working base, not a destructive overwrite of the original file.

Example:

```text
OriginalImage
-> ShapeBalanceWorkingBase
-> SkinRetouchWorkingBase
-> HairRetouchWorkingBase optional
-> ToneWorkingBase
-> VisiblePreview
```

Rules:

- ShapeBalance creates the geometry-corrected working base and matching mask set.
- SkinRetouch uses the ShapeBalance working base and balanced masks.
- SkinRetouch is a grouped module containing its own masked correction layers.
- HairRetouch, when enabled later, uses the current upstream working base and HairMask-derived candidate masks.
- Tone, curve, and later tools use the current upstream working base plus their own options.
- A later tool must not bake its output into the earlier module's base.
- If an earlier module changes, all later working bases are rebuilt from that changed upstream state.
- If only a later module changes, earlier working bases are reused.
- User control order must not change the final result.

Conceptually:

```text
CurrentToolInput =
    OutputOfPreviousEnabledModule(
        OriginalImage,
        CurrentOptions)
```

Then the current tool applies only its own masked correction layer and user-selected values.

SkinRetouch layer examples:

```text
SkinRetouchWorkingBase =
    ShapeBalanceWorkingBase
    + SkinToneMaskCorrectionLayer
    + BlemishMaskCorrectionLayer
    + AcneMaskCorrectionLayer
    + MoleAgeSpotMaskCorrectionLayer
    + PoreMaskCorrectionLayer
    + WrinkleMaskCorrectionLayer
    + BeardShadowMaskCorrectionLayer
    - HardProtectMask
```

`WrinkleMaskCorrectionLayer` is also a grouped layer, not a single global wrinkle blur.

Wrinkle correction layer examples:

```text
WrinkleMaskCorrectionLayer =
    UnderEyeWrinkleMaskCorrection
    + GlabellaWrinkleMaskCorrection
    + ForeheadWrinkleMaskCorrection
    + NasolabialFoldMaskCorrection
    + MouthCornerWrinkleMaskCorrection
    + NeckWrinkleMaskCorrection
    + NoseShadowWrinkleMaskCorrection
    - HardProtectMask
```

Wrinkle correction means softening selected wrinkle candidates while preserving facial structure, natural skin texture, eye edges, lip edges, nose detail, and expression lines that define the face.

`BeardShadowMaskCorrectionLayer` means shaving mark, blue cast, and beard-shadow softening.

Actual beard, mustache, and sideburn hair remain protected detail masks unless a later explicit beard tool says otherwise.

HairRetouch layer examples:

```text
HairRetouchWorkingBase =
    UpstreamWorkingBase
    + HairGlossMaskCorrectionLayer
    + HairColorMaskCorrectionLayer
    + GrayHairCandidateMaskCorrectionLayer
    + FlyawayHairMaskCorrectionLayer optional
    - FaceHardProtectMask
    - SkinRetouchAllowMask
```

`GrayHairCandidateMaskCorrectionLayer` means selective gray or white hair cover inside the hair region.

It must not repaint the whole hair area, smear hair texture, spill into skin, eyebrows, beard, mustache, glasses, or background, or override protected hair-edge detail.

## Interaction Rules

SkinRetouch slider or Skin Stage change:

- Reuse SnapshotMask.
- Reuse ShapeBalanceMap.
- Reuse BalancedImageBundle when possible.
- Re-run SkinRetouch only.
- Update Preview.

ShapeBalance slider or shape control change:

- Reuse SnapshotMask.
- Rebuild ShapeBalanceMap.
- Rebuild BalancedImage and BalancedMaskSet.
- Reapply current SkinRetouchOptions.
- Rebuild downstream working bases.
- Update Preview.

FaceManualAdjust:

- Rebuild SnapshotMask.
- Re-run ShapeBalance analysis.
- Rebuild ShapeBalanceMap and BalancedImageBundle.
- Re-run SkinRetouch.

ReAnalyze:

- Re-run full analysis.
- Rebuild SnapshotMask.
- Rebuild ShapeBalanceMap and BalancedImageBundle.
- Re-run SkinRetouch.

Before / After:

- Switch display only.
- Do not rerun analysis.
- Do not rerun filters.

## Dirty Flags

`PreviewRenderDirtyState` tracks:

- `MaskDirty`
- `ShapeDirty`
- `SkinDirty`
- `PreviewDirty`
- `ExportDirty`

Mask dirty sources:

- ReAnalyze
- FaceManualAdjust
- Image reload or file version change

Shape dirty sources:

- ShapeBalance or face shape control change
- FaceManualAdjust
- ReAnalyze

Skin dirty sources:

- SkinRetouch slider change
- Skin Stage change
- ShapeBalance change requiring SkinRetouch reapply

## Current Implementation Notes

- Photo selection shows the selected base image only.
- SnapshotMask and ShapeBalance analysis preparation start when the user opens a relevant right edit panel section.
- The selected base image is the clear preview foundation, not a low-quality placeholder.
- Skin Stage and skin slider changes reuse the cached `BalancedImageBundle`.
- Shape-related control changes mark ShapeDirty, which forces the next ShapeBalance bundle rebuild.
- Downstream tools use the latest confirmed upstream working base.
- `SetAdjustedImage(...)` only updates the displayed preview result.
- FastPreview and QualityPreview are staged correction-layer composites over the clear base preview.
- Rendering paths for ShapeBalance/SkinRetouch use `photo.BaseImage`, not `photo.Image`.
- FastPreview and QualityPreview are downscaled after OriginalImage-based rendering.

## Forbidden

- Do not feed PreviewImage into FaceAnalyzer.
- Do not build SnapshotMask from PreviewImage.
- Do not destructively accumulate filters on top of PreviewImage.
- Do not bake correction layers into the clear base preview.
- Do not move image and masks with different transforms.
- Do not keep masks in original coordinates after ShapeBalance.

## Final Sentence

Preview is a result, not a source.

The base preview stays clear.

Transparent correction layers change in stages over that base.

ShapeBalance moves image and masks together.

SkinRetouch is then reapplied on the balanced image and balanced mask set.
