# K Retouch Pro / PhotoRetouch - ORDER_27

This file is UTF-8.

## Stage

Export / save options

## Status

InProgress / Core implemented

## Implemented

- Added `ExportService`.
- Added `ExportOptions`.
- Added `ExportRequest`, `ExportResult`, and `ExportReport`.
- Supports JPG and PNG export.
- JPG quality is clamped from `1` to `100`; current default is `100` to match the requested highest-quality save direction.
- Export never overwrites the original source path by default.
- Output names use a suffix and auto-rename:
  - default suffix: `_-_1`
  - duplicate example: `photo_-_1_001.jpg`
- Optional sidecar report writes:
  - source file name only
  - output file name only
  - export format
  - JPG quality
  - requested/applied stage
  - mask quality score when available
- The existing Save button now routes through `ExportService`.

## Not Yet Implemented

- Full export options UI.
- Save As dialog.
- Explicit PNG/JPG selector in the main UI.
- Exporting the final mask-retouch pipeline result from the retouch Stage toolbar; the current Save button still uses the current tone/preview adjustment render path.
- Debug export options UI.

## Guardrail

Export is a save operation.
It must not rerun FaceAnalyzer, SnapshotMaskBuilder, MaskQualityValidator, or RetouchStageProcessor just because the user saves.

