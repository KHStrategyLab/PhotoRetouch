# K Retouch Pro / PhotoRetouch - ORDER_29

This file is UTF-8.

## Stage

Batch processing / multiple photo retouch

## Status

InProgress / Core implemented

## Implemented

- Added `BatchProcessingService`.
- Added `BatchOptions`.
- Added `BatchRequest`, `BatchReport`, and `BatchItemReport`.
- Initial batch mode is sequential:
  - `MaxParallelCount = 1`
- Batch can use:
  - a loaded `RetouchPreset`
  - the current `RetouchToolset`
- Each image gets its own `SnapshotMask`.
- Each image applies its own `MaskQualityGate`.
- Each image gets its own `AppliedStage`.
- Results are saved through `ExportService`.
- Batch report is written as JSON.
- Failed images produce an item report and do not necessarily stop the full batch.
- Per-photo transient preview cache is cleared after each item.

## Not Yet Implemented

- Main UI for batch file selection.
- Progress bar / cancel button.
- Batch report viewer.
- Debug image saving option per batch item.
- Parallel processing.

## Guardrail

Preset values may be shared across batch items.
SnapshotMask must never be shared across different photos.

