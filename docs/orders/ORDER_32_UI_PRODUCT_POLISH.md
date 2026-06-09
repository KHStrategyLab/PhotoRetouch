# K Retouch Pro / PhotoRetouch - ORDER_32

This file is UTF-8.

## Stage

UI product polish

## Status

InProgress / First polish pass implemented

## Goal

Make the app feel less like a development harness and more like a working portrait retouch product, without redesigning the whole UI.

## Implemented

- Top-left app label changed from `PhotoRetouch Studio` to `K Retouch Pro`.
- Removed the temporary `UI REVIEW` badge.
- Removed the dead top-bar reset button that had no command wired.
- Replaced static top-right sample text with:
  - selected photo dimensions
  - current zoom percent
- Developer status text is now hidden unless mask view or pipeline/skin retouch preview is active.
- Pipeline button wording changed from developer language to photographer-facing language:
  - `피부 보정`
  - `피부 보정 끄기`
- Related message box titles/text now use `피부 보정`.

## Still Pending

- Full export options UI.
- Preset UI.
- Batch UI.
- Better grouping for debug/developer controls.
- Product-ready iconography.
- More polished empty preview/list states.
- Full visual QA on right monitor.

## Guardrails

- No new filters.
- No layout redesign.
- No V2/Hold features.
- No SnapshotMask event-flow changes.
- Keep mouse-first workflow.

## Verification

- `dotnet build .\PhotoRetouch.sln -p:Platform=x64`
- Result: build passed with 0 warnings and 0 errors.

