# K Retouch Pro / PhotoRetouch - ORDER_23

# Stage 1 / 5 / 10 Result Compare Report

Status:
Implemented / Needs local image run

Prerequisite:
ORDER_22_TEST_IMAGE_SET_BUILD

Next order:
ORDER_24_SNAPSHOTMASK_CACHE_PERSISTENCE

Goal:
Generate Stage `1`, `5`, and `10` outputs and compare reports for portrait test cases.

## Implemented

- Added `StageCompareReportRunner`.
- Added `StageCompareRunSummary`.
- Added `StageCompareReport`.
- Added `StageCompareResult`.
- The runner reads `PortraitTestCase` metadata.
- Empty or missing local files are skipped safely.
- For assigned local originals, the runner:
  - creates SnapshotMask once
  - runs Stage `1`, `5`, and `10`
  - reuses the same SnapshotMask
  - saves original and Stage outputs
  - saves Stage `1/5/10` comparison sheet
  - saves HardProtect diff images
  - writes JSON and Markdown reports

## Output

Stage outputs:

```text
test_assets/portraits/stage_outputs/{TestId}/
```

Reports:

```text
test_assets/portraits/reports/{TestId}_stage_report.json
test_assets/portraits/reports/{TestId}_stage_report.md
test_assets/portraits/reports/stage_compare_summary.json
```

## Recorded Values

- RequestedStage
- AppliedStage
- SkinSmoothAmount
- BlemishReduceAmount
- WrinkleReduceAmount
- ToneEvenAmount
- TextureRestoreAmount
- PlasticSkinRiskScore
- HardProtectChangedPixelCount
- Eye / Eyebrow / Lip / InnerMouth / Nostril / Hair / Beard / Glasses changed flags
- Blemish candidate and applied counts
- Wrinkle applied count

## Current Boundary

The runner records measurable engine outputs. Human visual judgment is still required for:

- Stage 5 suitability as default studio/profile retouch.
- Stage 10 plastic skin risk.
- Nasolabial and age-feeling preservation.
- ToneEven over-flattening.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

