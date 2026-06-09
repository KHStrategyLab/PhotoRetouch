# K Retouch Pro / PhotoRetouch - ORDER_31

This file is UTF-8.

## Stage

Cache cleanup / memory optimization

## Status

InProgress / First cleanup pass implemented

## Goal

Keep the V1 single-face retouch engine stable during long sessions, high-resolution images, and batch-like workflows.

This order is not a new filter order.
It is a cache and memory safety pass.

## Implemented

- Added bounded analysis caches for:
  - `BlemishReduceFilter`
  - `WrinkleSoftReduceFilter`
  - `TextureRestoreFilter`
- Each analysis cache now trims itself after new entries are added.
- Added cache count exposure through filter interfaces.
- Added `RetouchStageProcessor.AnalysisCacheStatus`.
- Added `RetouchStageProcessor.ClearAnalysisCaches()`.
- Added `RetouchAnalysisCacheStatus`.
- Added `PhotoItem.ClearTransientPreviewCache()`.
- Added `PhotoItem.ReleaseInactiveRetouchMemory()`.
- When photo selection changes, non-selected photos release:
  - effect preview cache
  - neutral preview image
  - in-memory SnapshotMask
  - adjusted preview image reference
- Last retouch output cache is cleared when it belongs to a photo that is no longer selected.

## Why This Matters

`SnapshotMaskDiskCache` keeps reload speed acceptable, so dropping inactive in-memory SnapshotMasks is safe.

Interactive editing should keep the selected photo responsive, while inactive photos should not hold heavy transient retouch data forever.

## Still Pending

- Disk cache size limit and cleanup policy.
- Manual cache clear command.
- App shutdown cleanup.
- Batch report-only memory retention review.
- Per-filter timing fields in the live `PipelineDebugReport`.
- UI display for cache/memory status.
- Long-session real photo stress test.

## Guardrails

- Do not clear original source images while the app is using the photo list.
- Do not clear user retouch settings.
- Do not clear manual mask or face adjustment data.
- Do not regenerate SnapshotMask on Stage or Slider changes.
- HardProtect remains unchanged.

## Verification

- `dotnet build .\PhotoRetouch.sln -p:Platform=x64`
- Result: build passed with 0 warnings and 0 errors.

