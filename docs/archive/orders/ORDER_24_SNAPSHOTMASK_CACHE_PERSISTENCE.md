# K Retouch Pro / PhotoRetouch - ORDER_24

# SnapshotMask Cache Persistence

Status:
Implemented / Needs real reload test

Prerequisite:
ORDER_23_STAGE_RESULT_COMPARE_REPORT

Next order:
ORDER_25_MANUAL_MASK_BRUSH

Goal:
Persist `FaceSnapshotMaskSet` to disk so the same image can reuse its SnapshotMask after reload without repeating face analysis.

## Implemented

- Added `SnapshotMaskDiskCache`.
- Added disk cache load/save reports:
  - `SnapshotMaskCacheLoadResult`
  - `SnapshotMaskCacheSaveResult`
- Added JSON metadata:
  - `SnapshotMaskCacheDocument`
  - `SnapshotMaskCacheKeyDto`
  - `FaceAnalysisDto`
  - `MaskQualityReportDto`
- Masks are saved as PNG grayscale files, not huge JSON arrays.
- `SnapshotMaskBuilder.GetOrCreate(...)` now checks:
  - in-memory snapshot
  - disk cache
  - rebuild
- `SnapshotMaskBuilder.Rebuild(...)` refreshes the disk cache.
- Stage and slider values remain excluded from cache identity.
- Top status now shows disk cache hit count.

## Cache Location

Default local user cache:

```text
%LOCALAPPDATA%/PhotoRetouch/cache/snapshot_masks/
```

The repository also ignores:

```text
cache/
*.maskcache
*.analysiscache
```

## Stored Snapshot Data

- Face analysis result.
- FaceBox.
- FaceLandmarks.
- FaceAngle.
- MaskQualityReport.
- SkinMask.
- EyeMask.
- EyebrowMask.
- LipMask.
- InnerMouthMask.
- NoseMask.
- NoseSkinMask.
- NostrilMask.
- HairMask.
- BeardMask.
- MustacheMask.
- GlassesMask.
- HardProtectMask.
- SoftProtectMask.
- RetouchAllowMask.
- FinalOverlayMask.

## Cache Invalidation

Disk cache is ignored when:

- cache version differs
- image ID differs
- source last-write time or file length differs
- image size differs
- crop version differs
- mask version differs
- cache files are missing or damaged

Stage, slider, Before/After, Debug Mask view, and Save/Export are not invalidation conditions.

## Deferred

Separate AnalysisCache persistence for Blemish, Wrinkle, ToneEven, and TextureRestore is not implemented yet. The first V1 cache pass focuses on the SnapshotMask itself.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

