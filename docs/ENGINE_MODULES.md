# Engine Modules

Last updated: 2026-06-10

This document lists the current source-code modules at a practical level.

## Program Policy

PhotoRetouch is not an automatic AI face-correction program. Modules may detect landmarks, masks, ratios, and confidence values, but they must not apply visible beautification, smoothing, reshaping, resizing, moving, or asymmetry correction by themselves.

Visible edits require user action through a specific slider or active tool, and only the related local region should be calculated and affected. Tabs should not precompute or apply all base corrections on open.

FaceBox is only an initial detection container. It may be used for detection, rough normalization, search limits, fallback, cache/debug metadata, and landmark sanity checks, but it must not define final skin, jawline, cheek, forehead, under-eye, lip, beard, hair, tone, or visible correction masks.

K-AnchorMesh final mask hierarchy: `FaceBox` is the detection container only, `K-AnchorMesh` provides landmark and anchor topology, `ComponentROI` is a bounded search area around anchors, `CandidateMask` comes from color/edge/texture/semantic evidence, `FinalMask` is fitted/clipped/feathered/confidence-checked, `ProtectionMask` is the hard exclusion area, and `CorrectionMask` is `FinalMask - ProtectionMask`.

## UI Entry

Main files:

- `MainWindow.xaml`
- `MainWindow.xaml.cs`

Responsibilities:

- Photo list.
- Preview area.
- Right retouch panel.
- Slider event handling.
- Preview render orchestration.
- AUTO MASK preview.
- ShapeBalance and SkinRetouch trigger flow.

## Photo Model

Main file:

- `Models/PhotoItem.cs`

Responsibilities:

- Source path and base image.
- Current display image.
- Thumbnail.
- Per-photo retouch state.
- Snapshot mask cache reference.
- ShapeBalance cache.
- AUTO MASK preview cache.
- Preview zoom and pan state.

Important properties:

- `BaseImage`
- `Image`
- `RetouchState`
- `SnapshotMaskSet`
- `CachedShapeBalanceBundle`
- `AverageFaceColorMaskPreviewCache`

## Preview Engine

Main files:

- `Tools/PhotoAdjustment/IPreviewEngine.cs`
- `Tools/PhotoAdjustment/CSharpPreviewEngine.cs`
- `Tools/PhotoAdjustment/PreviewEngineFactory.cs`
- `Tools/PhotoAdjustment/PreviewSourceFactory.cs`
- `Models/PreviewRenderTier.cs`

Current state:

- C# preview engine is the active path.
- Native CPU and GPU paths are future work.
- Preview source should be display-sized for interaction.
- Export render remains conceptually original-resolution.

## Snapshot Mask

Main files:

- `Tools/Masking/SnapshotMaskBuilder.cs`
- `Tools/Masking/SnapshotMaskDiskCache.cs`
- `Tools/Masking/FaceSnapshotMaskSet.cs`
- `Tools/Masking/FaceMaskSet.cs`
- `Tools/Masking/StandardMaskWarpEngine.cs`
- `Tools/Masking/NoFaceParsingDetector.cs`

Current state:

- Snapshot masks are per-photo.
- Snapshot mask rebuild is for image/source/face-work-area/manual-mask/reanalyze changes.
- Stage and slider changes should not rebuild SnapshotMask.
- Real face parsing AI is not connected.
- `NoFaceParsingDetector` is a fallback scaffold.
- `MaskPlane` values are internal soft mask weights, not exported channel masks. Do not generate separate RGBA component cutout PNGs for lip, eyebrow, skin, beard, hair, or other detected parts.

## AUTO MASK

Main files:

- `Tools/Masking/AverageFaceColorMaskBuilder.cs`
- `Tools/Masking/DebugMaskExporter.cs`
- `Models/PhotoItem.cs`
- `MainWindow.xaml.cs`

Current state:

- Skin-color/range based.
- Not AI.
- Default range control is `skin_mask_range = 75`.
- Fills small enclosed mask holes.
- Reapplies feature block mask after filling.
- Uses per-photo cache for same photo and same range.

## Skin Retouch Pipeline

Main files:

- `Tools/Masking/RetouchStageProcessor.cs`
- `Tools/Masking/RetouchOptions.cs`
- `Tools/Masking/RetouchToolset.cs`
- `Tools/Masking/RetouchProcessReport.cs`
- `Tools/Masking/BlemishReduceFilter.cs`
- `Tools/Masking/WrinkleSoftReduceFilter.cs`
- `Tools/Masking/TextureRestoreFilter.cs`
- `Tools/Masking/HardProtectFinalRestoreFilter.cs`

Current state:

