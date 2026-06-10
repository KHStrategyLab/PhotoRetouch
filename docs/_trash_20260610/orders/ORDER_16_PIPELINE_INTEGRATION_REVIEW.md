# K Retouch Pro / PhotoRetouch - ORDER_16

# Pipeline Integration Review

Status:
Implemented / Needs visual review

Prerequisite:
ORDER_15_HARDPROTECT_FINAL_RESTORE

Next order:
ORDER_17 is still held until the user releases it.

Goal:
Verify that the portrait retouch pipeline is separated into analysis stages and filter stages, and that stage or slider changes reuse the existing `FaceSnapshotMaskSet`.

Final pipeline order:

1. Image load prepares or reuses `FaceSnapshotMaskSet`.
2. `StagePresetMapper`
3. `RetouchStageProcessor`
4. `SkinSmoothFilter`
5. `BlemishReduceFilter`
6. `WrinkleSoftReduceFilter`
7. `ToneEvenFilter`
8. `TextureRestoreFilter`
9. `HardProtectFinalRestoreFilter`
10. Preview or final image output.

Analysis stages:

- `FaceAnalyzer`
- `StandardMaskWarper`
- `NostrilDetector`
- `FaceParsingDetector`
- `MaskQualityValidator`

These stages must not run again only because Stage, slider values, texture restore, before/after, or preset-like retouch values changed.

Filter stages:

- Skin smoothing
- Blemish reduction
- Wrinkle softening
- Tone even
- Texture restore
- Hard protect final restore

Current implementation notes:

- `RetouchStageProcessor` now records a `PipelineDebugReport`.
- The report marks analysis as not executed inside the retouch processor because the processor only receives an already-built snapshot.
- `SnapshotMaskReused` and `QualityReportReused` are true for the filter pass.
- `ToneEvenFilter` is placed after wrinkle reduction and before final texture restoration.
- HardProtect final restore remains the last image-changing operation.

Debug outputs added:

- `debug_pipeline_original.png`
- `debug_pipeline_snapshot_mask_overlay.png`
- `debug_pipeline_hard_protect.png`
- `debug_pipeline_soft_protect.png`
- `debug_pipeline_retouch_allow.png`
- `debug_pipeline_after_skin_smooth.png`
- `debug_pipeline_after_blemish.png`
- `debug_pipeline_after_wrinkle.png`
- `debug_pipeline_after_tone_even.png`
- `debug_pipeline_after_texture_restore.png`
- `debug_pipeline_final_after_hardprotect_restore.png`
- `debug_pipeline_stage_1_final.png`
- `debug_pipeline_stage_5_final.png`
- `debug_pipeline_stage_10_final.png`
- `debug_pipeline_report.txt`

Validation points:

- Stage changes should reuse the snapshot mask.
- Stage changes should rerun only filter work.
- HardProtect should be clean after final restore.
- RetouchAllow, SoftProtect, and HardProtect debug masks should match the expected facial areas.
- Stage 1, 5, and 10 final images should show different strength without changing the mask cache key.

Do not include in this order:

- New filter implementation.
- UI redesign.
- Batch processing.
- Preset implementation.
- Export option implementation.
- FaceParsing model replacement.

Core rule:
ORDER_16 is an integration review stage. It makes the existing pipeline observable and keeps analysis reuse separate from retouch filter execution.
