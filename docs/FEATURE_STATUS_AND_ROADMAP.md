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
- Full save/export options UI.
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

### Mask-First Direction

Skin retouching is now mask-first. Do not continue strengthening blemish, acne, mole, age-spot, or smoothing filters until mask debug output is reliable.

Snapshot mask policy:

- Analyze a newly loaded photo once and save a `FaceSnapshotMaskSet`.
- Reuse the snapshot when Stage `1-10`, `SkinSmooth`, `BlemishReduce`, `ToneEven`, `TextureRestore`, or before/after view changes.
- Rebuild the snapshot only when the image changes, face re-analysis is requested, crop/rotation changes, manual mask edits are saved, or face position changes significantly.

Required mask engine stages:

- Prepare the required test image set first.
- `FaceDetection` for face boxes.
- `FaceLandmark` for eye, nose, mouth, eyebrow, and jawline coordinates.
- `FaceParsing` for pixel-level regions such as skin, eye, eyebrow, nose, lip, mouth, hair, glasses, beard, and mustache.
- `PartMaskBuilder` modules for skin, eye, eyebrow, mouth, nose, nostril, hair, beard, glasses, hard protect, soft protect, and final retouch allow masks.
- `NostrilDetector` as a separate module. Nostrils are protected detail, not skin.
- `MaskQualityValidator` to expand protection and reduce retouch strength when detection is uncertain.
- `DebugMaskExporter` before filter work continues.

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

Required test image set:

- Age and gender coverage from 20s through 60s, men and women.
- Skin conditions: acne, blemishes, freckles, redness, nasolabial folds, forehead wrinkles, neck wrinkles, pores, beard shadow.
- Mask failure cases: large nostrils, strong under-nose shadow, thick eyebrows, clear eyelashes, strong lip edge, glasses, beard, hair touching the face, smiling photos, open mouth photos, near-side faces, strong lighting, and strong shadows.

### Current Controls

- Blemish removal - first preview pass connected for light skin blemish softening
- Acne removal - first preview pass connected for small red/dark spot softening
- Mole / age spot removal - first preview pass connected for dark and brown spot softening
- Skin texture cleanup - first preview pass connected with edge-protected smoothing
- Pore cleanup - first preview pass connected for small texture cleanup
- Skin tone correction - first preview pass connected for mild local tone evening
- Skin correction range - narrows or widens the skin-tone mask used by cleanup passes
- Skin texture preservation - protects pores, natural texture, and facial feature edges
- Manual wide skin-tone eyedropper - samples a broad average skin reference from the preview

### Implemented

