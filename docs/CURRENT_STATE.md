# PhotoRetouch Current State

Last updated: 2026-06-10

This document is the active project baseline. When older order documents disagree with the current source code, this document and the source code win.

## Product Direction

PhotoRetouch is a Windows WPF portrait retouching app for real photo-studio work.

The current goal is not feature expansion. The current goal is to make the existing workflow stable enough for daily use:

- Load a photo quickly.
- Show the selected photo clearly.
- Let the user open tools only when needed.
- Avoid hidden heavy processing.
- Keep per-photo tool values while the app session is active.
- Make mask preview trustworthy before strengthening retouch filters.

## Current Priority

The active priority is AUTO MASK.

AUTO MASK is not AI MASK. It is a color/range based mask workflow with feature protection and session caching.

Current work is focused on:

- Face skin color mask quality.
- Small mask-hole filling.
- Protecting eyes, eyebrows, lips, inner mouth, teeth, nose structure, nostrils, glasses, hair, beard, clothing, and background.
- Making mask preview fast enough to feel usable.
- Saving debug mask files only when useful.

## Shape Geometry Direction

Shape geometry is a controlled photographer tool, not an automatic beauty engine.

K-AnchorMesh / K-AnchorWarp exists to make face rotation, left-right balance, and 2.5D local shape correction possible for the user. It should provide reference points, measurements, handles, falloff regions, and weak controllable morph groups.

The final judgment belongs to the user.

Rules:

- Do not let AI decide whether a face is beautiful.
- Do not automatically force an oval face.
- Do not significantly change the subject's identity or impression.
- Use mesh, symmetry, ratio, yaw-like, pitch-like, and oval-profile values as guides for user controls, not as automatic correction commands.
- Shape tools may let the user "shake" or drag the face structure through handles, sliders, or brush-like local liquify.
- Image pixels and masks should move together only when an explicit geometry tool is active.
- Mesh movement preview should come before pixel warp.
- K-AnchorMesh / K-AnchorWarp must stay separate from SkinRetouch.

## Current Source Of Truth

The current source code is the source of truth.

Important modules:

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Models/PhotoItem.cs`
- `Models/PreviewRenderTier.cs`
- `Tools/Masking/AverageFaceColorMaskBuilder.cs`
- `Tools/Masking/DebugMaskExporter.cs`
- `Tools/Masking/StandardMaskWarpEngine.cs`
- `Tools/Masking/RetouchStageProcessor.cs`
- `Tools/Shape/*`
- `Core/AnchorMesh/*`
- `Core/Vision/*`
- `Core/Masks/*`
- `Tools/PhotoAdjustment/*`

## User Experience Rules

The user is a working photographer. The app should be judged by working feel, not only by benchmark numbers.

Rules:

- If the fan spins hard during a simple UI action, something is too heavy.
- Slider movement must not feel stuck.
- Clicking the slider track must not send the value racing to the end.
- The preview must not become blurry just because a tool panel opened.
- Same photo plus same AUTO MASK range should reuse the cached mask.
- Switching away from a photo and back should restore that photo's values until refresh.
- Reset should happen only on app start, refresh, explicit reset, or source image reload.
- Do not surprise the user with sudden layout changes or hidden processing.

## Current Working Status

Implemented or partially implemented:

- WPF desktop UI.
- Photo list and single-photo editing flow.
- Preview render tier model.
- C# preview engine path.
- Curve editor first pass.
- ShapeBalance first-pass architecture.
- K-AnchorMesh / 2.5D geometry scaffolding for future user-controlled rotation and balance tools.
- Retouch stage processor first pass.
- AUTO MASK preview and debug export.
- Per-photo retouch state memory during the session.
- Per-photo AUTO MASK preview cache.
- Slider track click lockout.

Not final:

- Real AI face parsing.
- Final skin retouch quality.
- Blemish healing quality.
- Full ShapeBalance UI and user-friendly tool grouping.
- Export pipeline polish.
- Batch workflow.
- Installer/package flow.

## Documentation Policy

This documentation set replaces the old accumulated order documents as the active baseline.

Old documents are historical reference only. Do not use them as active planning instructions unless the current source code confirms them.
