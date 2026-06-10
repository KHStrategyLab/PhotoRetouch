# K Retouch Pro / PhotoRetouch - ORDER_25

# Manual Mask Brush

Status:
InProgress / Core engine implemented

Prerequisite:
ORDER_24_SNAPSHOTMASK_CACHE_PERSISTENCE

Next order:
ORDER_26_FACE_POSITION_MANUAL_ADJUST

Goal:
Allow user corrections to be stored as a separate `ManualMaskOverride` layer over the automatic SnapshotMask.

## Implemented

- Added `ManualMaskOverride`.
- Added `ManualMaskBrushOptions`.
- Added `ManualMaskBrushMode`.
- Added `ManualMaskBrushEngine`.
- Added `ManualMaskOverrideApplier`.
- Added `PhotoItem.ManualMaskOverride`.
- Manual override masks are separate from automatic SnapshotMask.
- Pipeline preview applies `ManualMaskOverride` over SnapshotMask before retouch processing.
- Debug mask preview applies `ManualMaskOverride` over SnapshotMask before overlay rendering.
- ReAnalyze rebuilds automatic SnapshotMask, then applies the current manual override layer.

## Brush Modes Prepared

- Protect
- Retouch
- SoftProtect
- Erase

## Final Mask Priority

HardProtect remains first priority.

```text
FinalHardProtect = AutoHardProtect + ManualHardProtectAdd - ManualHardProtectRemove
FinalSoftProtect = AutoSoftProtect + ManualSoftProtectAdd - ManualSoftProtectRemove - FinalHardProtect
FinalRetouchAllow = AutoRetouchAllow + ManualRetouchAllowAdd - ManualRetouchAllowRemove - FinalHardProtect
```

## Still Needed

- In-app brush cursor UI.
- Mouse stroke capture on preview.
- Brush size / feather controls.
- Reset Manual button.
- Manual override disk persistence.
- Debug selector entries for manual add/remove/final masks.
- Last stroke undo.

## Build

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:
Passed with 0 warnings and 0 errors.

