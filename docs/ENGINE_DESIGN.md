# PhotoRetouch Engine Design

## Goal

PhotoRetouch is a 64-bit portrait retouching tool for daily photo work. The UI should stay predictable and calm, while preview generation must feel responsive enough for repeated professional use.

The current priority is not maximum theoretical speed. The priority is stable behavior, clear preview quality, and a path toward faster engines without rewriting the whole application.

Customization is a product requirement. Do not remove or merge useful photographer controls simply to make the code smaller or the current engine faster. Optimize the preview and engine path instead.

## Current Engine

The current preview engine is a C# CPU engine behind `IPreviewEngine`.

- WPF handles the UI and image display.
- `PreviewAdjustment` carries tone and curve parameters.
- `PreviewEngineFactory` chooses the active preview engine.
- `CSharpPreviewEngine` contains the current managed pixel loop.
- `PreviewSourceFactory` creates screen-sized effect preview sources.
- App settings state lives in `Settings/` classes, separate from `SettingsWindow.xaml.cs`.
- Pixel adjustments are calculated on a background thread.
- Input is blocked while a preview render is running.
- Tone correction sliders use throttled live preview while dragging; heavier future tools may still commit on mouse release.
- Curve preview has been experimented with, but heavy image recalculation during point movement should be avoided.
- Preview processing can use a reduced preview image size from settings.

Current implemented adjustment order:

1. Preview source sizing
2. Exposure (`-15` to `+15`, with one-decimal display for smoother adjustment; negative exposure protects near-white highlights)
3. Contrast (`-25` to `+25`)
4. Saturation
5. White balance
6. Curve LUT
7. Skin tone evening
8. Blemish removal
9. Skin texture smoothing
10. Pore cleanup
11. Face-shape preview warp and localized face-area refinement passes
12. Blur or sharpen

## Preview Policy

The preview is for judging the edit, not final export quality.

- Interactive effect preview should render against the image size actually needed by the current preview viewport.
- The visible preview cell size is the first sizing target for effect preview rendering.
- Low performance users can also set a maximum long side size.
- Current allowed settings preview long side range is 800px to 4000px.
- The settings preview limit is an upper bound; it should not force effect previews to render larger than the visible preview needs.
- This policy applies to all interactive tool categories, not only photo adjustment: curves, skin, face shape, eyes, nose, mouth, hair, clothing, and background.
- Multi-select split preview is for choosing and comparing photos. It should show original/lightweight views and avoid applying retouch effects to all selected images.
- Original comparison should continue to show the original source image.
- Future export should not be limited by preview size.

## Interaction Policy

Heavy work should not run continuously while the user is dragging unless it is throttled and working against the screen-sized preview source.

- Tone correction sliders: update UI while dragging and render throttled live preview.
- Future heavy sliders: update UI while dragging, render preview on release unless an optimized preview path exists.
- Curve point movement: move curve UI while dragging, render preview on release.
- Curve opacity or strength: render with throttled live preview.
- Mouse wheel should not adjust retouch sliders.
- Photo-list arrow navigation should be contextual. It should only apply after clicking a photo item in the left list, and should stop after clicking preview, tools, empty space, or other controls.
- Mouse helper gestures are separate from keyboard shortcuts and may include Space as well as Ctrl, Shift, and Alt.
- Individual tool controls should remain independently adjustable. Shared/global application should be added later as explicit copy or batch workflow.

## Geometry Tool Policy

Face shape, eyes, nose, mouth, hair volume, clothing shape, and other geometry tools should not be approximated by global image transforms.

- Each photo can carry a normalized face work area as the first geometry target model.
- The current UI can show and edit a face work area guide when the Face Shape section is active.
- The first oval-face pass uses the editable face work area as its explicit target region.
- Jawline clarity currently uses the editable face work area for a localized lower-side edge pass.
- Broader geometry rendering should move behind a dedicated warp engine as controls grow.

## Skin Filter Policy

Skin cleanup must follow photo-studio ID photo standards.