- First-pass pipeline exists.
- Final quality is not finished.
- HardProtect should restore original protected pixels at the end.
- Stronger filter tuning should wait until AUTO MASK is reliable.
- Skin smoothing is manual and slider-triggered. It must not run just because a tab opens.
- Skin smoothing may use face skin masks, but it must never become whole-face global blur.
- Skin smoothing must preserve pores, fine grain, age-appropriate detail, lighting, and protected features.
- Blemish, wrinkle, dark-circle, shine, beard, makeup, and texture restoration behavior should remain separated instead of being hidden inside one broad smoothing pass.
- Skin color/body color balance is also manual-control only. It must not whiten automatically, exact-match every skin region, or run on tab open.
- Tone matching should use clean reference skin, preserve makeup and lighting direction, and harmonize face/neck/body only partially.

## ShapeBalance

Main files:

- `Tools/Shape/ShapeBalanceProcessor.cs`
- `Tools/Shape/ShapeBalanceMap.cs`
- `Tools/Shape/ShapeBalanceMapBuilder.cs`
- `Tools/Shape/ShapeBalanceModels.cs`
- `Tools/Shape/ShapeBalanceToolset.cs`
- `Tools/Shape/BalancedMaskQualityValidator.cs`

Current state:

- ShapeBalance is a geometry module, not a skin filter.
- It can run before SkinRetouch when shape controls are active.
- It should move image and masks together.
- SkinRetouch should then use balanced image and balanced masks.
- AnchorMesh pose data can be used as a guide for yaw-like and pitch-like balance, but it must not become an automatic face beautifier.

## K-AnchorMesh / K-AnchorWarp

Main files:

- `Core/AnchorMesh/KAnchorMeshEngine.cs`
- `Core/AnchorMesh/Alignment/*`
- `Core/AnchorMesh/Measure/*`
- `Core/AnchorMesh/Pose/*`
- `Core/AnchorMesh/Morph/*`
- `Core/AnchorMesh/Warp/*`
- `Core/AnchorMesh/Templates/*`
- `Core/AnchorMesh/Snap/*`
- `Core/Vision/*`
- `Core/Masks/*`
- `Tools/DebugMeshPreview/*`

Purpose:

- Provide 2.5D face-structure reference data.
- Estimate roll/yaw-like/pitch-like face direction.
- Build reference points for eyes, brows, nose, mouth, chin, jawline, and face outline.
- Measure practical face ratios for eyes, nose, mouth, lips, brows, philtrum, chin, whole-face proportion, and face-shape safety guidance as described in `docs/FACE_RATIO_GUIDES.md`.
- Provide handle groups, falloff regions, locked points, and weak morph groups for future user-controlled shape tools.

Current state:

- Engine scaffolding is present.
- It is not a finished pixel liquify solver.
- It is not a panel/tab UI feature yet.
- It should be treated as a measurement, guide, and handle-generation layer.
- Face shape analysis is for contour understanding, confidence, occlusion handling, protection masks, and manual tool safe limits; it must not reshape by itself.
- Existing ShapeBalance remains the active geometry application path.

Rules:

