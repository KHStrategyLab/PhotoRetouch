# AGENTS.md

## Project Identity

PhotoRetouch is a 64-bit Windows WPF portrait retouching application for daily photographer workflow. The user is a working photographer and is shaping the tool around practical ID/photo retouching needs, not around generic image editor conventions.

The application should feel stable, calm, and predictable. Avoid surprising UI movement, sudden list changes, or heavy live recalculation while the user is adjusting values.

## Current Highest Priority Note

The current work is not a stage for adding tempting new features.

Current source code is the source of truth. If design documents disagree with code, inspect code first and update the documents before using them as planning references.

`ORDER_01` through `ORDER_30` are the V1 single-face skin retouch engine stabilization flow. Keep the order sequence intact and finish only the current active order.

Stage and Slider changes must not regenerate SnapshotMask. SnapshotMask regeneration is only for image changes, ReAnalyze, and later manual face-keypoint adjustment.

HardProtect outranks every filter. Eyes, eyebrows, lips, inner mouth, teeth, nostrils, hair, beard, mustache, and glasses remain original even at Stage 10.

ShapeBalance is now first-pass code in the current source, but it remains a geometry module, not a skin filter. Multi-face, generative AI retouching, background replacement, and clothing retouch are still Hold / After V1.

The current goal is not feature expansion. The current goal is to finish the V1 engine reliably.

## Mouse-First Workflow

The app is primarily operated by mouse. Keyboard shortcuts exist, but they should support mouse work rather than take over the workflow.

- Favor mouse-visible controls, clear hover/click states, and predictable drag behavior.
- Avoid global keyboard behavior that fires while the user's mouse focus is on the preview or tool panels.
- Photo-list arrow navigation should work only after the user has clicked a photo item in the left photo list. Clicking the preview, tool panel, empty space, or another control should leave/clear that list-navigation context.
- Mouse helper keys are separate from normal keyboard shortcuts. They can include Ctrl, Shift, Alt, Space, or combinations, and are configured in Settings.
- Space is both the default original-compare keyboard shortcut and an allowed mouse helper key. Avoid changing one behavior in a way that unexpectedly breaks the other.

## Repository And Build

- Main repository path: `C:\Users\beint\source\repos\PhotoRetouch`
- Solution: `PhotoRetouch.sln`
- Target platform: x64 only
- Build command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Before building, close the running app to avoid file lock errors:

```powershell
Get-Process PhotoRetouch -ErrorAction SilentlyContinue | Stop-Process -Force
```

Run after successful build:

```powershell
Start-Process -FilePath .\bin\x64\Debug\net8.0-windows\PhotoRetouch.exe -WindowStyle Hidden
```

## Korean Text And Encoding

The app has Korean UI text, so source encoding must stay predictable.

- The project uses `.editorconfig` with `charset = utf-8-bom` for source and config files.
- Keep Korean text files such as `.cs`, `.xaml`, `.json`, and `.md` as UTF-8 with BOM.
- If Korean looks broken in terminal output, first suspect the viewer or shell encoding before rewriting source files.
- In PowerShell, prefer explicit UTF-8 reads for inspection, for example `Get-Content -Encoding UTF8`.
- Avoid broad encoding conversions across the whole repository unless there is a verified file-level problem.

## Important Documents

Read these before making larger design or engine changes:

- `docs\ENGINE_DESIGN.md`
- `docs\FEATURE_STATUS_AND_ROADMAP.md`

These documents describe the current engine state, roadmap, menu status, and next recommended steps.

## Current Product Direction

The app is being built in layers:

1. UI and workflow stability
2. Photo list and preview behavior
3. Preview adjustment engine
4. Curve editor
5. Engine separation
6. Native CPU engine
7. Optional GPU engine

Do not jump directly to GPU or native code without first keeping the current C# engine path stable and separable.

## Customization First Policy

PhotoRetouch should favor photographer-level control over early simplification.

- Keep each tool and each meaningful sub-control independently adjustable during development.
- Do not collapse multiple controls into one shared/global value just to make the code shorter.
- Batch apply, copy settings to selected photos, and global apply are later workflow conveniences, not the base model.
- Multi-select should remain a choosing/comparison workflow until explicit setting-copy or batch behavior is designed.
- If customization makes the code larger, prefer improving structure, preview sizing, caching, and engine separation over removing controls.
- Performance should be solved through screen-sized preview sources, render throttling, engine interfaces, native CPU, optional GPU, and caching.