- Skin filters must not reshape or soften the subject's eyes, nose, mouth, eyebrows, jaw, or face outline.
- Face-shape and feature changes belong only to their explicit geometry tools.
- Blemish, acne, mole, and age-spot passes should use narrow masks and should only affect the detected target areas.
- Preserve natural skin texture. A result that looks like global blur, plastic skin, or beauty-filter smoothing is not acceptable for this product.
- Use face-area targeting, average skin-tone matching, optional manual wide-average skin-tone sampling, local edge protection, and feathered protection around eyes, nose, and mouth.
- When uncertain, reduce the filter effect rather than risk damaging the original expression or professional ID photo realism.
- Portraiture-style references are useful for the engine direction: precise skin masking, smoothness by detail scale, controlled fill-light/tone support, and clear before/after preview. PhotoRetouch should keep those ideas conservative for ID photo work. Reference: https://www.imagenomic.com/Products/Portraiture

## Portrait Retouch AI Mask Engine

The next major engine direction is mask-first retouching. Do not continue increasing skin filter strength until mask debug output is reliable.

## Snapshot Mask Policy

A `Snapshot Mask` is the face-specific mask set created for one original photo. It is created from the original image analysis and then reused while the user changes retouch strength.

Core rules:

- Create the snapshot mask when a new photo is loaded or when the user explicitly requests face re-analysis.
- Do not recreate the snapshot mask when the user changes Stage `1-10`, skin smooth strength, blemish reduce strength, tone even strength, texture restore strength, or before/after view.
- Recreate the snapshot mask after crop or rotation changes, manual mask edits, face work area changes, or a source file version change.
- Reuse the snapshot mask to keep preview interaction fast and stable.
- If AI confidence is weak, widen protection and reduce retouch strength inside the snapshot rather than rebuilding repeatedly.

`FaceSnapshotMaskSet` carries:

- `ImageId`
- `SourcePath`
- `SourceLastWriteTimeUtc`
- `SourceLength`
- `FaceAnalysisResult`
- `FaceMaskSet`
- `MaskQualityReport`
- `CreatedAtUtc`

Snapshot creation flow:

1. `InputImage`
2. `FaceDetection`
3. `FaceLandmark`
4. `WarpStandardMasks`
5. Optional `FaceParsing`
6. `NostrilDetector`
7. `BuildHardProtectMask`
8. `BuildSoftProtectMask`
9. `BuildRetouchAllowMask`
10. Save `FaceSnapshotMaskSet`
11. Apply retouch using the snapshot

The current verification implementation uses `SnapshotMaskBuilder` with `StandardMaskWarpEngine`.
This stage intentionally does not connect real AI models. It verifies `StandardMaskSet` loading or generation, affine warping, temporary FaceParsing merge, snapshot creation, cache reuse, debug overlay output, and hard-protect behavior first.
`TemporaryFaceParsingDetector` implements the `IFaceParsingDetector` contract as a fallback scaffold. Real AI detection, landmark, parsing, nostril detection, and triangle mesh warping must keep the same snapshot contract.

Current Standard Mask resources:

- `standard_skin_mask`
- `standard_eye_protect_mask`
- `standard_eyebrow_protect_mask`
- `standard_lip_protect_mask`
- `standard_nose_mask`
- `standard_nostril_mask`
- `standard_soft_protect_mask`

If PNG files are not present, `StandardMaskLoader` generates temporary grayscale masks and records debug warnings instead of failing.

Current cache key:

- `ImageId`
- `ImageWidth`
- `ImageHeight`
- `FaceBox`
- `FaceAngle`
- `CropVersion`
- `MaskVersion`

Stage values are not part of the cache key.

Current FaceAnalyzer:

- `IFaceAnalyzer`
- `FaceAnalyzerResult`
- `OpenCvFaceAnalyzer`
- `TemporaryFaceAnalyzer`
- OpenCV YuNet model resource: `Assets/AiModels/face_detection_yunet_2023mar.onnx`
- Real detected `FaceBox`
- Real detected `LeftEyeCenter`, `RightEyeCenter`, `NoseTip`, and `MouthCenter`
- Estimated `ChinPoint` from the detected `FaceBox`
- `FaceAngle` from the eye line
- Debug warnings for multiple faces, high face angle, low landmark confidence, and estimated chin point

`TemporaryFaceAnalyzer` remains available as a scaffold/fallback class, but it is no longer the default analyzer.

Current FaceParsing scaffold:

- `IFaceParsingDetector`
- `FaceParsingInput`
- `ParsingMaskSet`
- `ParsingLabelMapper`
- `TemporaryFaceParsingDetector`

