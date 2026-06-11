# AUTO MASK

Last updated: 2026-06-11

AUTO MASK is the current guide-generation workflow for skin-area preview.

It is not AI MASK.

## Purpose

AUTO MASK finds likely skin-colored areas and shows a guide preview that the user can inspect.

The current goal is not final retouch quality. The current goal is to make face-area detection and guide preview readable and reusable.

## Current Controls

Main control:

- `skin_mask_range`
- UI label: `피부 보정 범위`

Behavior:

- Opening the related panel may build the current guide mask.
- Moving the range slider and releasing it regenerates the guide preview.
- The same photo plus same range value should reuse the cached result.
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
-> open guide panel
-> capture skin_mask_range
-> check per-photo cache
-> if cache matches, show cached preview
-> if not, build guide mask from source image
-> show transparent source-color preview
-> optionally save debug files
```

## Guide Rules

The guide starts from selected skin color ranges.

It should help the user see:

- Approximate cheeks.
- Approximate chin skin.
- Approximate forehead skin.
- Approximate nose skin.
- Missed or over-selected areas that need later tuning.

It should avoid:

- White background.
- Clothing.
- Hair and beard.
- Glasses.
- Strong non-skin shadows.

## Debug Files

Current debug output uses:

- `debug_average_skin_mask_color.png`
- `debug_average_skin_mask_report.txt`

The color preview should show selected source pixels over transparent background.

## Known Limits

- This is not a trained AI face parser.
- Landmark-based guide regions are approximate.
- The preview is still a guide surface, not a finished correction surface.