## Preview Engine Policy

Current preview engine is C# CPU-based.

Key rules:

- Tone correction sliders use throttled live preview while dragging. Future heavy tools may still render only on release if needed.
- Mouse wheel must not adjust retouch sliders.
- Curve point dragging should move only the curve UI while dragging.
- Preview should be rendered when the point is released.
- Preview rendering should run in the background.
- Only one preview render should run at a time.
- While preview rendering is in progress, conflicting input should be blocked.
- Do not darken the whole UI during preview rendering.
- Preview max long side can be limited in settings from 800px to 4000px.
- All interactive tool effects must render against a screen-sized effect preview source, not the full original image.
- Use the current preview viewport/cell size as the first limit for effect preview rendering; the preview setting remains an upper bound.
- This screen-sized preview policy applies to every future tool category: photo adjustment, curves, skin, face shape, eyes, nose, mouth, hair, clothing, and background.
- Multi-select split preview is a choosing/comparison mode. Do not apply retouch effects to multiple selected photos during preview.
- Final export/save must stay conceptually separate and should render from the original source, not from the reduced screen preview.
- Original comparison should continue to use the original source image.

## ID Photo Retouch Policy

PhotoRetouch is a professional ID photo retouching tool for photo studio work. It is not a beauty-filter, face-changing, or cosmetic surgery app.

- Preserve the subject's original identity and impression.
- Do not alter eye, nose, mouth, eyebrow, jaw, or face shape from skin filters.
- Geometry changes belong only in explicit face-shape/feature tools, not in skin cleanup filters.
- Left/right balance and symmetry correction are handled by explicit `ShapeBalance` geometry code when those controls are active. ShapeBalance reuses SnapshotMask, FaceLandmark, and HardProtect, but it must stay separate from the V1 skin filter pipeline.
- Skin filters should remove or reduce only the intended flaw, such as blemishes, acne, moles, or age spots.
- Skin texture must remain usable for ID photos. If a filter smears pores, wrinkles, or natural male skin texture, it is too broad.
- Use narrow masks, skin-tone targeting, optional manual wide-average skin-tone sampling, local edge protection, and feathered protection around eyes, nose, and mouth.
- Prefer a conservative result that preserves the original photo over an aggressive result that looks synthetic.

## Portrait Mask Engine Direction

Skin retouching is now mask-first.