The current merge rule is conservative: protect masks are widened with parsing output, while retouch-allow skin remains constrained by the warped standard mask and hard protection.

Current first-pass retouch pipeline:

- `StagePresetMapper` maps Stage `1-10` to skin smooth, blemish reduce, tone even, texture restore, soft protect opacity, and retouch allow opacity.
- `BlemishReduceFilter` now provides the first local mask-based blemish pass. It detects small candidates inside `RetouchAllowMask`, avoids `HardProtectMask`, applies weaker handling in `SoftProtectMask`, samples a surrounding skin ring, and caches candidate analysis by Snapshot cache key so Stage changes only change strength.
- `WrinkleSoftReduceFilter` now provides the first line-based wrinkle softening pass. It detects dark linear candidates from `SoftProtectMask` plus weak `RetouchAllowMask`, separates under-eye, glabella, forehead, nasolabial, mouth-corner, neck, and nose-shadow masks, samples surrounding skin, preserves facial structure with low correction caps, and caches candidate analysis by Snapshot cache key so Stage/toolset changes only change strength.
- `ToneEven` currently runs as a simple mask-aware processor stage after wrinkle reduction and before texture restoration. The full `ORDER_13_TONE_EVEN` dedicated filter, candidate masks, reports, and slider/toolset binding still need follow-up.
- `TextureRestoreFilter` now provides the final skin texture restoration pass. It extracts a high-frequency detail preview from the original image, restores limited detail through RetouchAllow and weak SoftProtect masks, lowers restore strength over blemish and wrinkle repair masks, applies PlasticSkinGuard when texture loss is high, restores HardProtect from the original image, and caches analysis by Snapshot cache key so Stage/toolset changes only change strength.
- `MaskQualityReport.MaxAllowedStage` gates the requested stage.
- `RetouchStageProcessor` creates a mask-aware edge-preserving smooth base, applies local blemish and wrinkle passes, applies ToneEven, restores texture, and then runs `HardProtectFinalRestoreFilter`.
- `RetouchToolset` and `AppliedRetouchOptions` now sit between UI slider values and `RetouchStageProcessor`. Stage presets provide defaults, while changed sliders are treated as user overrides without rebuilding SnapshotMask.
- `RetouchStageProcessor` records `PipelineDebugReport` so Stage changes can be checked as filter-only passes over a reused SnapshotMask.
- `RetouchDebugExporter` saves stage, smooth base, detail layer, tone-even image, texture restored image, retouch allow applied, soft protect applied, hard protect restored, final retouch mask, final outputs, pipeline debug images, and compare overlays.

HardProtect remains original at every stage. SoftProtect receives only low-opacity retouch. RetouchAllow receives the main skin retouch blend.

Benchmark direction:

- Imagenomic Portraiture-style workflow.
- The product value comes from AI masking, skin smoothing, and natural texture preservation.
- Skin should be retouched while eyes, eyebrows, lips, nostrils, teeth, hair, beard, mustache, and glasses stay protected.
- Retouch failure should be treated as a mask failure first and a filter failure second.

Failure criteria:

- Eyes become hazy.
- Iris or sclera detail becomes blurred.
- Eyelid edge becomes smeared.
- Eyelashes disappear or become skin-like.
- Eyebrow hair spreads into skin.
- Lip edge becomes soft.
- Inner mouth or teeth receive skin retouching.
- Nostrils are filled or flattened.
- Under-nose shadow disappears enough to flatten the nose.
- Hair edges spread into skin.
- Beard or mustache texture becomes smeared.
- Glasses frames become blurred.
- Skin becomes plastic.

Required pipeline:

1. `InputImage`
2. `FaceDetection`
3. `FaceLandmark`
4. `FaceParsing`
5. `PartMaskBuilder`
6. `NostrilDetector`
7. `HardProtectMaskBuilder`
8. `SoftProtectMaskBuilder`
9. `RetouchAllowMaskBuilder`
10. `MaskQualityValidator`
11. `DebugMaskExporter`
12. `RetouchFilter`
13. `TextureRestore`
14. `OutputImage`

Initial model roles:

- `FaceDetection` detects face boxes.
- `FaceLandmark` detects eyes, nose, mouth, eyebrows, and jawline coordinates.
- `FaceParsing` provides pixel labels such as skin, eye, eyebrow, nose, lip, mouth, hair, glasses, beard, and mustache.

