# PhotoRetouch Portrait Test Cases

This file is UTF-8.

## Purpose

ORDER_22 defines the representative portrait test set for V1 single-face skin retouch validation.

The original image files are local-only and should be placed under:

```text
test_assets/portraits/original/
```

That folder is intentionally ignored by Git except for `.gitkeep`.

## Folders

```text
test_assets/portraits/original/
test_assets/portraits/stage_outputs/
test_assets/portraits/debug_outputs/
test_assets/portraits/reports/
test_assets/portraits/portrait_test_cases.json
```

## Minimum 10 Cases

- `HP_NOSTRIL_01`: nostril and lower-nose protection.
- `HP_EYEBROW_01`: eyebrow and eyelash protection.
- `HP_LIP_EDGE_01`: lip edge and inner-mouth protection.
- `HP_GLASSES_01`: glasses frame and lens boundary protection.
- `HP_BEARD_01`: beard and mustache protection.
- `WR_GLABELLA_01`: glabella wrinkle and eyebrow boundary.
- `WR_NASOLABIAL_01`: nasolabial fold and age-feeling preservation.
- `SK_BLEMISH_01`: acne / blemish candidate behavior.
- `TN_RED_DULL_01`: redness / dullness / mixed color light.
- `HP_HAIR_FACE_01`: hair touching face and hairline boundary.

## Rules

- Never overwrite originals.
- Do not commit sensitive real-person portraits.
- Save generated results under `stage_outputs`, `debug_outputs`, or `reports`.
- Stage compare should use Stage `1`, `5`, and `10`.
- HardProtect must be checked before judging filter quality.

