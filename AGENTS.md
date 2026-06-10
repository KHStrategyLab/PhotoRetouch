# AGENTS.md

## Project Identity

PhotoRetouch is a 64-bit Windows WPF portrait retouching application for daily photographer workflow. The user is a working photographer and is shaping the tool around practical ID/photo retouching needs, not around generic image editor conventions.

The application should feel stable, calm, and predictable. Avoid surprising UI movement, sudden list changes, or heavy live recalculation while the user is adjusting values.

## Current Highest Priority Note

The current work is not a stage for adding tempting new features.

`ORDER_01` through `ORDER_30` are the V1 single-face skin retouch engine stabilization flow. Keep the order sequence intact and finish only the current active order.

Stage and Slider changes must not regenerate SnapshotMask. SnapshotMask regeneration is only for image changes, ReAnalyze, and later manual face-keypoint adjustment.

HardProtect outranks every filter. Eyes, eyebrows, lips, inner mouth, teeth, nostrils, hair, beard, mustache, and glasses remain original even at Stage 10.

Multi-face, left/right ShapeBalance, generative AI retouching, background replacement, and clothing retouch are not discarded. They are Hold / After V1.

The current goal is not feature expansion. The current goal is to finish the V1 engine reliably.

## Program Policy

PhotoRetouch is not an automatic AI face-correction program.

Do not automatically beautify, reshape, resize, move, smooth, or modify the face based only on detected landmarks, ratios, masks, or confidence scores.

Facial structure definitions are used for:

- Local mask creation
- Region classification
- Slider target isolation
- Over-correction prevention
- Protection mask generation
- Confidence checks
- Avoiding unintended edits

Visible correction requires user action. When the user moves a specific slider or activates a specific tool, only the related local region should be calculated and affected.

Do not precompute or apply all base corrections when a tab opens. Do not globally block, smooth, reshape, or modify the entire face. Do not force ideal facial proportions. Do not auto-correct asymmetry unless the specific feature tool is active and the user requests adjustment.

Default state:

- Detect only if needed.
- Protect identity.
- Preserve original shape.
- Apply nothing visible until the user changes a control.

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
- `docs\FACE_RATIO_GUIDES.md`

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
- Left/right balance and symmetry correction are not part of the V1 skin retouch engine. Keep them for a separate V2 `ShapeBalance` geometry-warp module that reuses SnapshotMask, FaceLandmark, and HardProtect.
- Skin filters should remove or reduce only the intended flaw, such as blemishes, acne, moles, or age spots.
- Skin texture must remain usable for ID photos. If a filter smears pores, wrinkles, or natural male skin texture, it is too broad.
- Use narrow masks, skin-tone targeting, optional manual wide-average skin-tone sampling, local edge protection, and feathered protection around eyes, nose, and mouth.
- Prefer a conservative result that preserves the original photo over an aggressive result that looks synthetic.

## Portrait Mask Engine Direction

Skin retouching is now mask-first.