- Do not keep increasing skin filter strength before mask debug output works.
- Use a Snapshot Mask model: analyze one photo once, save a `FaceSnapshotMaskSet`, and reuse it while retouch strengths or Stage presets change.
- Current verification stage uses `StandardMaskWarpEngine`: load or generate `StandardMaskSet`, run `IFaceAnalyzer`, affine-warp it to `MaskWarpInput`, run `IFaceParsingDetector`, then merge warped standard masks with parsing masks before building the snapshot.
- The current default `IFaceAnalyzer` implementation is `OpenCvFaceAnalyzer`; it uses OpenCV YuNet (`Assets/AiModels/face_detection_yunet_2023mar.onnx`) to detect a real FaceBox plus eye, nose, and mouth anchors. `ChinPoint` is still estimated from the detected FaceBox.
- The current `IFaceParsingDetector` implementation is `NoFaceParsingDetector`; it is a fallback scaffold, not a real AI model. `NostrilDetector` is implemented as an image-analysis module, but real pixel-level FaceParsing is still not connected.
- `SnapshotMaskCacheKey` includes image id, image size, face box, face angle, crop version, and mask version. Stage values are never part of the mask cache key.
- ShapeBalance controls may rebuild the cached ShapeBalance map/balanced bundle, but they must not recreate SnapshotMask unless the image, face work area, manual mask, source file, or explicit ReAnalyze condition requires it.
- `RetouchStageProcessor` is the current first-pass retouch pipeline. It maps requested Stage `1-10` through `StagePresetMapper`, gates it by `MaskQualityReport.MaxAllowedStage`, and applies retouch only through masks.
- If ShapeBalance controls are active, `ShapeBalanceProcessor` runs before `RetouchStageProcessor`; the skin pipeline then uses `BalancedImage` and `BalancedMaskSet`.
- `MaskQualityValidator` now calculates overall, face, landmark, parsing, skin, eye, eyebrow, lip, nostril, hair, hard-protect, and retouch-allow quality scores, plus warnings, fatal errors, strong-retouch safety, and max allowed stage.
- Fail-safe opacity rules lower retouch strength when mask quality is weak, while HardProtect remains dominant over RetouchAllow.
- Current SkinSmooth quality pass creates a mask-aware edge-preserving smooth base, extracts a detail layer, restores texture by stage, applies RetouchAllow/SoftProtect blends separately, and finally restores HardProtect pixels from the original.
- Current BlemishReduce pass detects small local blemish candidates only through RetouchAllow/weak SoftProtect masks, samples nearby skin color, clips out HardProtect, caches candidate analysis by Snapshot key, and changes only correction strength when Stage changes.
- Current WrinkleSoftReduce pass detects linear dark wrinkle candidates through SoftProtect plus weak RetouchAllow masks, splits them into under-eye, glabella, forehead, nasolabial, mouth-corner, neck, and nose-shadow masks, clips out HardProtect, caches candidate analysis by Snapshot key, and changes only correction strength when Stage or toolset values change.
- Current TextureRestore pass runs after SkinSmooth, BlemishReduce, WrinkleSoftReduce, and ToneEven stages. It extracts original detail, restores texture through RetouchAllow and weak SoftProtect masks, reduces restore strength over blemish/wrinkle repair masks, applies a PlasticSkinGuard boost when detail loss is high, caches analysis by Snapshot key, and restores HardProtect from the original.
- Skin filter baseline follows a Photoshop frequency-separation idea: smooth low-frequency color/tone through `RetouchAllowMask`, preserve or restore high-frequency skin texture through `TextureRestoreFilter`, reduce restoration over repaired blemish/wrinkle masks, and restore `HardProtectMask` from the original last.
- Fill small enclosed holes in skin/color masks before feature exclusion, but never refill deliberate protection holes for eyes, lips, nostrils, hair, beard, glasses, clothing, or background.
- Current Toolset alignment uses `RetouchToolset` and `AppliedRetouchOptions`: Stage presets provide defaults, and changed sliders act as user overrides without rebuilding SnapshotMask.
- HardProtect always keeps the original pixels. SoftProtect is blended at low opacity. RetouchAllow receives the main skin smoothing blend.
- Never rerun face analysis only because Stage `1-10`, `SkinSmooth`, `BlemishReduce`, `ToneEven`, `TextureRestore`, or before/after view changed.
- Rebuild Snapshot Mask only for new image load, explicit face re-analysis, crop or rotation changes, manual mask edits, source file changes, or major face position changes.
- Treat retouch failures as mask failures first and filter failures second.
- Build detection, landmark, parsing, part-mask building, validation, debug export, retouch filters, and texture restore as separate modules.
- Landmark output provides position anchors. Face parsing provides pixel boundaries. Final masks should combine both.
- Required final masks include `SkinMask`, `EyeMask`, `EyebrowMask`, `LipMask`, `InnerMouthMask`, `TeethMask`, `NoseMask`, `NoseSkinMask`, `NostrilMask`, `NoseShadowMask`, `HairMask`, `BeardMask`, `MustacheMask`, `GlassesMask`, `HardProtectMask`, `SoftProtectMask`, and `RetouchAllowMask`.
- `HardProtectMask` must include eyes, eyebrows, lips, inner mouth, teeth, nostrils, hair, beard, mustache, and glasses. These areas stay at 0% retouch opacity even at the strongest retouch stage.
- `SoftProtectMask` should include under-eye skin, nasolabial folds, nose tip, nose wings, forehead wrinkles, and neck wrinkles.
- `RetouchAllowMask` should be skin, nose skin, and optional neck skin minus hard protection.
- Nostrils are protected detail, not skin. Implement `NostrilDetector` separately and put nostrils into `HardProtectMask`.
- Current `NostrilDetector` builds a lower-nose ROI, extracts dark candidates, runs connected components, combines selected components with warped standard nostril fallback, and forces the final nostril mask into HardProtect.
- If detection is uncertain, protect wider and retouch weaker.
- Required debug mask images must exist before skin filters are considered reliable: original, face box, landmarks, parsing, skin, eye, eyebrow, lip, inner mouth, nose, nose skin, nostril, hair, beard, glasses, hard protect, soft protect, retouch allow, and final overlay.
- Prepare a real test image set before judging mask quality: age/gender coverage, acne, blemishes, freckles, redness, wrinkles, pores, beard shadow, large nostrils, strong under-nose shadow, thick eyebrows, clear eyelashes, strong lip edge, glasses, beard, hair touching the face, smiling/open-mouth images, near-side faces, strong lighting, and strong shadows.
- The first UI surface for this work should include mask debug viewing before stronger retouch controls.