Candidate model direction:

- MediaPipe Face Landmarker or Face Mesh-style output is a good fit for coordinate-based part recognition and face ROI alignment.
- CelebAMask-HQ-style face parsing is a good fit for pixel-level facial part separation. Useful label families include skin, nose, eyes, eyebrows, mouth, lip, hair, eyeglass, neck, and cloth.
- The app should not depend on one model forever. Keep detection, landmark, parsing, mask building, validation, and retouch filters as separate replaceable modules.

Recognition should use three layers:

1. `Layer A - FaceDetection`: find the face box.
2. `Layer B - FaceLandmark`: find position anchors for eyes, nose, mouth, eyebrows, and jawline.
3. `Layer C - FaceParsing`: classify pixel regions such as skin, eye, brow, nose, lip, mouth, and hair.

Final masks are built by combining landmark and parsing results. Landmarks provide position stability; parsing provides real pixel boundaries. Either one alone is too weak for photo-studio retouching.

`FaceAnalysisResult` should carry:

- `FaceBox`
- `FaceLandmarks`
- `FaceParsingLabels`
- `FaceAngle`
- `FaceQualityScore`
- `LandmarkConfidence`
- `ParsingConfidence`

`FaceMaskSet` should carry:

- `SkinMask`
- `EyeMask`
- `EyebrowMask`
- `LipMask`
- `InnerMouthMask`
- `TeethMask`
- `NoseMask`
- `NoseSkinMask`
- `NostrilMask`
- `NoseShadowMask`
- `HairMask`
- `BeardMask`
- `MustacheMask`
- `GlassesMask`
- `HardProtectMask`
- `SoftProtectMask`
- `RetouchAllowMask`
- `FinalOverlayMask`

Mask rules:

- `HardProtectMask` means retouch opacity is `0%`.
- `SoftProtectMask` means retouch opacity is usually `20%` to `60%`.
- `RetouchAllowMask` means normal retouch opacity is allowed.
- `RetouchAllowMask = SkinMask + NoseSkinMask + optional NeckSkinMask - HardProtectMask`.

Hard protection includes:

- `EyeMask`
- `EyebrowMask`
- `LipMask`
- `InnerMouthMask`
- `TeethMask`
- `NostrilMask`
- `HairMask`
- `BeardMask`
- `MustacheMask`
- `GlassesMask`

Soft protection includes:

- `UnderEyeMask`
- `NasolabialFoldMask`
- `NoseTipMask`
- `NoseWingMask`
- `ForeheadWrinkleMask`
- `NeckWrinkleMask`

Part-specific rules:

- `EyeMask` should combine landmark eye polygons and parsing eye masks. It must include iris, eyelid edge, eye corners, and eyelash area. Apply small dilation and feather. Eyes are always hard-protected.
- `EyeSoftProtect` should cover under-eye skin, tear troughs, and dark-circle areas. It can receive weak correction, but never the same strength as cheek skin.
- `EyebrowMask` should combine parsing eyebrow masks and expanded landmark eyebrow polygons. Apply stronger dilation than eye. Eyebrows are always hard-protected.
- `MouthMask` should split upper lip, lower lip, inner mouth, teeth, and lip edge. Lip, inner mouth, and teeth are always hard-protected. Skin around mouth may be retouchable. Nasolabial folds are soft-protected.
- `NoseMask` must not be treated as one area. Split nose bridge, nose side, nose tip, nose wing, nostril, and nose shadow. Nose bridge and nose side may be retouchable. Nose tip and nose wing are soft-protected. Nostril and strong nose shadow are hard-protected or strong-protected.
- `HairMask` should come primarily from parsing. Hair touching the face outline must remain protected.
- `BeardMask` and `MustacheMask` may start from parsing if available, then use color and texture cues as fallback. Beard and mustache are hard-protected.
- `GlassesMask` may start from parsing if available, then use edge and shape cues as fallback. Frames and the nearby eye region should be protected.

Part policy table:

