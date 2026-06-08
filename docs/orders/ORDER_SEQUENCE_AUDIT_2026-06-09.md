# PhotoRetouch Order Sequence Audit - 2026-06-09

This file is UTF-8.

Purpose:
Check whether any engine orders arrived out of sequence or were missed before continuing implementation.

Sources checked:

- `C:\Users\beint\Downloads\K_RETOUCH_ENGINE_ORDER_BLUEPRINT.md`
- Recent pasted order attachments in `C:\Users\beint\.codex\attachments`
- Current project docs under `docs/orders/`
- Current code state in `Tools/Masking`

## Result

No numbered order from `ORDER_00` through `ORDER_30` is missing from the conversation/context after the latest pasted orders are included.
The post-`ORDER_30` V1 stabilization keys for `ORDER_31` through `ORDER_35` are also recorded as the productization boundary.

Important caveat:

- `ORDER_30_HIGH_RES_PERFORMANCE_OPTIMIZATION` has now been received and stored as a queued/planned order.
- `ORDER_13_TONE_EVEN` arrived later than several later-numbered orders. It should be treated as a real queued order that belongs before `ORDER_14_TEXTURE_RESTORE`.
- `ORDER_17` through `ORDER_29` arrived as planned/queued future orders. They should not be implemented before their prerequisites are complete.

## Sequence

| Order | Name | Current Handling |
| --- | --- | --- |
| ORDER_00 | Backup lock / order backup | Done as documentation practice; keep updating. |
| ORDER_01 | Snapshot Mask | Implemented first-pass structure. |
| ORDER_02 | Dummy Snapshot Mask | Implemented/verified scaffold. |
| ORDER_03 | Standard Mask Resource + Warp | Implemented first-pass structure. |
| ORDER_04 | FaceBox / keypoint automation | Implemented with analyzer structure and fallback. |
| ORDER_05 | FaceDetection / FaceLandmark | Implemented first-pass OpenCV YuNet analyzer. |
| ORDER_06 | Nostril protect mask | Implemented first-pass `NostrilDetector`. |
| ORDER_07 | FaceParsing | Scaffolded with `TemporaryFaceParsingDetector`; real model still pending. |
| ORDER_08 | MaskQualityValidator + Stage Gate | Implemented first-pass quality gate. |
| ORDER_09 | Retouch Pipeline 1 + StagePresetMapper | Implemented first-pass pipeline. |
| ORDER_10 | SkinSmooth quality pass | Implemented first-pass mask-aware smoothing. |
| ORDER_11 | BlemishReduce | Implemented first-pass local blemish candidate filter. |
| ORDER_12 | WrinkleSoftReduce | Implemented first-pass wrinkle candidate filter and part masks. |
| ORDER_13 | ToneEven / skin tone evening | Queued/NeedsReview. Current code has a simple tone-even stage hook, but not the full dedicated `ToneEvenFilter` order. Slider/toolset connection still needs ORDER_17/18 alignment. |
| ORDER_14 | TextureRestore | Implemented first-pass `TextureRestoreFilter`; needs visual review. |
| ORDER_15 | HardProtect final verify | Implemented first-pass `HardProtectFinalRestoreFilter`; needs visual review. |
| ORDER_16 | Pipeline Integration Review | Implemented report/debug additions; build verification pending in this backup run. |
| ORDER_17 | Toolset / Slider / StagePreset alignment | Implemented / Needs UI binding review. |
| ORDER_18 | ViewModel / UI Binding review | Implemented / Needs visual review. |
| ORDER_19 | Stage 1-10 preset real tuning | Implemented / Needs visual review. |
| ORDER_20 | Before / After + Debug Mask Panel | Implemented / Needs visual review. |
| ORDER_21 | HardProtect test set | Queued/Planned. |
| ORDER_22 | Full test image set | Queued/Planned. |
| ORDER_23 | Stage 1/5/10 compare report | Queued/Planned. |
| ORDER_24 | SnapshotMask cache persistence | Queued/Planned. |
| ORDER_25 | Manual Mask Brush | Queued/Planned. |
| ORDER_26 | Face position / keypoint manual adjust | Queued/Planned. |
| ORDER_27 | Export / save options | Queued/Planned. |
| ORDER_28 | Preset save/load | Queued/Planned. Existing doc created. |
| ORDER_29 | Batch processing | Queued/Planned. |
| ORDER_30 | High-res performance optimization | Queued/Planned. Full order stored. |
| ORDER_31 | Cache / memory cleanup | Queued/Planned as V1 stabilization key. |
| ORDER_32 | UI product polish | Queued/Planned as V1 stabilization key. |
| ORDER_33 | User settings persistence | Queued/Planned as V1 stabilization key. |
| ORDER_34 | Installer / package | Queued/Planned as V1 stabilization key. |
| ORDER_35 | V1 final review | Queued/Planned as V1 stabilization key. |

## Out-Of-Order Arrivals

- `ORDER_17`, `ORDER_18`, `ORDER_19`, `ORDER_20`, and later orders were delivered before all earlier work was fully verified. They are preserved as queued orders.
- `ORDER_13_TONE_EVEN` was delivered after later orders. It should not be considered skipped; it is the missing conceptual step before `ORDER_14`.
- `ORDER_14` was implemented before the full `ORDER_13_TONE_EVEN` implementation. The current pipeline position is correct, but the dedicated tone-even filter/toolset/sliders still need follow-up.

## Practical Next Step

1. Finish build verification for the current `ORDER_16` integration changes.
2. Backup/commit current state.
3. Continue with `ORDER_17_PROJECT_STRUCTURE_TOOLSET_SLIDER`.
4. During `ORDER_17/18`, explicitly connect the skin tone slider path so `ORDER_13_TONE_EVEN` is not forgotten.

## Current Risk Notes

- ToneEven is the main order at risk of being under-implemented because it currently exists as a simple processor stage, not as the full planned filter with candidate masks and reports.
- `ORDER_17` should not introduce large UI redesign. It should align existing structures first.
