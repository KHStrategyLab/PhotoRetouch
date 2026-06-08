# K Retouch Pro / PhotoRetouch - ORDER_21

# HardProtect Preservation Test Set

Status:
Implemented / Needs real image run

Prerequisite:
ORDER_20_BEFORE_AFTER_DEBUG_PANEL

Next order:
ORDER_22_TEST_IMAGE_SET_BUILD

Goal:
Verify that HardProtect regions stay original through Stage `1`, `5`, and `10`.

## Implemented

- Added `HardProtectTestSetRunner`.
- The runner can process a directory of JPG, PNG, TIFF images.
- For every image it builds/reuses `FaceSnapshotMaskSet`.
- For every image it runs Stage `1`, `5`, and `10`.
- For every image it exports common masks:
  - `hardprotect_mask.png`
  - `eye_mask.png`
  - `eyebrow_mask.png`
  - `lip_mask.png`
  - `inner_mouth_mask.png`
  - `nostril_mask.png`
  - `hair_mask.png`
  - `beard_mask.png`
  - `glasses_mask.png`
  - `hardprotect_overlay.png`
- For every Stage it exports:
  - `final_stage_N.png`
  - `hardprotect_restored_stage_N.png`
  - `hardprotect_diff_before_stage_N.png`
  - `hardprotect_diff_after_stage_N.png`
  - `hardprotect_report_stage_N.json`
  - `retouch_report_stage_N.json`
- Added `HardProtectTestReport`.
- Added `HardProtectTestSetSummary`.
- Summary output:
  - `hardprotect_test_summary.json`

## Failure Classification

Initial failure classes:

- `MaskMissing`
- `MaskTooSmallOrMisaligned`
- `FinalRestoreFailed`
- `NeedsReview`

More specific labels such as `NostrilDetectorError`, `ParsingError`, `StageLeak`, and `FilterOrderWrong` can be added after real-image failures are collected.

## Test Image Types Required

- Large visible nostrils.
- Strong under-nose shadow.
- Thick eyebrows and clear eyelashes.
- Strong lip edge.
- Open mouth and visible teeth.
- Hair touching face.
- Beard or mustache.
- Glasses.
- Under-eye, glabella, forehead, nasolabial, and neck wrinkles.
- Blemishes, redness, and strong shadows.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