- Do not keep increasing skin filter strength before mask debug output works.
- Use a Snapshot Mask model: analyze one photo once, save a `FaceSnapshotMaskSet`, and reuse it while retouch strengths or Stage presets change.
- Current verification stage uses `StandardMaskWarpEngine`: load or generate `StandardMaskSet`, run `IFaceAnalyzer`, affine-warp it to `MaskWarpInput`, run `IFaceParsingDetector`, then merge warped standard masks with parsing masks before building the snapshot.
- The current default `IFaceAnalyzer` implementation is `OpenCvFaceAnalyzer`; it uses OpenCV YuNet (`Assets/AiModels/face_detection_yunet_2023mar.onnx`) to detect an initial FaceBox plus eye, nose, and mouth anchors. FaceBox is an initial detection container only; `ChinPoint` is estimated from eye/nose/mouth anchor spacing, and visible correction areas must come from fitted masks/protection masks rather than FaceBox.
- K-AnchorMesh final policy: FaceBox may support initial detection, rough normalization, landmark search, debug, and anchor-failure fallback only. Final visible correction masks must follow anchored local masks: K-AnchorMesh anchors -> ComponentROI -> CandidateMask -> FinalMask -> ProtectionMask -> CorrectionMask.
- K-AnchorMesh topology policy: landmark points must be connected into anchor, boundary, surface, protection, measurement, structural, and morph-control edges. Edges guide ROI direction, ratios, surface candidates, protection boundaries, and confidence checks, but edges are not final masks. Pixel evidence still fits final masks.
- Eyebrow protection masks must be fitted from pixel evidence inside an anchor-based brow ROI. Do not treat the `browHead` to `browTail` segment itself as `EyebrowMask`; brow anchors only position the ROI and fallback guide. Brow ROIs must also pass eye-to-brow distance and side-offset ratio guards; do not fix brow masks by hard-coded coordinates.
- Eyebrow detection must be orbit-guided. Use eye center, pupil/iris center when available, upper eyelid, eye width/height, and the superior orbital arc to construct a local orbital-brow ROI above each eye. Do not search eyebrows globally over the face, and do not let forehead wrinkles, eyelid shadows, lashes, bangs, or random hair strands become brow candidates outside this orbital zone.
- Eyebrow 3D guide geometry is a 30-point free polygon shape enclosing the brow hair mass, not a simple guide line. The polygon has upper and lower boundaries around the bundle so thickness, taper, arch, and broken sections can be represented before pixel fitting.
- Eyebrow mask drawing policy: first search inside the brow ROI for the real eyebrow hair bundle, measure the found pixel cluster's left/right endpoints, free angle, free length, thickness, curve peak, and density, then draw a feathered round-cap free arch brush cover from hair-bundle endpoint to endpoint. The brow is a variable-thickness hair mass, not a straight line; it may be sparse, broken, uneven, or occasionally absent. Preserve gaps and density variation when evidence supports them, and do not draw an anchor-only brow when pixel evidence says the brow is missing. The brush cover is still clipped by the brow ROI and protection masks. Do not place a horizontal brow line or move a fixed percentage above the eyes or face box.
- Nose masks must separate `NoseStructureGuide` from editable surface masks. Structure guide lines explain bridge direction, tip, wings, base, and nostril positions only; final `NoseMask` must be an area-based `NoseSurfaceMask` union of bridge, tip, wing, and base surfaces with nostril protection subtracted. Nostril dark spots are protection masks, not the nose mask.
- Mouth/lip masks must respect nose-to-mouth proximity ratios so lip or inner-mouth protection cannot climb into nostrils or nose base. Use anchored distance guards, not fixed y positions.
- Open-mouth topology must never become two independent circles. `LipOuter` uses shared mouth-corner anchors, and upper/lower lip surface loops must both include the same left/right mouth-corner endpoints with `InnerMouthProtectionLoop` between them. Do not create mouth masks from a mouth-center radius.
- Lip directional/phase texture analysis must be guide-centered. Do not scan the full lip as a standalone correction source: start from a lip 3D guide, guide centerline, or guide search mask, clip the nearby search band by `lipSurfaceMask` and protection masks, then use directional texture evidence only to confirm and refine candidate lip line/crack/dryness masks. No guide or no `lipSurfaceMask` means protect-only.
- The current `IFaceParsingDetector` implementation is `TemporaryFaceParsingDetector`; it is a fallback scaffold, not a real AI model. `NostrilDetector` is now implemented as an image-analysis module, but real FaceParsing and triangle mesh model connections remain deferred until debug masks prove stable.
- `SnapshotMaskCacheKey` includes image id, image size, face box, face angle, crop version, and mask version. Stage values are never part of the mask cache key.
- `RetouchStageProcessor` is the current first-pass retouch pipeline. It maps requested Stage `1-10` through `StagePresetMapper`, gates it by `MaskQualityReport.MaxAllowedStage`, and applies retouch only through masks.
- `MaskQualityValidator` now calculates overall, face, landmark, parsing, skin, eye, eyebrow, lip, nostril, hair, hard-protect, and retouch-allow quality scores, plus warnings, fatal errors, strong-retouch safety, and max allowed stage.
- Fail-safe opacity rules lower retouch strength when mask quality is weak, while HardProtect remains dominant over RetouchAllow.
- Current SkinSmooth quality pass creates a mask-aware edge-preserving smooth base, extracts a detail layer, restores texture by stage, applies RetouchAllow/SoftProtect blends separately, and finally restores HardProtect pixels from the original.
- Current BlemishReduce pass detects small local blemish candidates only through RetouchAllow/weak SoftProtect masks, samples nearby skin color, clips out HardProtect, caches candidate analysis by Snapshot key, and changes only correction strength when Stage changes.
- Current WrinkleSoftReduce pass detects linear dark wrinkle candidates through SoftProtect plus weak RetouchAllow masks, splits them into under-eye, glabella, forehead, nasolabial, mouth-corner, neck, and nose-shadow masks, clips out HardProtect, caches candidate analysis by Snapshot key, and changes only correction strength when Stage or toolset values change.
- Current TextureRestore pass runs after SkinSmooth, BlemishReduce, WrinkleSoftReduce, and ToneEven stages. It extracts original detail, restores texture through RetouchAllow and weak SoftProtect masks, reduces restore strength over blemish/wrinkle repair masks, applies a PlasticSkinGuard boost when detail loss is high, caches analysis by Snapshot key, and restores HardProtect from the original.
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
