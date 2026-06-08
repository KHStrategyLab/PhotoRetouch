# K Retouch Pro / PhotoRetouch - ORDER_17

# Project Structure Toolset / Slider / StagePreset Alignment

Status:
Implemented / Needs UI binding review

Prerequisite:
ORDER_16_PIPELINE_INTEGRATION_REVIEW

Next order:
ORDER_18_VIEWMODEL_UI_BINDING_REVIEW

Goal:
Align the current PhotoRetouch project structure so Stage, existing sliders, retouch toolsets, and applied options can flow into the retouch pipeline without rebuilding SnapshotMask.

## Project Structure Report

- ProjectType: WPF desktop app, .NET 8, x64.
- MainViewFile: `MainWindow.xaml`
- MainViewCodeBehind: `MainWindow.xaml.cs`
- MainViewModelFile: none. The current UI still uses `MainWindow` as the binding source.
- ExistingModelsFolder: `Models`
- ExistingServicesFolder: none.
- ExistingFiltersFolder: current filter code lives under `Tools/Masking` and `Tools/PhotoAdjustment`.
- ExistingViewModelsFolder: none.
- ExistingPresetsFolder: none.
- ExistingOptionsFile: `Tools/Masking/RetouchOptions.cs`
- ExistingStageControl: top toolbar Stage slider bound to `DummyMaskStageValue`
- ExistingSliderControls: right-side `RetouchSection` / `RetouchControl` collection in `MainWindow.xaml.cs`
- MissingFolders: ViewModels, Services/Retouch, Presets

Decision:
Do not create a new ViewModel or move large UI code in this order. Keep the current structure and add the minimum model objects under `Tools/Masking`.

## Implemented

- Added `RetouchToolset`.
- Added `SkinSmoothToolset`.
- Added `BlemishToolset`.
- Added `ToneEvenToolset`.
- Reused existing `WrinkleToolset`.
- Reused existing `TextureRestoreToolset`.
- Added `MaskDebugOptions`.
- Added `RetouchUserOverrideFlags`.
- Added `AppliedRetouchOptions`.
- Added `RetouchControl.DefaultValue` so slider overrides can be detected without guessing.
- Added a collapsed right-side wrinkle section:
  - 전체
  - 눈밑
  - 미간
  - 이마
  - 팔자
  - 입가
  - 목
  - 코그림자
- Connected the Stage slider and retouch sliders to create `RetouchToolset` values for the mask retouch pipeline.
- `RetouchStageProcessor` now creates `AppliedRetouchOptions` and uses toolset-adjusted StagePreset values.
- Slider changes while pipeline retouch preview is on call the retouch pipeline and do not request SnapshotMask rebuild.

## Current Mapping

Basic skin sliders:

- `skin_smooth` -> `SkinSmoothToolset.GlobalSmoothAmount`
- `skin_texture_protect` -> `SkinSmoothToolset.DetailPreserveAmount` and `TextureRestoreToolset`
- `blemish_remove`, `acne_remove`, `mole_age_spot_remove` -> `BlemishToolset`
- `tone_even` -> `ToneEvenToolset`
- `pore_clean` -> `TextureRestoreToolset.PoreTextureAmount`

Wrinkle sliders:

- `wrinkle_global` -> `WrinkleToolset.GlobalWrinkleAmount`
- `wrinkle_under_eye` -> `UnderEyeWrinkleAmount`
- `wrinkle_glabella` -> `GlabellaWrinkleAmount`
- `wrinkle_forehead` -> `ForeheadWrinkleAmount`
- `wrinkle_nasolabial` -> `NasolabialFoldAmount`
- `wrinkle_mouth_corner` -> `MouthCornerWrinkleAmount`
- `wrinkle_neck` -> `NeckWrinkleAmount`
- `wrinkle_nose_shadow` -> `NoseShadowWrinkleAmount`

## Important Behavior

- Stage still provides the default preset.
- A slider only becomes an override when its value differs from its control default.
- Stage and slider changes do not create a new SnapshotMask.
- `AppliedRetouchOptions` carries requested stage, applied stage, toolset, quality report, and actual retouch amounts.

## Needs Follow-Up In ORDER_18

- Verify the WPF binding flow visually.
- Decide whether the top Stage slider should be renamed from the temporary dummy naming in code.
- Add stronger UI status for slider override state if useful.
- Decide whether stage changes should update visible slider positions to stage defaults or keep the current override-only behavior.
- Dedicated `ToneEvenFilter` is still pending from ORDER_13. This order only connected the existing simple ToneEven stage.

## Build

Build command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

