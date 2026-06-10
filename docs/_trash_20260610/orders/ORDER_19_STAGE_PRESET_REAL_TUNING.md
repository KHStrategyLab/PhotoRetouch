# K Retouch Pro / PhotoRetouch - ORDER_19

# Stage 1-10 Preset Real Tuning

Status:
Implemented / Needs visual review

Prerequisite:
ORDER_18_VIEWMODEL_UI_BINDING_REVIEW

Next order:
ORDER_20_BEFORE_AFTER_DEBUG_PANEL

Goal:
Tune Stage `1-10` so the numbers behave like photographer-readable retouch strength presets instead of arbitrary values.

## Implemented In This Order

- `StagePresetMapper.Map(...)` now applies an ORDER_19 tuning layer over the existing preset table.
- `StagePresetMapper.GetAll()` exposes the final Stage `1-10` values for debug export.
- Stage groups are tuned as:
  - Stage 1-3: natural, high texture keep, weak correction.
  - Stage 4-6: studio/profile, visible but controlled cleanup.
  - Stage 7-8: stronger beauty cleanup with texture restore.
  - Stage 9-10: strong test/sample cleanup, still protected by HardProtect.
- `RetouchDebugExporter` now saves Stage `1`, `5`, and `10` verification files.
- `debug_stage_1_5_10_compare.png` is generated from cached Stage outputs.
- `debug_stage_preset_values.json` records the final Stage `1-10` values.
- `debug_stage_gate_report.json` records RequestedStage, AppliedStage, MaxAllowedStage, mask quality, and HardProtect result.
- Stage `1`, `5`, and `10` now save HardProtect diff previews:
  - `debug_stage_hardprotect_diff_1.png`
  - `debug_stage_hardprotect_diff_5.png`
  - `debug_stage_hardprotect_diff_10.png`

## Verification Rules

- Stage changes must reuse SnapshotMask.
- Stage changes must not run FaceAnalyzer, StandardMaskWarper, NostrilDetector, FaceParsingDetector, or MaskQualityValidator again.
- Stage `5` should be the first practical studio/profile default.
- Stage `10` is a strong test value, not the default recommendation.
- HardProtect must remain original at every Stage.

## Visual Review Needed

- Confirm Stage `1` is subtle enough.
- Confirm Stage `5` is a useful photo-studio default.
- Confirm Stage `10` is visibly stronger without plastic skin.
- Confirm eyes, eyebrows, lips, nostrils, hair, beard, and glasses remain unchanged in Stage `10`.

## Build

Run after implementation:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