- Eyes, iris, sclera, eyelid edges, eye corners, and eyelashes: hard protect.
- Under-eye skin, tear troughs, dark circles, and under-eye fine wrinkles: soft protect.
- Eyebrows and eyebrow shadows: hard protect or strong soft protect.
- Upper lip, lower lip, lip edge, inner mouth, teeth, and mouth corners: hard protect.
- Nasolabial folds: soft protect.
- Nose bridge and nose side skin: retouch allow.
- Nose tip, nose wings, and philtrum: soft protect.
- Nostrils: hard protect.
- Under-nose shadow: hard protect or soft protect.
- Cheeks, forehead, chin, jaw skin, and neck skin: retouch allow.
- Forehead wrinkles and neck wrinkles: soft protect.
- Hair, beard, mustache, glasses, earrings, and accessories: hard protect.

Builder decomposition:

- `SkinMaskBuilder`
- `EyeMaskBuilder`
- `EyebrowMaskBuilder`
- `MouthMaskBuilder`
- `NoseMaskBuilder`
- `NostrilMaskBuilder`
- `HairMaskBuilder`
- `BeardMaskBuilder`
- `GlassesMaskBuilder`
- `HardProtectMaskBuilder`
- `SoftProtectMaskBuilder`
- `RetouchAllowMaskBuilder`
- `FinalOverlayMaskBuilder`

Do not put all facial part logic into one file. The mask engine should stay readable and testable because mask failure is the main risk for the product.

### NostrilDetector

Nostrils are protected detail, not skin.

Current implementation status:

- `NostrilDetectorInput`
- `NostrilDetector`
- `NostrilDetectorResult`
- `NostrilCandidateComponent`
- Lower-nose ROI from nose tip to upper philtrum
- Dark candidate extraction from ROI luminance distribution
- Connected component analysis
- Candidate scoring by size, shape, position, pair structure, and warped standard overlap
- Warped standard nostril fallback
- Final nostril mask dilation and feather
- HardProtect injection through `StandardMaskWarpEngine`

Input:

- Original image
- Nose landmarks
- Nose mask
- Lip mask
- Optional beard or mustache mask

Process:

1. Build a lower-nose ROI from lower nose landmarks.
2. Find dark pixel candidates inside the lower-nose ROI.
3. Run connected-component analysis.
4. Filter components by size, shape, and position.
5. Prefer a left/right symmetric nostril pair.
6. Exclude components overlapping lip, mouth, beard, or mustache.
7. Apply small dilation and feather.
8. Return `NostrilMask` and `NostrilConfidence`.

Fallback:

- If nostril confidence is low, widen the lower-nose protection area.
- Reduce retouch opacity around lower nose.
- Never smooth the lower nose blindly.

### Mask Quality Validator

The validator must run before retouch filters.

Checks:

- Face detected.
- Face angle is within a usable range.
- Face size is sufficient for reliable retouching.
- Face blur is acceptable.
- Landmark confidence is usable.
- Parsing confidence is usable.
- Skin mask area ratio is plausible.
- Eye mask area ratio is plausible.
- Lip mask area ratio is plausible.
- Nose mask area ratio is plausible.
- Nostril confidence is usable, or fallback protection was expanded.
- Landmark-derived masks and parsing-derived masks overlap plausibly.

If validation is weak:

- Expand protection.
- Reduce retouch strength.
- Export debug masks so the failure can be inspected visually.

Current implementation status:

- `MaskQualityValidator`
- Detailed `MaskQualityReport`
- `OverallQualityScore`
- Face, landmark, parsing, skin, eye, eyebrow, lip, nostril, hair, hard-protect, and retouch-allow quality scores
- `MaxAllowedStage`
- `IsSafeForStrongRetouch`
- `DebugWarnings`
- `FatalErrors`
- Stage gate reuse through `RetouchStageProcessor`
- Fail-safe opacity reduction for weak eye, lip, nostril, hair, skin, hard-protect, and retouch-allow quality
- Add a debug warning explaining which part was weak.

Failure prevention rules:

- If eye mask validation fails, protect a wider eye region.
- If lip or mouth validation fails, protect a wider mouth region.
- If nostril detection fails, weaken lower-nose retouch and protect lower nose more broadly.
- If eyebrow validation fails, protect a wider eyebrow region.
- If hair validation fails, weaken retouch around the face outline.
- If glasses are detected, weaken eye-region retouch.
- If beard or mustache is detected, weaken mouth, chin, and jaw retouch.

### Debug Mask Export

Mask work is not considered implemented until debug images can be saved and inspected.