- Mask scaffolding is added: `FaceAnalysisResult`, `FaceMaskSet`, `FaceSnapshotMaskSet`, `MaskPlane`, `MaskQualityReport`, `IPortraitMaskEngine`, `PortraitMaskResult`, and `SnapshotMaskBuilder`.
- `StandardMaskWarpEngine` is wired for the current verification pass. It loads or generates a `StandardMaskSet`, uses `IFaceAnalyzer` to create `MaskWarpInput`, applies affine scale/rotate/translate warping, and builds `HardProtectMask`, `SoftProtectMask`, and `RetouchAllowMask`.
- FaceBox and landmark automation is added: `IFaceAnalyzer`, `FaceAnalyzerResult`, `TemporaryFaceAnalyzer`, and `OpenCvFaceAnalyzer`.
- `OpenCvFaceAnalyzer` is now the default analyzer. It uses OpenCV YuNet (`Assets/AiModels/face_detection_yunet_2023mar.onnx`) for real FaceBox detection plus eye, nose, and mouth anchors. `ChinPoint` is still estimated from the detected FaceBox.
- FaceParsing scaffolding is added: `IFaceParsingDetector`, `FaceParsingInput`, `ParsingMaskSet`, `ParsingLabelMapper`, and `TemporaryFaceParsingDetector`.
- `StandardMaskWarpEngine` now merges warped standard masks with parsing masks. Eye, eyebrow, lip, inner mouth, hair, beard, mustache, and glasses parsing outputs are treated as protection first.
- `DebugMaskExporter.SaveAll(...)` now exports parsing and merged-mask debug files such as `debug_parsing_labels.png`, `debug_merged_eye_mask.png`, `debug_hard_protect_after_parsing.png`, and `debug_retouch_allow_after_parsing.png`.
- `StandardMasks/` is reserved for optional PNG resources. Missing masks are generated by `StandardMaskLoader` and recorded as debug warnings.
- `SnapshotMaskCacheKey` is added with `ImageId`, image size, `FaceBox`, `FaceAngle`, `CropVersion`, and `MaskVersion`. Stage values are intentionally excluded.
- Snapshot cache keys now use FaceAnalyzer `FaceBox` and `FaceAngle`; Stage values remain excluded.
- `MaskQualityValidator` is added. It calculates overall, face, landmark, parsing, skin, eye, eyebrow, lip, nostril, hair, hard-protect, and retouch-allow quality scores, plus warnings, fatal errors, strong-retouch safety, and max allowed stage.
- `MaskQualityReport` now exposes detailed quality scores, `MaxAllowedStage`, `IsSafeForStrongRetouch`, `DebugWarnings`, and `FatalErrors`; strong stages are limited when mask quality is weak.
- Quality debug exports include `debug_quality_face.png`, `debug_quality_landmark.png`, `debug_quality_skin_mask.png`, `debug_quality_eye_mask.png`, `debug_quality_lip_mask.png`, `debug_quality_nostril_mask.png`, `debug_quality_hair_mask.png`, `debug_quality_retouch_allow.png`, `debug_stage_gate_overlay.png`, `debug_final_safe_mask.png`, and `debug_mask_quality_report.txt`.
- `StagePresetMapper`, `RetouchOptions`, `RetouchProcessReport`, and `RetouchStageProcessor` are added for the first mask-based retouch pipeline pass.
- Skin smoothing is upgraded from plain blur to a mask-aware edge-preserving smooth base plus detail-layer texture restore.
- The retouch composition now saves and verifies separate RetouchAllow applied, SoftProtect applied, and HardProtect restored debug stages.
- `BlemishReduceFilter` is added as the first mask-based local blemish pass. It finds small dark, red, and color-offset candidates inside `RetouchAllowMask`, weakly samples surrounding skin color, clips against `HardProtectMask`, and reuses the candidate analysis by Snapshot cache key while Stage changes adjust strength.
- Blemish debug exports include `debug_blemish_search_mask.png`, `debug_blemish_candidates.png`, `debug_blemish_components.png`, `debug_blemish_mask.png`, `debug_blemish_corrected.png`, `debug_blemish_before_after.png`, and `debug_final_after_blemish_stage_*.png`.
- `WrinkleSoftReduceFilter` is added as the first mask-based wrinkle-softening pass. It finds linear dark candidates through `SoftProtectMask` plus weak `RetouchAllowMask`, separates under-eye, glabella, forehead, nasolabial, mouth-corner, neck, and nose-shadow masks, clips against `HardProtectMask`, preserves facial structure, and reuses wrinkle analysis by Snapshot cache key while Stage and toolset values adjust strength.
- Wrinkle debug exports include `debug_wrinkle_search_mask.png`, `debug_wrinkle_candidates.png`, `debug_wrinkle_components.png`, `debug_wrinkle_under_eye_mask.png`, `debug_wrinkle_glabella_mask.png`, `debug_wrinkle_forehead_mask.png`, `debug_wrinkle_nasolabial_mask.png`, `debug_wrinkle_mouth_corner_mask.png`, `debug_wrinkle_neck_mask.png`, `debug_wrinkle_nose_shadow_mask.png`, `debug_wrinkle_combined_mask.png`, `debug_wrinkle_applied_mask.png`, `debug_wrinkle_corrected.png`, `debug_wrinkle_before_after.png`, and `debug_final_after_wrinkle_stage_*.png`.
- `TextureRestoreFilter` is upgraded as the final texture restoration pass. It extracts original detail, builds a texture restore mask, reduces restoration over blemish and wrinkle repair masks, applies weak SoftProtect restoration, guards against plastic-looking skin, and reuses texture analysis by Snapshot cache key while Stage/toolset values adjust strength.
- Texture debug exports include `debug_texture_blur_original.png`, `debug_texture_detail_layer.png`, `debug_texture_restore_mask.png`, `debug_texture_restore_strength_map.png`, `debug_texture_restored_image.png`, `debug_texture_before_after.png`, `debug_plastic_skin_risk_map.png`, and `debug_final_after_texture_stage_*.png`.
- `HardProtectFinalRestoreFilter` is added as the last retouch step so HardProtect pixels are restored from the original after all filter stages.
- `PipelineDebugReport` and pipeline debug images are added for ORDER_16 integration review. The current filter order is SkinSmooth, BlemishReduce, WrinkleSoftReduce, ToneEven, TextureRestore, and HardProtectFinalRestore.
- `RetouchToolset` and `AppliedRetouchOptions` are added for ORDER_17. The top Stage slider provides defaults, and existing skin/tone/texture sliders plus the new wrinkle section can override the stage defaults without rebuilding SnapshotMask.
- The wrinkle section adds 전체, 눈밑, 미간, 이마, 팔자, 입가, 목, and 코그림자 controls for the first wrinkle toolset pass.
- `RetouchBindingReport` is added for ORDER_18. Stage and slider changes now expose whether the pipeline used cache or rebuilt the snapshot, and the top toolbar shows the last binding event.
- A minimal `재분석` action rebuilds SnapshotMask explicitly; Stage and Slider changes continue to use `GetOrCreate`.
- Stage `1-10` is tuned for ORDER_19. Stage `1-3` keeps a natural look, Stage `4-6` targets studio/profile cleanup, Stage `7-8` is stronger beauty retouch, and Stage `9-10` is strong test/sample retouch with HardProtect still restored.
- Retouch debug export now writes `debug_stage_1_5_10_compare.png`, `debug_stage_preset_values.json`, `debug_stage_gate_report.json`, and Stage `1/5/10` HardProtect diff previews.
- ORDER_20 adds a Debug Mask selector for Final, Skin, HardProtect, SoftProtect, RetouchAllow, Eye, Eyebrow, Lip, InnerMouth, Nostril, Hair, Beard, Glasses, Blemish, Wrinkle, TextureRestore, PlasticRisk, and HardProtectDiff overlays. Selection changes reuse SnapshotMask or the last retouch output and do not rerun retouch filters.
- `HardProtectTestSetRunner` is added for ORDER_21. It runs Stage `1`, `5`, and `10`, exports HardProtect/part masks, before/after HardProtect diff images, and JSON reports for preservation testing.
- ORDER_22 adds a local-only portrait test asset layout, a `portrait_test_cases.json` manifest, `PortraitTestCaseCatalog`, and minimum test slots for nostrils, eyebrows, lips, glasses, beard, glabella wrinkles, nasolabial folds, blemishes, tone issues, and hair-on-face cases.
- `StageCompareReportRunner` is added for ORDER_23. It runs Stage `1`, `5`, and `10` from one SnapshotMask, saves comparison sheets, HardProtect diff images, and JSON/Markdown reports.
- `SnapshotMaskDiskCache` is added for ORDER_24. Snapshot masks are persisted under local AppData as JSON metadata plus grayscale mask PNGs, and `SnapshotMaskBuilder` now checks memory cache, disk cache, then rebuild.
- ORDER_25 core manual mask override is added. `ManualMaskOverride`, brush modes, brush painting engine, and final mask composition exist, and the preview pipeline applies manual override layers over SnapshotMask before processing.
- ORDER_26 core face manual adjustment override is added. Existing face work area edits are persisted as `FaceManualAdjustOverride`, loaded per photo, and included in SnapshotMask cache versioning.
- ORDER_27 core export service is added. `ExportService` saves JPG/PNG without overwriting the source, auto-renames duplicates, uses highest JPG quality by default, and can write sidecar export reports.
- ORDER_28 core preset service is added. `RetouchPresetService` stores retouch toolset values only, separates default/user presets, and never stores photo-specific masks or paths.
- ORDER_29 core batch processing service is added. Batch runs sequentially, applies shared Preset/Toolset values, creates per-photo SnapshotMasks, applies per-photo StageGate, saves through `ExportService`, and writes a batch report.
- ORDER_30 core high-resolution policy is added. `HighResolutionProcessingPolicy` separates preview downscale decisions from original-resolution export, and batch clears transient per-photo preview caches.
- ORDER_31 first cache/memory cleanup pass is added. Blemish, Wrinkle, and TextureRestore analysis caches are bounded, processor cache status/clear APIs exist, and inactive photos release transient preview/Snapshot memory on selection changes.
- ORDER_32 first UI product polish pass is added. The top bar now uses `K Retouch Pro`, removes the temporary review badge and unused reset button, hides developer status text until debug/retouch preview is active, and shows selected photo size plus zoom percent.
- ORDER_33 first session persistence pass is added. Open photo paths, selected photo path, and zoom percent are saved on close and restored on startup, skipping missing files.
- ORDER_34 first packaging pass is added. `build/Publish-V1.ps1` creates a win-x64 publish folder and ZIP package while generated output stays out of git.
- ORDER_35 first V1 final review is recorded. Build and light package verification pass, V1 scope is bounded, and remaining risks are listed for real portrait QA and UI completion.
- `ORDER_SEQUENCE_AUDIT_2026-06-09.md` records that orders `00-30` are accounted for.
- `ORDER_28_PRESET_SAVE_LOAD.md` is recorded as queued/planned. It must wait until export/save quality options are complete.
- `NostrilDetector` is added. It creates a lower-nose ROI, finds dark candidate pixels, runs connected component analysis, scores nostril candidates, merges them with the warped standard nostril fallback, and forces the final mask into HardProtect.
- Nostril debug exports include `debug_nose_lower_roi.png`, `debug_nostril_dark_candidates.png`, `debug_nostril_components.png`, `debug_warped_standard_nostril.png`, `debug_final_nostril_mask.png`, `debug_hard_protect_with_nostril.png`, and `debug_final_overlay_with_nostril.png`.
- The top toolbar Stage `1-10` slider now drives the first-pass retouch pipeline. Snapshot masks are reused; only `RetouchStageProcessor` reruns when Stage changes.
- `RetouchDebugExporter` saves debug images such as `debug_smooth_base.png`, `debug_detail_layer.png`, `debug_texture_restored.png`, `debug_retouch_allow_applied.png`, `debug_soft_protect_applied.png`, `debug_hard_protect_restored.png`, and `debug_retouch_report.txt`.
- `DummySnapshotMaskEngine` remains available as the first structural verification engine.
- `HeuristicPortraitMaskEngine` remains available as a later non-AI placeholder path until real detection, landmark, and parsing models are connected.
- The top toolbar has a temporary Stage `1-10` slider and pipeline retouch preview to verify that Stage changes reuse the cached `FaceSnapshotMaskSet`.
- `DebugMaskExporter.SaveAll(...)` exports the required mask PNG set for visual inspection.
- `PhotoItem` can hold a `SnapshotMaskSet` cache and invalidates it when the source image or face work area changes.
- `skin_smooth` is captured in `PreviewAdjustment`.
- The C# preview engine applies a mild screen-sized texture smoothing pass.
- Strong detail/edge differences are protected so facial edges, eyes, hair, and clothing do not blur as aggressively.
- `pore_clean` is captured in `PreviewAdjustment`.
- The C# preview engine applies a narrower small-texture cleanup pass for pore-like detail.
- `tone_even` is captured in `PreviewAdjustment`.
- The C# preview engine applies a mild local tone evening pass with edge protection.
- `blemish_remove` is captured in `PreviewAdjustment`.
- The C# preview engine softly reduces lighter blemishes without acting as the main mole/age-spot tool.
- `acne_remove` is captured in `PreviewAdjustment`.
- The C# preview engine softens small red or dark acne-like spots toward the local surrounding tone.
- `mole_age_spot_remove` is captured in `PreviewAdjustment`.
- The C# preview engine treats dark moles and brown age spots as a separate pass from general blemish removal.
- `skin_mask_range` and `skin_texture_protect` are captured in `PreviewAdjustment`.
- Manual skin reference color is captured per photo and passed through `PreviewAdjustment`.
- Skin cleanup passes use face-area targeting, automatic or manual average skin-tone matching, local edge protection, and feathered protection around eyes, nose, and mouth.

