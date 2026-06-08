# K Retouch Pro / PhotoRetouch - ORDER_20

# Before / After + Debug Mask Panel

Status:
InProgress / First UI pass implemented

Prerequisite:
ORDER_19_STAGE_PRESET_REAL_TUNING

Next order:
ORDER_21_HARDPROTECT_TEST_SET

Goal:
Make result review and mask inspection easier without treating view changes as retouch or analysis changes.

## Implemented In This Pass

- Kept `Original` / `Preview` switching as a view-only operation.
- Added a Debug Mask selector that appears only while mask debug view is enabled.
- Added `DebugMaskOption` for selectable mask views.
- Added selectable overlays:
  - Final
  - Skin
  - HardProtect
  - SoftProtect
  - RetouchAllow
  - Eye
  - Eyebrow
  - Lip
  - InnerMouth
  - Nostril
  - Hair
  - Beard / Mustache
  - Glasses
- `DebugMaskExporter.CreateMaskOverlayPreview(...)` now renders a selected mask over the original image.
- Debug mask changes use `SnapshotMaskBuilder.GetOrCreate(...)`.
- Debug mask changes do not run the retouch filters.
- Turning mask debug off restores the image that was visible before the debug overlay was applied.
- Changing to multi-select or another photo exits mask debug view and restores the previous preview.

## Current Scope Boundary

This pass focuses on SnapshotMask / part-mask inspection.

Filter candidate masks such as BlemishCandidateMask, WrinkleAppliedMask, ToneEvenMask, TextureRestoreMask, HardProtectDiff, and Stage compare panel still need a second ORDER_20 pass that reads the last `RetouchStageProcessorOutput` instead of rerunning the pipeline.

## View Rules

- Before / After: display switch only.
- Debug mask selector: overlay switch only.
- Stage / Slider changes: retouch filter only, SnapshotMask reuse.
- ReAnalyze: explicit SnapshotMask rebuild.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

