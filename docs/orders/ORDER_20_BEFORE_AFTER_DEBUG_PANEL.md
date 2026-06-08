# K Retouch Pro / PhotoRetouch - ORDER_20

# Before / After + Debug Mask Panel

Status:
Implemented / Needs visual review

Prerequisite:
ORDER_19_STAGE_PRESET_REAL_TUNING

Next order:
ORDER_21_HARDPROTECT_TEST_SET

Goal:
Make result review and mask inspection easier without treating view changes as retouch or analysis changes.

## Implemented

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
- The last `RetouchStageProcessorOutput` is cached for read-only filter-mask inspection.
- Added filter output overlays:
  - BlemishCandidate
  - BlemishApplied
  - WrinkleCandidate
  - WrinkleApplied
  - WrinkleCombined
  - UnderEyeWrinkle
  - GlabellaWrinkle
  - TextureRestore
  - TextureStrength
  - PlasticRisk
  - HardProtectDiff

## Current Scope Boundary

ToneEven does not yet expose a dedicated candidate mask in the current engine. When ORDER_13 receives a dedicated `ToneEvenFilter`, its `ToneEvenMask`, `RednessCandidateMask`, and `DullnessCandidateMask` should be added to this selector.

Stage 1/5/10 compare output is currently exported through `RetouchDebugExporter`, not shown as an in-app compare panel yet.

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
