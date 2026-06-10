# Preview And Workflow Rules

Last updated: 2026-06-10

This document describes how the app should feel while editing.

## Core Principle

The user must stay in control.

The app should not start heavy work just because the user selected a photo or opened a screen.

## Photo Selection

Selecting a photo is a viewing action.

Expected behavior:

- Show the selected photo quickly.
- Reset preview zoom/pan to fit for the selected photo.
- Do not run heavy SkinRetouch output immediately.
- Do not make the preview blurry.
- Do not apply retouch to multiple selected photos.

## Tool Panel Opening

Opening a tool panel is a preparation action.

Allowed:

- Prepare required lightweight data.
- Show AUTO MASK preview if the panel is specifically a mask-related panel.
- Reuse cached results when available.

Avoid:

- Rebuilding masks unnecessarily.
- Running full retouch pipelines without a changed value.
- Blocking mouse wheel preview navigation with tool overlay layers.

## Slider Behavior

Sliders are mouse-first controls.

Rules:

- Mouse wheel must not change retouch slider values.
- Clicking the slider track must not page the value toward 0 or 100.
- Value changes should come from dragging the thumb.
- Heavy renders can be throttled or run on release.
- If a slider goes back to zero, pending work for that tool should stop when possible.
- Reset should cancel pending tool work and restore the photo state.

Current slider track protection is implemented in `MainWindow.xaml` by disabling track repeat button hit tests.

## Per-Photo State

The user can work on one photo, switch to another photo, and return.

Rules:

- Per-photo values should follow the photo until app refresh or source reload.
- Selecting another photo must not permanently overwrite the first photo's values.
- A new photo with no saved session state starts from default values.
- Refresh or app restart can reset session-only state.

Current session state is stored on `PhotoItem.RetouchState`.

AUTO MASK preview cache is stored on `PhotoItem.AverageFaceColorMaskPreviewCache`.

## Preview Tiers

Current enum:

- `LowPreview`
- `FastPreview`
- `QualityPreview`
- `ExportRender`

Current practical behavior:

- Interactive preview should use a display-sized source where possible.
- Export render should use original source resolution.
- The preview image is a result, not the source of truth.

Do not:

- Feed PreviewImage back into FaceAnalyzer.
- Build SnapshotMask from PreviewImage.
- Stack filters repeatedly on the displayed preview.
- Save reduced preview as final export.

## Before And After

Before/after is a view switch.

Rules:

- Do not reanalyze.
- Do not rebuild masks.
- Do not rerun filters.
- Show original versus current preview result.

## Refresh

Refresh is an explicit reset boundary.

Rules:

- It may clear per-photo session values.
- It may clear generated debug photo folders under `_mask_debug`.
- It should close open retouch sections.
- It should not remove the root `_mask_debug` folder unnecessarily.

## Performance Feel

Practical performance is judged by working feel.

Warning signs:

- Fan spins during simple tab opening.
- Slider lags while no visible result appears.
- Preview becomes blurry.
- Mouse wheel or preview drag stops working because an overlay is on top.
- Same photo and same value recalculates instead of reusing cache.

Fix performance by:

- Reusing per-photo caches.
- Running heavy work only on release.
- Using visible-preview-sized sources.
- Avoiding repeated full-original processing during dragging.