### Needs Work

- MaskEngine scaffolding.
- Actual skin mask and facial part detection.
- Debug mask export for every required mask.
- Dedicated `ToneEvenFilter` with candidate masks, process report, and slider/toolset binding. The current code has only a simple mask-aware tone-even processor stage.
- ORDER_19 visual review on real portrait test images.
- Nostril detector with fallback lower-nose protection.
- HardProtect, SoftProtect, and RetouchAllow mask composition.
- Mask quality validation and debug warnings.
- Mask debug view in the UI.
- ToneEven dedicated candidate overlays after ORDER_13 creates a real `ToneEvenFilter` output mask.
- ORDER_21 real-image run against the required HardProtect failure-case set.
- Assign local original files to the ORDER_22 manifest without committing sensitive portraits.
- Run ORDER_23 reports after local original files are assigned.
- ORDER_24 real reload test to confirm disk cache hits after app restart.
- Separate AnalysisCache persistence for Blemish, Wrinkle, ToneEven, and TextureRestore candidates.
- ORDER_25 UI brush cursor, mouse stroke capture, reset button, override persistence, and debug entries for manual masks.
- ORDER_26 eye/nose/mouth/chin draggable handles and face-adjust debug overlays.
- ORDER_27 full export options UI and Save As flow.
- ORDER_28 preset select/save/load/delete UI binding.
- ORDER_29 batch file list/progress/cancel UI.
- ORDER_30 full per-filter timing integration in the live pipeline.
- ORDER_31 disk cache size limits, manual cache clear command, and long-session stress testing.
- ORDER_32 export/preset/batch UI polish, debug control grouping, and full visual QA.
- ORDER_33 full project/session persistence, last active tool section, preset/export option persistence, and missing-file UI.
- ORDER_34 real installer, code signing, version stamping, release notes, and packaging QA on another PC.
- ORDER_35 real portrait visual QA, HardProtect diff review, and selection of the next UI completion panel.
- Stage `1-10` preset mapping with hard protection always preserved.
- Brush/manual target mode for precise blemish removal.
- Texture-preserving smoothing.
- Skin-region-only local tone evening.
- Portraiture-style multi-scale skin controls: separate fine texture, medium blemish, large blotch/tone handling, plus conservative fill-light support.
- Skin texture restoration after defect removal. Use a subtle Soft-Light-style texture pass so repaired spots do not become flat or plastic.
- Photoshop-style realistic smoothing layer: inverted/detail-separated smoothing candidate, high-pass radius, secondary blur radius, then reveal only through a soft skin mask.
- Strength behavior tuned for ID photos.