## K-AnchorMesh And K-AnchorMorph Purpose

K-AnchorMesh and K-AnchorMorph are not automatic AI beautification or automatic cosmetic-surgery engines.

Their purpose is to provide photographer-controlled shape correction handles that the user can adjust while watching the image.

The engine should only:

- Locate face feature positions.
- Build reference points for eyes, nose, mouth, chin, jaw, and face outline.
- Measure lengths, ratios, symmetry, width profiles, and guide scores.
- Provide weak, local MorphGroups that sliders can use when the relevant control is active.

The final judgment belongs to the user.

- Do not let AI decide whether a face is beautiful or not.
- Do not automatically force a face into an oval shape.
- Do not significantly change the subject's original identity or impression.
- Use oval scores, symmetry values, ratios, and mesh data as guides for controls, not as automatic correction commands.
- ShapeBalance and K-AnchorMorph must stay separate from skin filters.
- Image pixels and masks should be transformed together only when an explicit geometry tool is active.

Core goal: make shape correction possible for the human operator by providing reliable handles, measurements, and weak controllable morph groups.

## K-AnchorWarp Direction

The shape-correction direction is moving from slider-centered edits to direct 2.5D handle-based local warp.

K-AnchorWarp is not an automatic beautification engine. It is a controlled geometry tool that lets the user drag handles on top of the face while watching the image.

Implementation order:

1. Separate real handle groups from the mesh.
2. Let handle dragging preview mesh movement first, without changing pixels.
3. Define `WarpGroup` with control, falloff, and locked points.
4. Implement an ROI-based MLS Similarity or MLS Affine warp solver.
5. Add local preview warp for eyes, mouth, chin, jawline, and face outline first.
6. Add final high-quality render after the preview path is stable.

Rules:

- Keep K-AnchorWarp separate from the existing retouch engine.
- Do not replace skin filters with geometry warp.
- Do not automatically beautify or force oval shape.
- Use limited safe zones, local ROI warp, and handle-based interaction.
- Mesh movement preview comes before pixel warp.
- Image pixels and masks must be warped together only when the explicit geometry tool is active.
- Final quality render must be separate from fast interactive preview.

Initial handle targets: `LeftEye`, `RightEye`, `LeftBrow`, `RightBrow`, `Nose`, `Mouth`, `Philtrum`, `Chin`, `Jawline`, and `FaceOutline`.

K-AnchorWarp should expose workflow modes instead of one intimidating free-transform surface:

- Easy Liquify mode: show only simple, safe handles for common photographer adjustments.
- Advanced Liquify mode: expose more detailed local handles, falloff controls, and debug guides.
- Auto Assist mode: use measurements and guide scores only to propose or initialize weak adjustments; the user still approves and controls the result.
- Full AUTO mode, if ever added, must remain optional and conservative. It must not replace the user as the final judge.

Default should be Easy Liquify. Advanced and Auto modes should be separate surfaces or tabs if they do not fit cleanly into the existing tool panels.

Separate shape tools into two user-facing families:

1. Liquify

   This is the easy Photoshop-like brush tool. It should feel approachable and local.

   Target behavior is closer to Photoshop CS3 Liquify: direct brush-based deformation, not modern automatic face-aware beautification.

   Initial brush modes: push, bloat, pinch, restore, and protect.

   Typical use: small jawline nudges, cheekbone nudges, cheek line, temple, sideburn/ear-side line, neck, clothing edge, and other light local corrections.

   Liquify is a visual brush workflow. It is not a face-feature handle workflow.

