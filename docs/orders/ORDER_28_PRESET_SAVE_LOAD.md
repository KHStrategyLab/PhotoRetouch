# K Retouch Pro / PhotoRetouch - ORDER_28

This file is UTF-8.

## Stage

Preset save / load

## Status

InProgress / Core implemented

## Prerequisite

ORDER_27_EXPORT_SAVE_OPTIONS

Proceed only after export/save quality options are complete.

## Next Order

ORDER_29_BATCH_PROCESSING

## Goal

Save current retouch settings as a preset and load them onto other photos.

Preset means retouch setting values only. It must not store photo-specific Snapshot Mask data.

## Save In Preset

- PresetName
- PresetId
- PresetVersion
- Stage
- SkinSmoothToolset
- BlemishToolset
- WrinkleToolset
- ToneEvenToolset
- TextureRestoreToolset
- ExportOptions optional
- CreatedAt
- UpdatedAt
- Notes optional

## Do Not Save In Preset

- OriginalImage
- FinalImage
- FaceBox
- FaceLandmarks
- FaceAngle
- SnapshotMask
- HardProtectMask
- SoftProtectMask
- RetouchAllowMask
- NostrilMask
- FaceParsing result
- MaskQualityReport
- ManualMaskOverride
- FaceManualAdjustOverride
- ImageHash
- Full private source photo path

## Initial File Policy

- Format: JSON
- Recommended extension: `.retouchpreset.json`
- Default presets may live in `presets/default/`.
- User presets may live in `presets/user/` during development.
- User presets should not be committed by default.

## Default Preset Candidates

- Natural
- Studio
- Beauty
- Strong
- ID Photo optional

## Required Flow

Save preset:

- Read current retouch toolset values.
- Create `RetouchPreset`.
- Save JSON.
- Refresh preset list.
- Do not run face analysis.
- Do not rebuild Snapshot Mask.
- Do not run RetouchStageProcessor.

Load preset:

- Load preset JSON.
- Validate `PresetVersion`.
- Apply values to retouch toolsets and slider UI.
- Run RetouchStageProcessor only.
- Reuse current Snapshot Mask.
- Do not run face analysis, mask warp, nostril detection, parsing, or mask quality validation.

## Stage Gate Rule

Preset stage is only the requested stage.

Actual applied stage remains:

```text
AppliedStage = min(Preset.Stage, Current MaskQualityReport.MaxAllowedStage)
```

## UI Targets

- Preset select
- Preset save
- Preset delete
- Preset rename optional
- Restore defaults

## Guardrails

- Do not implement before ORDER_27 is complete.
- Do not store Snapshot Mask or photo-specific analysis data.
- Do not redesign UI broadly.
- Do not change filters in this order.
- Do not implement batch processing in this order.

## Completion Criteria

- `RetouchPreset` structure exists.
- Current retouch toolset can be saved.
- Preset can be loaded and applied.
- Slider UI updates after load.
- Preset load runs RetouchStageProcessor only.
- Snapshot Mask is reused.
- Default and user presets are separated.
- PresetVersion exists.
- Corrupt preset files do not crash the app.
- Build passes.

## Current Implementation Notes

- Added `RetouchPreset`.
- Added `RetouchPresetService`.
- Default and user preset directories are separated under local AppData:
  - `PhotoRetouch/presets/default/`
  - `PhotoRetouch/presets/user/`
- Default presets are generated:
  - Natural
  - Studio
  - Beauty
  - Strong
- Preset JSON extension:
  - `.retouchpreset.json`
- Presets store only toolset values and stage.
- Presets do not store SnapshotMask, FaceBox, FaceLandmarks, image hash, or private image path.
- UI binding for preset select/save/load is still pending.