Required debug outputs:

- `debug_original.png`
- `debug_face_box.png`
- `debug_landmarks.png`
- `debug_parsing.png`
- `debug_skin_mask.png`
- `debug_eye_mask.png`
- `debug_eyebrow_mask.png`
- `debug_lip_mask.png`
- `debug_inner_mouth_mask.png`
- `debug_nose_mask.png`
- `debug_nose_skin_mask.png`
- `debug_nostril_mask.png`
- `debug_hair_mask.png`
- `debug_beard_mask.png`
- `debug_glasses_mask.png`
- `debug_hard_protect.png`
- `debug_soft_protect.png`
- `debug_retouch_allow.png`
- `debug_final_overlay.png`

### Skin Retouch Stages

The stage control should map to conservative professional behavior, not beauty-filter behavior by default.

- Stage `1-3`: natural, high texture keep, weak smoothing.
- Stage `4-6`: studio portrait, medium smoothing, medium blemish reduction.
- Stage `7-8`: beauty retouch, strong smoothing, texture restore required.
- Stage `9-10`: commercial/extreme, very strong skin cleaning, hard protection remains fully protected.

Initial skin filter modules after mask validation:

- `SkinSmoothFilter` for weak skin surface smoothing.
- `BlemishReduceFilter` for acne, blemishes, and small uneven spots.
- `ToneEvenFilter` for skin-only tone evening.
- `TextureRestoreFilter` to restore part of original skin texture and avoid plastic skin.

Compositing model:

```text
RetouchedSkin = ApplySkinFilter(OriginalImage, RetouchAllowMask)

FinalImage =
    RetouchedSkin * FinalRetouchMask
  + OriginalImage * HardProtectMask
  + SoftRetouchedImage * SoftProtectMask

TextureRestore:
FinalImage = FinalImage + OriginalTexture * TextureRestoreAmount
```

Implementation order:

1. Prepare the test image set.
2. Mask engine scaffolding.
3. Face detection.
4. Face landmark detection.
5. Face parsing.
6. Debug mask exporter.
7. Skin, eye, eyebrow, lip, mouth, nose, hair, beard, glasses, hard-protect, soft-protect, and retouch-allow masks.
8. Nostril detector.
9. Mask quality validator.
10. Weak `SkinSmooth` and `BlemishReduce` through `RetouchAllowMask`.
11. Texture restoration over only the repaired skin area.
12. Stage `1-10` preset mapping.

UI direction:

- Provide stage buttons from `1` to `10`.
- Group stages as `Mild / Natural`, `Studio`, `Beauty`, and `Extreme / Sample`.
- Keep independent sliders for skin texture keep, blemish removal, skin tone evening, and wrinkle softening.
- Nostril protection is always on. It is not a user-facing off switch.
- Add a mask debug view option for development and later diagnostics.

## Skin Texture Restoration Reference

The Photoshop skin-texture reference subtitle in `C:\Users\beint\source\repos\유튜브_자막다운\subs\01 - Create Highly Realistic SKIN TEXTURE In Photoshop! [FREE Download] [so-ZjeE2MuA].en-orig.vtt` should be treated as a texture restoration reference, not a blemish detection method.

Useful ideas for PhotoRetouch:

- A neutral `50% gray` texture layer can be blended with `Soft Light` or `Overlay`; the neutral gray disappears and only the texture contrast remains.
- `Soft Light` is the safer default for ID photo work; `Overlay` is stronger and should be treated as an aggressive option.
- Texture strength should be subtle. The reference suggests a relief range around 5-10, with about 7 as a practical middle value.
- Generated texture can be tile-sized, such as `128 x 128`, but the app should avoid obvious repeated patterns on real portrait previews.
- Texture should follow the skin mask only. It must not apply to eyes, eyebrows, eyelashes, lips, hair, clothing, or background.
- The engine should remove blemishes first, then restore or preserve fine skin texture over the repaired area. This prevents the repaired patch from looking plastic.

Engine interpretation:

1. Build a soft skin color mask.
2. Detect defects with high-pass difference from the low-frequency skin tone field.
3. Repair the defect with nearby clean skin color.
4. Reintroduce subtle fine texture inside the repair mask, preferably from nearby original skin detail first, procedural 50% gray texture only as a fallback.

## Photoshop Smoothing Reference

