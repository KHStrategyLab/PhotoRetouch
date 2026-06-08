# PhotoRetouch Feature Status And Roadmap

## Purpose

This document tracks what exists now, what is partly implemented, and what still needs to be designed. It is written for a photographer-led workflow, not as a generic photo editor checklist.

## Global App

### Implemented

- WPF desktop app targeting 64-bit.
- Starts on the rightmost monitor and maximizes.
- Neutral dark UI palette.
- Work folder setting.
- Work folder preload on startup.
- Manual work folder refresh button.
- Duplicate file prevention by path.
- Settings tabs for color management, preview, performance, work folder, and shortcuts.

### Needs Work

- App-level project/session persistence.
- Save/export workflow.
- File missing status when external delete or rename happens.
- Better separation between UI layer and preview engine.
- Undo/redo model.

## Photo List And Viewer

### Implemented

- Load photos by file dialog.
- Drag and drop to list or preview.
- Work folder preload.
- Manual refresh for new files.
- Mouse helper keys for add/range/group interactions, configurable in settings and allowed to include Ctrl, Shift, Alt, and Space.
- Up to 8 selected images in split preview.
- Split layouts: 2, 4, 5 as 3+2, up to 4 per row.
- Selected list item uses a thin white border.
- F2 rename, Enter confirm.
- Previous/next photo navigation from bottom buttons.
- Arrow-key photo list navigation only while the photo list item context is active after clicking a list item; it stops applying after clicking preview, tools, or other UI.
- Preview zoom and pan.
- Per-cell pan and zoom in split mode.
- Configurable mouse helper gesture for group split interaction.
- Fit-in reset on list selection change.
- Maximum zoom calculated up to source 1:1.

### Needs Work

- Better visual status for missing files.
- Optional manual cleanup of deleted files.
- More explicit 1:1/Fit commands if implemented later.
- Split preview small labels or indexes if useful.

## Settings

### Implemented

- Color management modes:
  - Automatic
  - Manual profile
  - Disabled
- Preview max long side:
  - Original
  - Custom, clamped 800px to 4000px
- Performance engine placeholder:
  - Stable mode - CPU engine
  - Accelerated mode - GPU engine, implementation pending
- Work folder selection.
- Shortcut editing for current basic keyboard shortcuts and mouse helper gestures.
- Settings state is separated into `Settings/` classes instead of living in `SettingsWindow.xaml.cs`.

### Needs Work

- Rename performance modes after native engine is added.
- Add curve point delete shortcut to shortcut settings.
- Better settings validation feedback.
- Option import/export if settings grow.

## Photo Adjustment

### Implemented

Menu name: Tone correction.

Controls currently connected to preview engine:

- Curves
- Exposure (`-15` to `+15`, one-decimal display for smoother adjustment, with near-white highlight protection when lowering exposure)
- Contrast (`-25` to `+25`)
- Saturation
- White balance
- Blur / sharpen

Engine behavior:

- Runs on C# CPU engine.
- Uses `PreviewAdjustment` and `IPreviewEngine`.
- Current pixel loop lives in `CSharpPreviewEngine`.
- `PreviewEngineFactory` chooses the active preview engine, with GPU currently falling back to C# CPU.
- `PreviewSourceFactory` creates screen-sized effect preview sources.
- Runs on screen-sized effect preview source for interactive preview.
- Single-photo preview applies current adjustment values.
- Multi-select split preview is a lightweight choosing/comparison mode and does not apply retouch effects to all selected photos.
- Tone correction sliders render with throttled live preview while dragging.
- Curve amount renders with throttled live preview.
- Preview generation blocks conflicting inputs.

### Needs Work

- Use LUTs for more tone operations.
- Add Native CPU engine later.
- Add GPU engine later as optional mode.

## Curves

### Implemented

- Channels: All, R, G, B.
- Per-channel point collection.
- Up to 7 points per channel.
- Default corner anchors at 0,0 and 255,255.
- Default corner anchors are editable and deletable.
- Anchors can be dragged past each other to create cross-curve shapes.
- Click curve canvas to add anchor point.
- Drag points.
- Delete selected point with Delete or Backspace.
- Drag any anchor outside the curve box to delete it.
- Selected point input/output numeric edit.
- Direction-key point nudging, with Shift for larger moves.
- Per-channel reset.
- Per-channel histogram display.
- Smooth cubic Hermite LUT interpolation.
- Curve amount throttled live preview.
- LUT is applied to preview.

### Needs Work

- Clean up curve UI text and labels.
- Improve point hit testing and line-click behavior.
- Add reset all channels if useful.
- Add shortcut settings entry for curve point deletion.
- Consider native engine for curve processing.

## Customization Direction

### Policy

- Keep tool controls independently adjustable during development.
- Do not merge controls into one global value just to simplify code.
- Add setting copy, selected-photo apply, and batch apply later as explicit workflow features.
- Keep multi-select as a comparison/choosing mode until those workflows are designed.
- Solve performance with preview sizing, throttling, caching, engine separation, native CPU, and optional GPU rather than by removing user control.

