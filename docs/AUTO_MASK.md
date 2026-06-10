# AUTO MASK

Last updated: 2026-06-10

AUTO MASK is the current mask-first workflow for skin retouch preparation.

It is not AI MASK yet.

## Purpose

AUTO MASK finds skin-colored areas and prepares a soft mask that later skin tools can use.

The goal is not to cover the face with a large blob. The goal is to keep only likely skin-color pixels, repair small holes, then exclude protected facial features and non-skin areas.

## Current UI Name

The UI may still show Korean text such as `평균색 마스크`.

Conceptually, the feature name is:

```text
AUTO MASK
```

Avoid calling it AI until a real AI model makes pixel-level face-part decisions.

## Current Controls

Main control:

- `skin_mask_range`
- UI label: `피부 보정 범위`
- Default value: `75`

Behavior:

- Opening the relevant skin/mask tool can generate the default mask at value 75.
- Moving the range slider and releasing it regenerates the mask.
- The generated mask file is overwritten rather than creating many versions.
- Same photo plus same range value should reuse the cached mask.
- Changing the range value invalidates the cached result for that value.

## Current Generation Flow

Current source:

- `AverageFaceColorMaskBuilder.Build(...)`
- `MainWindow.RefreshAutoAiMaskPreviewAsync(...)`
- `PhotoItem.TryGetAverageFaceColorMaskPreview(...)`
- `PhotoItem.CacheAverageFaceColorMaskPreview(...)`
- `DebugMaskExporter.CreateSourceColorMaskPreview(...)`

Flow:

```text
Selected photo
-> open skin/mask panel
-> capture skin_mask_range
-> check per-photo AUTO MASK cache
-> if cache matches, show cached preview
-> if not, build mask from original image
-> fill small enclosed holes
-> subtract feature block mask
-> show transparent source-color mask preview
-> optionally save debug mask files
```

## Mask Rules

The mask starts from selected skin color ranges.

It should include:

- Cheeks.
- Chin skin.
- Forehead skin only if color is within range.
- Nose skin only weakly and carefully.
- Skin-colored face areas that are missed by tiny holes.

It should exclude:

- White background.
- Clothes.
- Hair.
- Beard hair.
- Glasses frame.
- Eyes.
- Eyebrows.
- Eyelashes where available.
- Lips.
- Inner mouth.
- Teeth.
- Nostrils.
- Nose ridge / strong nose structure.
- Strong nose side and under-nose shadows when they carry facial structure.

## Hole Filling

Small holes inside the skin-color mask can be filled.

This is not a blob mask.

The fill rule is:

- Fill only small enclosed gaps.
- Require skin support from both horizontal and vertical directions.
- Do not expand outside the selected skin-color region into background.
- Do not fill through feature block regions.
- Reapply feature exclusion after filling.

Protected holes must remain holes.

## Feature Block Mask

Feature block regions act like a Photoshop path selection with feather.

Current block sources:

- Existing hard protect masks.
- Eye mask.
- Eyebrow mask.
- Lip mask.
- Inner mouth mask.
- Teeth mask.
- Nostril mask.
- Nose mask and nose shadow mask.
- Glasses mask.
- Estimated soft paths from landmarks for eyes, brows, nose bridge/tip, and mouth.

## Shadow Policy

Not every shadow is a defect.

Initial policy:

- Nose side shadow: preserve strongly.
- Under-nose shadow: preserve strongly.
- Nose ridge/edge: preserve strongly.
- Under-eye shadow: allow weak smoothing only.
- Mouth-corner shadow: allow weak to medium smoothing.
- Patchy skin tone: allow smoothing inside AUTO MASK.

Shadow smoothing must eventually be controlled per region. Do not use one global amount for every facial shadow.

## Debug Files

Current AUTO MASK debug output uses:

- `debug_average_skin_mask_color.png`
- `debug_average_skin_mask_report.txt`

The color preview should show selected source pixels over transparent background.

## Known Limits

- This is not a trained AI face parser.
- Landmark-based feature paths are approximate.
- Glasses and hair depend on available masks and current detection quality.
- Actual retouch filters are not the current quality target. Mask reliability comes first.