- Do not use K-AnchorMesh as automatic beautification.
- Do not force symmetry or oval face automatically.
- Do not force ratio-guide values automatically.
- Component masks must be positioned from the best available snapped landmark or AnchorMesh feature point; face-box percentage tables are fallback ROI and sanity guides only.
- FaceBox must not drive editable regions after landmarks and fitted masks are available.
- No visible correction can be driven by FaceBox; all visible correction must be driven by anchored local masks and user controls.
- Eyebrow masks must not be a simple `browHead -> browTail` line segment. Brow head/arch/tail points are anchors for a local ROI; the final `EyebrowMask` should be fitted from dark hair texture, eyebrow directionality, local connectivity, and brow-color clustering inside that ROI.
- Eyebrow analysis is separated into `EyebrowAnalyzer`. It is not a correction module: it builds left/right eyebrow candidates, area masks, protection masks, confidence, failure reason, eye-to-brow distance, length, thickness, slope, arch, color, texture, and connectedness scores.
- Eyebrow search must be constrained by the orbital structure above each eye. Build the brow search ROI from eye center, pupil/iris center when available, upper eyelid position, eye width/height, and a soft upper orbital arc. The orbital guide decides where to search; pixel evidence decides the final brow mask.
- Eyebrow 3D guide geometry should be a 30-point closed free polygon around the brow hair bundle. It has upper and lower contour points, variable head/body/tail thickness, and represents the brow mass envelope rather than a centerline.
- Eyebrow ROI placement must be constrained by eye-to-brow distance and side-offset ratio guards. Do not fix eyebrow masks by absolute coordinates.
- Eyebrow mask generation should be evidence-only: search inside the brow ROI for the real eyebrow hair bundle, score the found cluster endpoints, free angle, free length, variable thickness, curve peak, density, and continuity, then use the confirmed hair-pixel evidence as the mask. Do not draw a generated brow cover, do not use a round brush, and do not create an anchor-only fallback brow. The brow may be sparse, broken, uneven, or absent; preserve gaps when pixel evidence is weak.
- Nose masks must represent bridge, tip, wings, and base skin as area-based surface masks. `NoseStructureGuide` lines are geometry guides only; nostril dark regions are separate hard protection masks and must not become the whole `NoseMask`.
- Lip and inner-mouth masks must respect nose-to-mouth proximity ratios and be clipped away from nostrils and nose base before final protection masks are produced.
- Mouth topology must be mouth-corner based. Closed mouth is one horizontal almond loop; open mouth is upper and lower almond surface loops sharing the same left/right mouth-corner anchors with an `InnerMouthProtectionLoop` between them. Do not use two independent circles or a mouth-center radius mask.
- Lip surface fill must use the lip loops as the boundary. Build upper and lower lip surface loops from the outer lip and inner lip topology, soft-fill them as surface areas, use color only as candidate evidence, and subtract a softened inner-mouth protection mask. A good mask covers the real lip surface without bleeding into skin, philtrum, chin, nostrils, teeth, or the inner mouth; it should not stop at the middle because a color threshold was too strict. Lip and inner-mouth masks should be internal no-edit exclusions, not a broad visible hard-protect cover over the lips.
- Lip directional/phase texture evidence is guide-centered, not standalone. A lip 3D guide, guide centerline, or guide search mask must first localize the expected lip line/curve area; `LipGuideTextureEvidenceAnalyzer` then expands that guide into two long upper/lower lip surface planes so evidence can run to the lip ends. The bold search can include a wider band and weak vermilion-side support, while still subtracting inner-mouth protection. It can produce candidate evidence masks for cracks, dry texture, gloss breaks, and line direction, but visible correction still requires a user lip tool or slider.
- Lip and eyebrow mask rules: `LipMask`, upper/lower lip masks, inner-mouth protection, eyebrow masks, skin, beard, and hair masks are internal soft masks only. Do not create component channel PNGs or replacement RGB pixels during detection.
- K-AnchorMesh topology must connect points into explicit edge types: anchor, boundary, surface, protection, measurement, structural, and morph-control edges. Edges are used for direction, distance, width, ratio, surface-loop candidates, protection boundaries, and confidence checks.
- Topology edges are not final masks. Surface loops create candidate areas only; final masks must still be fitted from pixel evidence inside anchor-based ROIs and clipped by protection masks.
- Do not replace SkinRetouch with geometry warp.
- User controls must remain the final authority.
- Mesh movement preview should happen before pixel warp.
- Image pixels and masks must be warped together only when an explicit geometry tool is active.
- Future solver work should start with limited ROI, safe zones, and screen-sized preview.

## Hair And Flyaway Cleanup

Current state:

- Hair and flyaway cleanup are design-level policies only.
- A future flyaway filter must be user-controlled by slider or explicit cleanup tool.
- It must remove or soften only distracting isolated strands with high confidence.
- It must preserve hairline, baby hair, hairstyle, hair volume, eyelashes, eyebrows, beard, mustache, skin texture, and background.
- It must use strand masks, protection overlaps, and restoration confidence before visible cleanup.
- It must not run automatically when a tab opens.

## Beard And Shaving Shadow Cleanup

Current state:

- Beard, mustache, sideburn, long beard, stubble, and shaving-shadow cleanup are design-level policies only.
- Beard/facial hair is protected by default and is not a blemish or skin texture defect.
- Beard landmarks are not expected from face landmark models; future beard points must be virtual estimates from beard masks, hair texture, color separation, and face anchors.
- Beard decision order is: classify as hair first, split mustache/chin/jaw/cheek/neck/sideburn/long beard, estimate virtual points from `beardMask`, lower jaw/lip/neck confidence where beard overlaps, and protect by default.
- A future beard cleanup filter must be user-controlled by an explicit beard/shaving cleanup slider or tool.
- It must separate intentional beard, mustache, sideburn, long beard, stubble dots, shaving shadow, razor redness, ingrown hair, acne, pigmentation, wrinkles, lips, jawline, neck wrinkles, and under-jaw shadow before correction.
- Skin smoothing and blemish filters must not remove beard dots or blur beard texture as normal skin.
- Jawline tools must not use beard edge as true jaw contour unless confidence is high; long beard should reduce jaw/neck/under-jaw confidence.
- Jawline, lips, nostrils, neck wrinkles, clothing, background, and under-jaw shadow remain protected.

## Debug And Reports

Main files:

- `Tools/Masking/DebugMaskExporter.cs`
- `Tools/Masking/RetouchDebugExporter.cs`
- `Tools/Integration/PreviewIntegrationDebugExporter.cs`
- `Tools/Shape/ShapeBalanceDebugExporter.cs`

Current state:

- Debug masks are useful during engine development.
- Debug output should not spam Visual Studio output.
- Debug file generation should be tied to user action or explicit debug paths.

## Reference Documents

Reference material lives under:

- `docs/reference/`

Current reference:

- `docs/reference/skin_retouching_photoshop_tutorial_workflow.md`

Reference documents are not active implementation orders. They are used to compare ideas and decide future behavior.
