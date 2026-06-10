# Engine Modules

Last updated: 2026-06-10

This document lists the current source-code modules at a practical level.

## UI Entry

Main files:

- `MainWindow.xaml`
- `MainWindow.xaml.cs`

Responsibilities:

- Photo list.
- Preview area.
- Right retouch panel.
- Slider event handling.
- Preview render orchestration.
- AUTO MASK preview.
- ShapeBalance and SkinRetouch trigger flow.

## Photo Model

Main file:

- `Models/PhotoItem.cs`

Responsibilities:

- Source path and base image.
- Current display image.
- Thumbnail.
- Per-photo retouch state.
- Snapshot mask cache reference.
- ShapeBalance cache.
- AUTO MASK preview cache.
- Preview zoom and pan state.

Important properties:

- `BaseImage`
- `Image`
- `RetouchState`
- `SnapshotMaskSet`
- `CachedShapeBalanceBundle`
- `AverageFaceColorMaskPreviewCache`

## Preview Engine

Main files:

- `Tools/PhotoAdjustment/IPreviewEngine.cs`
- `Tools/PhotoAdjustment/CSharpPreviewEngine.cs`
- `Tools/PhotoAdjustment/PreviewEngineFactory.cs`
- `Tools/PhotoAdjustment/PreviewSourceFactory.cs`
- `Models/PreviewRenderTier.cs`

Current state:

- C# preview engine is the active path.
- Native CPU and GPU paths are future work.
- Preview source should be display-sized for interaction.
- Export render remains conceptually original-resolution.

## Snapshot Mask

Main files:

- `Tools/Masking/SnapshotMaskBuilder.cs`
- `Tools/Masking/SnapshotMaskDiskCache.cs`
- `Tools/Masking/FaceSnapshotMaskSet.cs`
- `Tools/Masking/FaceMaskSet.cs`
- `Tools/Masking/StandardMaskWarpEngine.cs`
- `Tools/Masking/NoFaceParsingDetector.cs`

Current state:

- Snapshot masks are per-photo.
- Snapshot mask rebuild is for image/source/face-work-area/manual-mask/reanalyze changes.
- Stage and slider changes should not rebuild SnapshotMask.
- Real face parsing AI is not connected.
- `NoFaceParsingDetector` is a fallback scaffold.

## AUTO MASK

Main files:

- `Tools/Masking/AverageFaceColorMaskBuilder.cs`
- `Tools/Masking/DebugMaskExporter.cs`
- `Models/PhotoItem.cs`
- `MainWindow.xaml.cs`

Current state:

- Skin-color/range based.
- Not AI.
- Default range control is `skin_mask_range = 75`.
- Fills small enclosed mask holes.
- Reapplies feature block mask after filling.
- Uses per-photo cache for same photo and same range.

## Skin Retouch Pipeline

Main files:

- `Tools/Masking/RetouchStageProcessor.cs`
- `Tools/Masking/RetouchOptions.cs`
- `Tools/Masking/RetouchToolset.cs`
- `Tools/Masking/RetouchProcessReport.cs`
- `Tools/Masking/BlemishReduceFilter.cs`
- `Tools/Masking/WrinkleSoftReduceFilter.cs`
- `Tools/Masking/TextureRestoreFilter.cs`
- `Tools/Masking/HardProtectFinalRestoreFilter.cs`

Current state:

- First-pass pipeline exists.
- Final quality is not finished.
- HardProtect should restore original protected pixels at the end.
- Stronger filter tuning should wait until AUTO MASK is reliable.

## ShapeBalance

Main files:

- `Tools/Shape/ShapeBalanceProcessor.cs`
- `Tools/Shape/ShapeBalanceMap.cs`
- `Tools/Shape/ShapeBalanceMapBuilder.cs`
- `Tools/Shape/ShapeBalanceModels.cs`
- `Tools/Shape/ShapeBalanceToolset.cs`
- `Tools/Shape/BalancedMaskQualityValidator.cs`

Current state:

- ShapeBalance is a geometry module, not a skin filter.
- It can run before SkinRetouch when shape controls are active.
- It should move image and masks together.
- SkinRetouch should then use balanced image and balanced masks.

## Debug And Reports

Main files:

- `Tools/Masking/DebugMaskExporter.cs`
- `Tools/Masking/RetouchDebugExporter.cs`
- `Tools/Integration/PreviewIntegrationDebugExporter.cs`
- `Tools/Shape/ShapeBalanceDebugExporter.cs`

Current state:

- Debug masks are useful during engine development.
- Debug output should not spam Visual Studio output.
- Debug file generation should be tied to user action or explicit debug paths.

## Reference Documents

Reference material lives under:

- `docs/reference/`

Current reference:

- `docs/reference/skin_retouching_photoshop_tutorial_workflow.md`

Reference documents are not active implementation orders. They are used to compare ideas and decide future behavior.

