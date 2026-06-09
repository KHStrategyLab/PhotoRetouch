# PhotoRetouch - ORDER_32A Preview Tier Original Coordinates

This file is UTF-8.

## Stage

Preview Render Tier / Original-coordinate engine lock.

## Status

Implemented / Policy correction after initial PreviewTier wiring.

## Core Rule

Preview tiers are display tiers, not computation coordinate spaces.

The engine must not treat `PreviewImage` as a source image.

The first visible preview is a clear base preview.

FastPreview and QualityPreview are staged transparent correction-layer composites placed over that clear base preview.

## Definitions

- `SourceFile`: real file selected by the user.
- `OriginalImage`: full-resolution decoded working image from SourceFile.
- `LowPreview`: immediate clear unfiltered display after selection, using the configured preview setting.
- `FastPreview`: fast display feedback while dragging sliders or shape controls.
- `QualityPreview`: review preview after user stops manipulating controls.
- `ExportRender`: final original-resolution render for saving.

## Fixed Rules

- SnapshotMask is created and stored in OriginalImage coordinates.
- ShapeBalance is calculated in OriginalImage coordinates.
- SkinRetouch is calculated in OriginalImage/Balanced Original coordinates.
- FastPreview and QualityPreview downscale display output after rendering.
- LowPreview is display-only and must not be forced below the configured preview quality.
- Tiers must not turn the base preview into a blurred placeholder.
- Correction layers can be stacked logically, but must be regenerated from OriginalImage and current options.
- Correction layers must not be baked into the base preview.
- A completed upstream module can become the next module's working base.
- Working bases are cached stage outputs, not destructive source replacements.
- If an upstream module changes, downstream module results must be rebuilt with the user's current values.
- Do not create SnapshotMask from PreviewImage.
- Do not feed PreviewImage into FaceAnalyzer.
- Do not accumulate filters over PreviewImage.
- Do not export PreviewImage.

## Current Implementation

- Photo selection displays `LowPreview`.
- `LowPreview` follows the configured preview setting instead of the visible viewport size.
- SnapshotMask preparation still uses `photo.BaseImage`.
- ShapeBalance/SkinRetouch preview rendering uses `photo.BaseImage`.
- The rendered result is downscaled by `PreviewRenderTierPolicy` only before display.
- `SnapshotMaskPreviewScaler` was removed so display-sized masks are not used as computation snapshots.

## Flow

```text
ImageLoaded
-> Clear LowPreview base display
-> OriginalImage load
-> Original-coordinate SnapshotMask
-> Right panel interaction
-> Original-coordinate render
-> FastPreview or QualityPreview correction-layer composite over the clear base preview
-> SaveClicked
-> ExportRender from OriginalImage
```

## Final Sentence

Build from the original, compute in original coordinates, keep the base preview clear, and refresh transparent correction layers in stages over it.
