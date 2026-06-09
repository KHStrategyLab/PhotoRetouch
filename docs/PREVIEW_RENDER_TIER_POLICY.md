# PhotoRetouch Preview Render Tier Policy

This file is UTF-8.

## Preview Tiers

Preview tiers are not a plan to blur the base preview.

The base preview must stay clear according to the configured preview setting.

Retouch changes are rendered as transparent correction layers over that clear base preview:

- `LowPreview`: clear unfiltered base preview.
- `FastPreview`: quick correction-layer composite while the user is dragging.
- `QualityPreview`: review-quality correction-layer composite after the user stops dragging.
- `ExportRender`: final original-resolution render.

PhotoRetouch uses four render tiers:

- `LowPreview`
- `FastPreview`
- `QualityPreview`
- `ExportRender`

## LowPreview

Purpose:

- Show a selected photo immediately using the normal preview setting.
- Keep photo selection responsive.
- Show the unfiltered image clearly before the user starts editing.

Rules:

- Do not force a smaller display size than the configured preview setting.
- Do not use LowPreview as a blurred or deliberately low-quality image.
- Do not use LowPreview for analysis.
- Do not use LowPreview for filter quality judgement.
- Do not build SnapshotMask from LowPreview.

## FastPreview

Purpose:

- Slider drag feedback.
- ShapeBalance adjustment feedback.

Rules:

- May use reduced resolution.
- Prioritize response over judgement quality.
- SnapshotMask remains based on OriginalImage coordinates.
- Do not create a computation SnapshotMask from FastPreview.
- If a mask is shown as an overlay, map the original-coordinate mask to display coordinates only for visualization.

## QualityPreview

Purpose:

- Review after the user stops dragging.
- Inspect the current edit on screen.

Rules:

- Render from OriginalImage and current options.
- Display a downscaled result at screen-needed size.
- Use scaled preview output only for display, not as a new source.

## ExportRender

Purpose:

- Final save/export.

Rules:

- Render again from OriginalImage.
- Use original resolution.
- Never save PreviewImage.

## Current Event Flow

```text
ImageLoaded
-> Clear LowPreview base display with configured preview sizing
-> SnapshotMask preparation from OriginalImage

SliderDragging
-> FastPreview correction-layer composite over the clear base preview

SliderReleased
-> QualityPreview correction-layer composite over the clear base preview

SaveClicked
-> ExportRender
```

## Implementation Notes

- `PreviewRenderTier` defines the tier.
- `PreviewRenderTierPolicy` maps each tier to preview sizing intent.
- `PreviewSourceFactory` creates tier-specific sources from `BaseImage`.
- `PhotoItem` caches preview sources per tier.
- `LowPreview` uses the configured preview sizing, not the visible viewport size.
- FastPreview and QualityPreview represent the current transparent correction-layer stack, not a new source image.
- Correction layers may be flattened into one display bitmap, but only after being regenerated from OriginalImage and current options.
- Each tool can use the previous enabled module's confirmed working base, but that base is regenerated from OriginalImage and current options when upstream settings change.
- The stored SnapshotMask remains original-coordinate.
- ShapeBalance and SkinRetouch calculations run in OriginalImage coordinates.
- Display output is downscaled after rendering according to the current tier.

## Forbidden

- Do not feed PreviewImage into FaceAnalyzer.
- Do not create SnapshotMask from PreviewImage.
- Do not accumulate filters over PreviewImage.
- Do not save PreviewImage.
