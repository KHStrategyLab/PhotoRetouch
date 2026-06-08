# K Retouch Pro / PhotoRetouch - ORDER_30

# High-Resolution Image Processing / Performance Optimization

Status:
Queued / Planned

Prerequisite:
ORDER_29_BATCH_PROCESSING

Next order:
ORDER_31_CACHE_MEMORY_CLEANUP

Goal:
Stabilize memory use, processing speed, and preview responsiveness for high-resolution portrait photos and batch processing.

Core rule:
Preview processing and final export processing must be separated.

Preview should be fast and may use a downscaled working image.
Export should render from original resolution or a full-resolution-safe path.

## Main Tasks

1. Separate PreviewImage processing from ExportImage processing.
2. Check memory use when high-resolution photos are loaded.
3. Re-verify that Stage and Slider changes do not rerun analysis stages.
4. Strengthen reuse rules for SnapshotMask, DetailLayer, CandidateMask, and TextureRestoreMask.
5. In Batch Processing, release per-image temporary objects after each item.
6. Create and save DebugMask images only when debug output is enabled.
7. Record per-stage timings in `PipelineDebugReport`.

## Processing Profile

Planned structure:

- `PreviewMaxWidth`
- `PreviewMaxHeight`
- `ExportUseOriginalResolution`
- `MaxWorkingImagePixels`
- `EnableDownscalePreview`
- `EnableFullResolutionExport`

Initial defaults:

- `PreviewMaxWidth`: 1600
- `PreviewMaxHeight`: 1600
- `ExportUseOriginalResolution`: true
- `EnableDownscalePreview`: true
- `EnableFullResolutionExport`: true

## Cache Reuse Rules

Stage changes reuse:

- `SnapshotMask`
- `MaskQualityReport`
- `BlemishCandidateMask`
- `WrinkleMaskSet`
- `ToneEvenMask`
- `DetailLayer`
- `TextureRestoreMask`

Slider changes reuse:

- `SnapshotMask`
- `MaskQualityReport`
- Candidate masks
- `DetailLayer`

Regenerate only when:

- New image load
- ReAnalyze click
- FaceManualAdjust applied
- MaskVersion changed
- AnalyzerVersion changed
- Crop or rotation changed

## Timing Fields To Add Later

`PipelineDebugReport` should eventually include:

- `ImageLoadTimeMs`
- `FaceAnalyzeTimeMs`
- `StandardMaskWarpTimeMs`
- `NostrilDetectTimeMs`
- `FaceParsingTimeMs`
- `SnapshotMaskBuildTimeMs`
- `MaskQualityTimeMs`
- `SkinSmoothTimeMs`
- `BlemishReduceTimeMs`
- `WrinkleReduceTimeMs`
- `ToneEvenTimeMs`
- `TextureRestoreTimeMs`
- `HardProtectRestoreTimeMs`
- `PreviewRenderTimeMs`
- `ExportTimeMs`
- `TotalPipelineTimeMs`

## Batch Memory Rules

Initial batch processing remains sequential:

- `MaxParallelCount = 1`
- Keep `BatchReport`, `ItemReport`, saved output paths, and failure reasons.
- Do not retain every full-resolution source image, detail layer, debug image set, or intermediate filter result after each item is finished.

## Debug Optimization Rules

Debug mode off:

- Avoid unnecessary debug image rendering.
- Do not save debug files automatically.

Debug mode on:

- Render only selected debug masks when possible.
- Save all debug images only when explicitly requested.

## Mask Resolution Policy

Recommended:

- Store masks in original coordinate space.
- Downscale masks for preview display.
- Use original-coordinate masks for export.

Planned fields:

- `SourceImageWidth`
- `SourceImageHeight`
- `PreviewScale`
- `MaskCoordinateSpace`

Preferred `MaskCoordinateSpace`:

- `Original`

## Do Not Do In This Order

- No new filter implementation.
- No filter quality tuning.
- No StagePreset tuning.
- No large SnapshotMask redesign.
- No FaceDetection / Landmark replacement.
- No NostrilDetector redesign.
- No FaceParsing redesign.
- No Toolset / Slider restructuring.
- No full UI redesign.
- No multi-face processing.
- No advanced parallel processing.
- No GPU optimization.

## Completion Criteria

- Preview and Export processing paths are conceptually separated.
- Preview downscaling is available.
- Export can save at original resolution.
- Stage and Slider changes reuse SnapshotMask.
- Debug mode off avoids unnecessary debug image creation.
- Batch temporary memory is cleaned per item.
- `PipelineDebugReport` records per-step timings.
- High-resolution images do not freeze the app or grow memory excessively.
- Build has no errors.

## Branch Signals

If complete and build is clean:
Next order is `ORDER_31_CACHE_MEMORY_CLEANUP`.

If preview is slow:
Reduce `PreviewMaxWidth` / `PreviewMaxHeight`, then review slider debounce.

If export masks are misaligned:
Fix mask coordinate space to original.

If batch memory grows:
Inspect per-image temporary object release and disposable resources.

If debug image generation is slow:
Block debug generation when debug mode is off.

If Stage changes rerun analysis:
Return to ORDER_16 / ORDER_18 event flow review.

