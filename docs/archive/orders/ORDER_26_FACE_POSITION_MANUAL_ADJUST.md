# K Retouch Pro / PhotoRetouch - ORDER_26

This file is UTF-8.

## Stage

Face position / keypoint manual adjustment

## Status

InProgress / Core implemented

## Implemented

- Added `FaceManualAdjustOverride`.
- Added normalized keypoint override slots for:
  - left eye
  - right eye
  - nose tip
  - mouth center
  - chin point
- Added `FaceManualAdjustStore`.
- Manual face adjustment files are stored under local AppData:
  - `PhotoRetouch/cache/manual_adjustments/`
- `PhotoItem` now loads saved manual face adjustment data on image load.
- Existing editable face work area is saved as `FaceBoxOverride` after the user drags the overlay.
- Resetting the Face Shape section clears the manual face adjustment and returns to the automatic/default face area.
- `SnapshotMaskBuilder` includes manual adjustment information in the crop/cache version.
- Stage and slider changes still do not regenerate SnapshotMask.
- Face position adjustment remains a SnapshotMask regeneration condition.

## Not Yet Implemented

- Individual draggable eye / nose / mouth / chin handles in the preview UI.
- Apply / Cancel / ResetToAuto face-adjust panel.
- Debug overlay entries for AutoFaceBox / ManualFaceBox / FinalFaceBox.
- Prompt policy for what to do with existing `ManualMaskOverride` after face position changes.

## Notes

This order is not a shape-warp or beauty geometry module.
It only stores face analysis correction data as an override layer so the mask position can be rebuilt safely.