2. Mesh Tool

   This is the face-specific handle tool built on K-AnchorMesh / K-AnchorWarp.

   Initial handle bundles: eyes, nose, mouth, brows, philtrum, chin, jawline, cheekbone, temple, and ear-side.

   Intended interaction: click a face handle, drag it, use arrow keys for fine adjustment, Enter to commit, and Esc to cancel.

   Mesh Tool is not slider-first. It should let the user directly grab grouped face handles on the image.

## Engine Roadmap

Already completed before adding C++:

- Introduced a `PreviewAdjustment` model.
- Introduced an `IPreviewEngine` interface.
- Moved current pixel code into a `CSharpPreviewEngine`.
- Added `PreviewEngineFactory`.
- Added `PreviewSourceFactory` for screen-sized preview source creation.
- Moved app settings models out of `SettingsWindow.xaml.cs` into `Settings/`.

Later:

- Add `NativeCpuPreviewEngine` using a C++ x64 DLL.
- Keep CPU C# as the safe fallback.
- GPU remains optional and should not be the default.

Settings already contains a `Performance` tab placeholder:

- Stable mode - CPU engine
- Accelerated mode - GPU engine, implementation pending

This may later be renamed or expanded when native CPU engine is added.

## UI Design Preferences

- Use neutral colors. Avoid red accents because they can confuse photo judgment.
- Avoid sudden layout shifts.
- Keep list selection borders 1px so items do not shake.
- Do not use heavy overlays unless absolutely necessary.
- Use compact, work-focused UI rather than marketing-style screens.
- Preserve the current right-monitor maximized startup behavior.
- Keep controls photographer-friendly in language.

## Photo List And Preview Rules

- Work folder is configured in settings.
- App preloads the work folder on startup.
- `새로고침` manually loads newly added files.
- Do not auto-sync the work folder yet.
- Prevent duplicate photo paths.
- Multi-select is limited to 8 images.
- Split preview:
  - 2 images: 2 split
  - 4 images: 4 split
  - 5 images: 3+2
  - max 2 rows, max 4 columns
- Selecting a photo from the list should reset selected preview transforms to Fit in.
- Split preview zoom max should allow source 1:1, not beyond.
- Dragging zoomed images should not reveal blank space.

## Curve Editor Direction

Target behavior is closer to Photoshop-style curves, not a simple slider.

Current rules:

- Channels: All, R, G, B
- Max 7 anchors per channel
- Default corner anchors start at 0,0 and 255,255, but they are editable and deletable
- Dragging anchors past each other is allowed so cross-curve shapes can be created
- Add points by clicking the curve canvas/line
- Drag points to edit
- Delete selected point with Delete or Backspace
- Drag a point outside the curve box to delete
- Point movement should not render the image continuously
- Render preview on release

Still needed:

- Cleaner curve labels
- Better point hit testing
- Smooth interpolation
- Selected point input/output display
- Per-channel reset
- Shortcut settings entry for curve point deletion

## Settings Files

Settings are stored under:

```text
%APPDATA%\PhotoRetouch
```

Known files:

- `section-order.json`
- `shortcuts.json`
- `preview-settings.json`
- `working-folder.json`
- `performance-settings.json`

## Working With Code

- Prefer scoped changes.
- Do not refactor large parts unless it directly supports the current step.
- Use `apply_patch` for manual edits.
- Keep build verification with x64.
- If app is running, stop it before build.
- Do not revert unrelated user changes.

## Suggested Next Engineering Step

The next engineering step is the mask engine, not stronger skin filtering:

1. Add `FaceAnalysisResult` and `FaceMaskSet` models.
2. Add mask builder module folders for skin, eyes, eyebrows, mouth, nose, nostrils, hair, beard, glasses, hard protect, soft protect, and final retouch allow masks.
3. Add `DebugMaskExporter.SaveAll(...)` for required mask PNGs.
4. Add a first safe heuristic mask implementation if AI model integration is not ready yet.
5. Only after debug masks are inspectable, reconnect weak `SkinSmooth`, `BlemishReduce`, `ToneEven`, and `TextureRestore` through `RetouchAllowMask`.
