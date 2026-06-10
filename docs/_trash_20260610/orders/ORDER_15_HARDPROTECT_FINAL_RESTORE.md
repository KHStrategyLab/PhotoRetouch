# K Retouch Pro / PhotoRetouch - ORDER_15

This file is UTF-8.

## Stage

HardProtect Final Restore final verification

## Status

Implemented / Needs visual review

## Prerequisite

ORDER_14_TEXTURE_RESTORE

## Next Order

ORDER_16_PIPELINE_INTEGRATION_REVIEW

## Goal

Verify that every final output restores `HardProtectMask` pixels from the original image at the very end of the retouch pipeline.

HardProtect has priority over every filter.

## Core Rule

```text
FinalProtectedImage =
    CurrentRetouchedImage * (1 - HardProtectMask)
  + OriginalImage * HardProtectMask
```

## Protected Areas

- Eyes
- Eyebrows
- Lips
- Inner mouth
- Teeth
- Nostrils
- Hair
- Beard
- Mustache
- Glasses

## Current Implementation

- `HardProtectFinalRestoreFilter`
- `HardProtectFinalRestoreInput`
- `HardProtectFinalRestoreResult`
- `HardProtectRestoreReport`

The filter runs after:

1. SkinSmooth
2. BlemishReduce
3. WrinkleSoftReduce
4. TextureRestore

The final `RetouchStageProcessorOutput.FinalImage` is the HardProtect-restored image.

## Debug Outputs

- `debug_hardprotect_mask.png`
- `debug_before_hardprotect_restore.png`
- `debug_after_hardprotect_restore.png`
- `debug_hardprotect_diff_before.png`
- `debug_hardprotect_diff_after.png`
- `debug_eye_restore_check.png`
- `debug_lip_restore_check.png`
- `debug_nostril_restore_check.png`
- `debug_hair_restore_check.png`
- `debug_final_stage_1_hardprotect_check.png`
- `debug_final_stage_5_hardprotect_check.png`
- `debug_final_stage_10_hardprotect_check.png`

## Completion Criteria

- HardProtect final restore exists as a distinct pipeline step.
- HardProtect pixels are restored from original image.
- HardProtect has priority over RetouchAllow and SoftProtect.
- Stage 1, Stage 5, and Stage 10 debug images verify protection.
- AfterRestoreDiff is expected to be near zero.
- Build passes.

## Guardrail

Do not proceed to ORDER_16 if HardProtect differs in final output.
