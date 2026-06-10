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

## Program Policy

PhotoRetouch is not an automatic AI face-correction program.

Detected landmarks, ratios, masks, and confidence scores do not apply visible edits by themselves. They exist to create local masks, classify regions, isolate slider targets, prevent over-correction, generate protection masks, check confidence, and avoid unintended edits.

User action is required before visible correction is applied. When the user moves a specific slider or activates a specific tool, only the related local region should be calculated and affected.

Rules:

- Do not precompute or apply all base corrections when a tab opens.
- Do not globally block, smooth, reshape, resize, move, or beautify the entire face.
- Do not force ideal facial proportions.
- Do not auto-correct asymmetry unless the specific feature tool is active and the user requests adjustment.
- Default state is detect only if needed, protect identity, preserve original shape, and apply nothing visible until a control changes.
- FaceBox is only an initial detection container. Once landmarks and anchor-based masks are available, FaceBox must not define visible correction areas or global editable masks.
- Final masks should follow the K-AnchorMesh hierarchy: FaceBox -> K-AnchorMesh anchors -> ComponentROI -> CandidateMask -> FinalMask -> ProtectionMask -> CorrectionMask.
- Component masks must not be repaired by hard-coded fixed positions. Eyebrow, nose, lip, nostril, jaw, and other masks should be constrained by anchor distance, component ratios, proximity limits, pixel evidence, protection masks, and confidence checks.
- Eyebrow anchors define a brow ROI only. Final eyebrow masks must be fitted from brow hair evidence inside that ROI and clipped by eye-to-brow distance and side-offset ratio guards.
- Nose structure guides are not final masks. Final nose surface masks must cover bridge, tip, left/right wings, and base skin as areas, while nostril interiors remain separate hard protection masks. Lip and mouth masks must be clipped by nose-to-mouth proximity so they cannot overlap nostrils or nose base.

## Shape Geometry Direction

Shape geometry is a controlled photographer tool, not an automatic beauty engine.

K-AnchorMesh / K-AnchorWarp exists to make face rotation, left-right balance, and 2.5D local shape correction possible for the user. It should provide reference points, measurements, handles, falloff regions, and weak controllable morph groups.

The final judgment belongs to the user.

Rules:

- Do not let AI decide whether a face is beautiful.
- Do not automatically force an oval face.
- Do not significantly change the subject's identity or impression.
- Use mesh, symmetry, ratio, yaw-like, pitch-like, and oval-profile values as guides for user controls, not as automatic correction commands.
- Face proportion ranges are documented in `docs/FACE_RATIO_GUIDES.md`; they are measurement and safety guides, not automatic correction targets.
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
