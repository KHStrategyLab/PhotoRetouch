# K Retouch Pro / PhotoRetouch - ORDER_22

# Full Portrait Test Image Set

Status:
Implemented / Needs local image assignment

Prerequisite:
ORDER_21_HARDPROTECT_TEST_SET

Next order:
ORDER_23_STAGE_RESULT_COMPARE_REPORT

Goal:
Define the representative portrait test image set for V1 engine validation without committing sensitive real-person originals.

## Implemented

- Added `test_assets/portraits/` layout.
- Added local-only folders:
  - `original`
  - `stage_outputs`
  - `debug_outputs`
  - `reports`
- Added `.gitignore` rules so real originals and generated outputs are not committed.
- Added `portrait_test_cases.json`.
- Added `docs/tests/PORTRAIT_TEST_CASES.md`.
- Added `PortraitTestCaseCatalog`.
- Added `PortraitTestCase`.
- Added `PortraitTestCaseValidationReport`.

## Minimum Test Cases

- `HP_NOSTRIL_01`
- `HP_EYEBROW_01`
- `HP_LIP_EDGE_01`
- `HP_GLASSES_01`
- `HP_BEARD_01`
- `WR_GLABELLA_01`
- `WR_NASOLABIAL_01`
- `SK_BLEMISH_01`
- `TN_RED_DULL_01`
- `HP_HAIR_FACE_01`

## Current Data State

The manifest defines required test slots and tags.

Most `FileName` fields are intentionally empty until local sample images are assigned. This avoids accidentally committing sensitive portraits or absolute local paths.

## Original Preservation Rule

Original files under `test_assets/portraits/original/` are local-only and ignored by Git.

Generated results must go to:

- `stage_outputs`
- `debug_outputs`
- `reports`

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