## Skin

### Current Controls

- Blemish removal - first preview pass connected for small dark spot softening
- Skin texture cleanup - first preview pass connected with edge-protected smoothing
- Pore cleanup - first preview pass connected for small texture cleanup
- Skin tone correction - first preview pass connected for mild local tone evening

### Implemented

- `skin_smooth` is captured in `PreviewAdjustment`.
- The C# preview engine applies a mild screen-sized texture smoothing pass.
- Strong detail/edge differences are protected so facial edges, eyes, hair, and clothing do not blur as aggressively.
- `pore_clean` is captured in `PreviewAdjustment`.
- The C# preview engine applies a narrower small-texture cleanup pass for pore-like detail.
- `tone_even` is captured in `PreviewAdjustment`.
- The C# preview engine applies a mild local tone evening pass with edge protection.
- `blemish_remove` is captured in `PreviewAdjustment`.
- The C# preview engine softens small dark spots toward the local surrounding tone.

### Needs Work

- Actual skin mask or face region detection.
- Brush/manual target mode for precise blemish removal.
- Texture-preserving smoothing.
- Skin-region-only local tone evening.
- Strength behavior tuned for ID photos.

## Face Shape

### Current Controls

- Oval face correction - first preview warp pass connected to the editable face work area
- Left-right balance - first preview warp pass connected to the editable face work area
- Cheekbone soften - first preview warp pass connected to the editable face work area
- Jawline clarity - first localized edge-clarity pass connected to the editable face work area
- Chin length - first vertical chin-tip warp pass connected to the editable face work area
- Chin width - first horizontal chin-tip warp pass connected to the editable face work area
- Jaw balance
- Double chin soften
- Neck and jaw boundary

### Implemented

- Each photo has a default normalized face work area model.
- Opening the Face Shape section shows an editable face work area guide over the single-photo preview.
- The face work area can be moved and resized by dragging its body or corner handles.
- Face work area edits participate in undo/redo.
- Resetting the Face Shape section resets the face work area.
- The guide is hidden during original comparison and split preview.
- `oval_face` applies a limited ellipse-based horizontal warp inside the face work area.
- `face_balance` uses a signed `-100` to `+100` range and applies a subtle horizontal balancing warp inside the face work area.
- `cheekbone_soften` applies a limited upper/mid side compression warp inside the face work area.
- `jawline_define` applies a localized lower-side edge clarity pass inside the face work area.
- `chin_length` uses a signed `-100` to `+100` range and applies a limited lower-center vertical warp inside the face work area.
- `chin_width` uses a signed `-100` to `+100` range and applies a limited lower-center horizontal warp inside the face work area.

### Needs Work

- Persist editable face work area across app restarts.
- Face landmark detection.
- Dedicated warp engine with stronger quality controls.
- Bounds to prevent unrealistic edits.
- Separate chin/jawline interaction review after real warp exists.

## Background

### Current Controls

- Background select
- Saved background library placeholder
- Background image opacity
- Solid color select
- Solid color amount
- Edge blending

### Needs Work

- File import for background images.
- Persist imported background thumbnails.
- Horizontal no-scrollbar gallery.
- Subject segmentation.
- Solid color background renderer.
- Edge blend/refine.

## Eyes

### Current Controls

- Left-right balance
- Pupil size
- Eye height
- Eye width
- Eye brightness
- Red eye or bloodshot eye removal

### Needs Work

- Eye landmark detection.
- Local brightness masks.
- White area detection for redness reduction.
- Natural limits for geometric edits.

## Nose

### Current Controls

- Nostril size match
- Nose wing size
- Nose width
- Nose height
- Nose tip size

### Needs Work

- Nose landmarks.
- Local warp controls.
- Symmetry-aware adjustments.

## Mouth

### Current Controls

- Mouth width
- Upper lip
- Lower lip

### Needs Work

- Mouth/lip landmarks.
- Natural shape limits.
- Keep terminology as "mouth" rather than cosmetic "lip" where possible.

## Hair

### Current Controls

- Volume up - top
- Volume up - upper side
- Flyaway removal - face side
- Flyaway removal - background side
- Hair gloss
- Hair color select
- Hair color amount
- Gray hair cover

### Needs Work

- Hair region masks.
- Flyaway detection.
- Gloss enhancement.
- Gray hair coverage.
- Color blend mode.

## Clothing

### Current Controls

- Fine wrinkle removal
- Deep wrinkle removal

### Needs Work

- Clothing region detection or manual mask.
- Fine wrinkle smoothing.
- Deep wrinkle repair.
- Preserve fabric texture.

## Next Recommended Steps

1. Clean curve UI labels and behavior.
2. Add curve reset and selected input/output display.
3. Consider extracting preview render orchestration from `MainWindow.xaml.cs`.
4. Keep `PreviewEngineFactory` as the only preview engine selection point.
5. Then evaluate Native CPU engine.