The subtitle `C:\Users\beint\source\repos\유튜브_자막다운\subs\NA - Photoshop Fast & Easy Series： Best Way to Realistically SMOOTH SKIN and Remove Blemishes! [utcs8Pdgt2c].en-orig.vtt` describes a practical smoothing workflow that maps well to the app's engine design.

Useful ideas:

- The method keeps skin texture while smoothing acne and blemishes.
- It duplicates the photo, inverts the copy, uses a contrast blend mode such as `Vivid Light`, then applies `High Pass`.
- The high-pass radius controls how much imperfection is hidden. The example uses around `40 px` for that image.
- A later `Gaussian Blur` softens the high-pass effect and brings back a controlled amount of texture. The example uses around `3 px`.
- The smoothed layer is hidden behind a black mask, then revealed only on skin with a soft brush.
- Eyes, hair, and other sharp edge areas are restored/protected.
- Remaining larger blemishes are handled separately with a remove/healing tool after the smoothing pass.

Engine interpretation for PhotoRetouch:

1. Generate a smoothing candidate layer from an inverted/high-pass-like detail separation.
2. Blur the high-pass/detail layer enough to suppress blemishes while retaining fine texture.
3. Reveal that candidate only through the soft skin color mask.
4. Protect eyes, eyebrows, eyelashes, lips, hair, clothing, and background before applying it.
5. Treat strong remaining spots as a separate healing/removal pass, not as part of global smoothing.

## Customization And Performance Policy

The app should keep photographer-facing controls flexible even if the source code becomes larger.

- Keep per-tool and per-sub-control values independent during development.
- Design later setting-copy workflows for applying one photo's settings to selected photos.
- Design later batch apply/export workflows separately from interactive preview.
- Do not make multi-select automatically process all selected photos during preview.
- Prefer performance fixes in this order: screen-sized effect preview sources, throttled live preview, caching, engine separation, native CPU, optional GPU.
- Final output should use the original image and full adjustment model, not the reduced preview bitmap.

## Engine Roadmap

### Stage 1: Stabilize C# CPU Engine

Keep the current engine but improve structure.

- UI controls are separated from preview engine rendering through `PreviewAdjustment` and `IPreviewEngine`.
- Build one adjustment request object from the current UI state.
- Avoid repeated full-image recalculation.
- Keep reduced preview size support.
- Create screen-sized effect preview sources before running tool effects.
- Do not let individual tool engines independently decide to process full original images for interactive preview.
- Keep one render in progress at a time.

### Stage 2: Add Engine Interface

Completed. The UI now talks to a common engine contract so it does not care which engine is used.

Suggested shape:

```csharp
public interface IPreviewEngine
{
    BitmapSource Render(BitmapSource source, PreviewAdjustment adjustment);
}
```

Suggested implementations:

- `CSharpPreviewEngine` - current implementation
- `NativeCpuPreviewEngine` later
- `GpuPreviewEngine` later

Settings labels:

- Stable mode - C# CPU engine
- Fast mode - Native CPU engine
- Accelerated mode - GPU engine

### Stage 3: Native CPU Engine

Move pixel loops to a C++ x64 DLL.

Reasons:

- Keeps GPU dependency out of the default path.
- More predictable than GPU for mixed user hardware.
- Fits the 64-bit-only design.
- Allows future SIMD and multithreaded optimization.

The first native target should be tone and curve processing using LUTs.

### Stage 4: GPU Engine

GPU should be optional, not default.

Reasons:

- GPU drivers and devices vary a lot.
- Older or low-end GPUs may be unstable or slower.
- It is useful as a future acceleration option for capable machines.

## Native Engine Direction

The native engine should receive BGRA32 pixels and LUT or adjustment values, then return BGRA32 pixels.

Initial C++ function shape:

```cpp
extern "C" __declspec(dllexport)
void ApplyPreviewBgra32(
    const unsigned char* source,
    unsigned char* destination,
    int width,
    int height,
    int stride,
    const PreviewAdjustmentNative* adjustment);
```

Start with preview rendering only. Do not move final export or file IO into native code yet.

## Important Decisions

- Do not auto-sync the working folder during editing.
- Use manual refresh for newly added files.
- Do not lock source image files while the app is running.
- Keep preview and final output paths conceptually separate.
- Keep CPU as the safe default.