Filter implementation order after mask validation:

1. Weak `SkinSmooth` through `RetouchAllowMask`.
2. Conservative `BlemishReduce`.
3. Skin-only `ToneEven`.
4. Texture restoration over repaired areas.
5. Stage preset mapping from `1` to `10`, with `HardProtectMask` always protected at every stage.

## Face Shape

### Current Controls

- Oval face correction - first preview warp pass connected to the editable face work area
- Left-right balance - first preview warp pass connected to the editable face work area
- Cheekbone soften - first preview warp pass connected to the editable face work area
- Jawline clarity - first localized edge-clarity pass connected to the editable face work area
- Chin length - first vertical chin-tip warp pass connected to the editable face work area
- Chin width - first horizontal chin-tip warp pass connected to the editable face work area
- Face left-right balance - first asymmetric side-width warp pass connected to the editable face work area
- Eye height balance - first paired eye-band height warp under face left-right balance
- Brow height balance - first paired brow-band height warp under face left-right balance
- Nose bend center correction - first central nose-strip warp under face left-right balance
- Double chin soften - first lower-center shadow/texture soften pass connected to the editable face work area
- Neck and jaw boundary - first narrow jaw-neck edge refinement pass connected to the editable face work area

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
- `jaw_balance` is labeled as face left-right balance and applies a signed asymmetric side-width warp inside the face work area.
- `eye_height_balance` applies a signed paired eye-band vertical warp inside the face work area.
- `brow_height_balance` applies a signed paired brow-band vertical warp inside the face work area.
- `nose_center_balance` applies a signed central nose-strip horizontal warp inside the face work area.
- `double_chin` applies a localized lower-center shadow and texture softening pass inside the face work area.
- `neck_jaw_edge` applies a narrow jaw-neck edge clarity and mild shadow cleanup pass inside the face work area.

### Needs Work

- Persist editable face work area across app restarts.
- Individual face keypoint handles for mask alignment.
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
- Solid color amount - first full-preview tint pass connected, pending subject segmentation
- Edge blending

### Implemented

- `background_color_amount` is captured in `PreviewAdjustment`.
- The C# preview engine applies a low-strength full-preview solid color tint as a temporary judging aid.
- The default solid color amount is `0` so normal photo judgment is not affected until the user raises the slider.

### Needs Work

- File import for background images.
- Persist imported background thumbnails.
- Horizontal no-scrollbar gallery.
- Subject segmentation.
- Replace the temporary full-preview tint with a subject-segmented solid color background renderer.
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
