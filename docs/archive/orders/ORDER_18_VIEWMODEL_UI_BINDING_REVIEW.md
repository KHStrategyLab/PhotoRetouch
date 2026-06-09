# K Retouch Pro / PhotoRetouch - ORDER_18

# ViewModel / UI Binding Connection Review

Status:
Implemented / Needs visual review

Prerequisite:
ORDER_17_PROJECT_STRUCTURE_TOOLSET_SLIDER

Next order:
ORDER_19_STAGE_PRESET_REAL_TUNING

Goal:
Verify that the current UI controls for Stage and sliders flow into `RetouchToolset`, `AppliedRetouchOptions`, and `RetouchStageProcessor` without rebuilding SnapshotMask.

## Current UI Structure

- UI type: WPF.
- MVVM status: no separate ViewModel yet. `MainWindow` is currently the binding source.
- Stage control: top toolbar slider bound to `DummyMaskStageValue`.
- Right-side controls: `RetouchSections` / `RetouchControl`.
- Pipeline trigger: `파이프라인 보정` button enables the mask retouch pipeline.

## Implemented In This Order

- Added `RetouchBindingReport`.
- Added `RetouchBindingStatusText` to the top toolbar.
- Added a minimal `재분석` button.
- Stage changes record a `StageChanged` report.
- Slider changes record a `SliderChanged` report.
- ReAnalyze uses `SnapshotMaskBuilder.Rebuild`.
- Stage/Slider pipeline updates use `SnapshotMaskBuilder.GetOrCreate`.
- Binding status shows:
  - event source
  - cache/rebuild state
  - requested stage
  - applied stage
  - limited state when MaskQualityGate restricts strong retouch

## Verified Binding Flow

Stage:

```text
DummyMaskStageValue
-> CreateRetouchOptions
-> CaptureRetouchToolset
-> AppliedRetouchOptions.Create
-> RetouchStageProcessor.Process
-> Preview image update
```

Slider:

```text
RetouchControl.Value
-> RetouchControl_PropertyChanged
-> SetPendingRetouchBindingEvent
-> ApplyDummyMaskRetouchAsync
-> CreateRetouchOptions
-> CaptureRetouchToolset
-> AppliedRetouchOptions.Create
-> RetouchStageProcessor.Process
-> Preview image update
```

Snapshot rules:

- Stage changes call `GetOrCreate`, not `Rebuild`.
- Slider changes call `GetOrCreate`, not `Rebuild`.
- `재분석` calls `Rebuild`.

## Current UI Status

Displayed in top toolbar:

- Snapshot created/reuse counters.
- Requested / Applied / Max stage.
- Last binding report.

The current display is functional and compact. ORDER_20 can later move this into a cleaner Debug Mask Panel.

## Known Follow-Up

- This is still not a full ViewModel architecture.
- Before/After is still a view toggle, but deeper Debug Panel handling belongs to ORDER_20.
- Save/Export still belongs to ORDER_27 and is not redesigned here.
- Dedicated `ToneEvenFilter` remains a separate ORDER_13 follow-up.
- Advanced tone sliders for redness/yellow/dullness/patchy tone are not yet visible. The current `tone_even` slider maps to `ToneEvenToolset` globally.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

