# Face Ratio Guides

Last updated: 2026-06-10

This document records photographer-facing face proportion guides for K-AnchorMesh, K-AnchorMeasure, ShapeBalance, and future K-AnchorWarp handles.

These values are guides, not automatic beautification targets.

The app must not force a face into these ranges. The user makes the final judgment. Use these ratios to measure, warn, build handles, and limit unsafe geometry edits.

## Program Policy

This program is not an automatic AI face-correction program.

Do not automatically beautify, reshape, resize, move, smooth, or modify the face based only on detected landmarks, ratios, or masks.

All facial structure definitions in this document are used for:

- Local mask creation.
- Region classification.
- Slider target isolation.
- Over-correction prevention.
- Protection mask generation.
- Confidence checks.
- Avoiding unintended edits.

User action is required to apply visible correction. When the user moves a specific slider or activates a specific tool, only the related local region should be calculated and affected.

FaceBox policy: FaceBox finds the face and may support rough normalization, search limits, fallback, cache keys, and debug visualization. Landmarks anchor the parts, fitted masks define editable areas, and sliders trigger correction. No visible correction should be applied directly from FaceBox.

K-AnchorMesh hierarchy: `FaceBox` is detection only; `K-AnchorMesh` is landmark and anchor topology; `ComponentROI` is search area; `CandidateMask` is rough pixel evidence; `FinalMask` is fitted, clipped, feathered, and confidence-checked; `ProtectionMask` is hard exclusion; `CorrectionMask` is `FinalMask - ProtectionMask`.

Topology edge rule: points alone are not enough. K-AnchorMesh must connect component points into anchor, boundary, surface, protection, measurement, structural, and morph-control edges. Edges guide ROI orientation, width, ratio, contour, surface-loop candidates, protection boundaries, and confidence checks. Edges are not final masks, and a topology line must never be treated as eyebrow, nose, lip, skin, or protection area by itself.

Eyebrow mask rule: `browHead`, `browArch`, and `browTail` are anchors, not the mask. Build a brow ROI around those anchors, constrain it by eye-to-brow distance and side-offset ratio bands, then confirm the final eyebrow mask from dark hair texture, eyebrow directionality, local connectivity, and brow-color clusters inside the ROI. A direct brow-head-to-brow-tail segment is only a low-confidence fallback guide.

Eyebrow analyzer rule: `EyebrowAnalyzer` owns eyebrow candidate interpretation and confidence. It calculates left/right eyebrow candidate masks, protection masks, failure reasons, eye-to-brow distance, brow length, thickness, slope angle, arch, color, texture, and connectedness scores. It does not beautify, reshape, recolor, or symmetrize eyebrows.

Eyebrow 3D guide rule: the brow is closest to a free arch-like hair bundle, not a horizontal stroke or fixed template. The guide can describe a 30-point closed free polygon around the hair-mass envelope, but it is only a guide. The final eyebrow mask must come from real hair-pixel evidence, not a generated brush cover. Brow thickness may differ between head, body, arch, and tail. The hair bundle may be sparse, broken, interrupted, or very occasionally absent. When pixel evidence is too low, do not invent an eyebrow from anchor points alone.

Orbital-eyebrow guide rule: eyebrow detection is constrained by the upper orbital structure. Estimate an orbital center from `eyeCenter` and `irisCenter`/`pupilCenter`, build a soft upper orbital arc above the upper eyelid, and search only inside that orbital-brow ROI. The upper orbital arc is a horizontally stretched curved guide, not a face-wide box and not a final mask. Use the arc, eye width, eye height, and eye-to-brow distance to reject eyelid shadow, lower lash darkness, forehead wrinkles, bangs, and floating dark lines before pixel fitting.

Nose mask rule: nose anchors define a nose ROI and `NoseStructureGuide`, not nostril-only dark components and not a final editable mask. The final `NoseMask` must be an area-based union of `noseBridgeSurfaceMask`, `noseTipSurfaceMask`, left/right `noseWingSurfaceMask`, and `noseBaseSurfaceMask`, then exclude `nostrilProtectionMask`.

Mouth/nose proximity rule: lip and inner-mouth masks must be clipped by anchored nose-to-mouth distance ratios so they cannot overlap nostril interiors or nose base. Do not solve overlap by fixed coordinates.

Component mask rule: detected component masks are internal soft masks only. Do not export separate RGBA/channel mask PNGs for detected parts; masks guide ROI, protection, snapping, and correction safety inside the engine.

Rules:

- Do not precompute or apply all base corrections when a tab opens.
- Do not block or modify the entire face globally.
- Do not force ideal facial proportions.
- Do not auto-correct asymmetry unless the specific feature tool is active and the user requests adjustment.
- Default state is detect only if needed, protect identity, preserve original shape, and apply nothing visible until the user changes a control.

## Global Face Landmark List

This list defines common landmark and mask names used by all retouch modules. These names are shared by K-AnchorMesh, K-AnchorMeasure, mask builders, future dense landmark engines, and K-AnchorWarp handles.

Not every AI model provides every point directly. If a point is unavailable, estimate it from nearby landmarks, segmentation masks, skin masks, contours, or virtual ROI points. Do not assume dense anatomy points exist unless the active model actually provides them.

### Face Boundary

- `faceLeft`: visible left boundary of the face contour.
- `faceRight`: visible right boundary of the face contour.
- `faceTop`: top of visible face area, usually forehead/hairline; estimate upper face boundary if hairline is hidden.
- `faceBottom`: lowest chin point.
- `faceCenterX = (faceLeft + faceRight) / 2`
- `faceCenterY = (faceTop + faceBottom) / 2`
- `faceW = faceRight - faceLeft`
- `faceH = faceBottom - faceTop`
- `faceContourPoints[]`: full visible outer contour for jawline, cheek, chin, and face shape.

### Head And Pose

- `headRollAngle`: face tilt, usually from `leftEyeCenter -> rightEyeCenter`.
- `headYawEstimate`: left/right face rotation estimate.
- `headPitchEstimate`: up/down head tilt estimate.
- `frontalPoseConfidence`: near-frontal confidence.

### Eyes

- `leftEyeOuter`, `leftEyeInner`, `leftEyeUpperMid`, `leftEyeLowerMid`, `leftEyeCenter`
- `leftPupilCenter`, `leftIrisCenter`, `leftIrisRadius`
- `rightEyeInner`, `rightEyeOuter`, `rightEyeUpperMid`, `rightEyeLowerMid`, `rightEyeCenter`
- `rightPupilCenter`, `rightIrisCenter`, `rightIrisRadius`
- `eyeCenterX`: center between both eyes.
- `eyeCenterY`: average vertical position of both eyes.
- `eyeDist`: distance between `leftEyeCenter` and `rightEyeCenter`.
- `eyeGap`: distance between `leftEyeInner` and `rightEyeInner`.

### Eyelids

- `leftUpperEyelidContour[]`, `leftLowerEyelidContour[]`
- `rightUpperEyelidContour[]`, `rightLowerEyelidContour[]`
- `leftUpperEyelidMask`, `leftLowerEyelidMask`
- `rightUpperEyelidMask`, `rightLowerEyelidMask`

### Eyelashes

- `leftUpperEyelashMask`, `leftLowerEyelashMask`
- `rightUpperEyelashMask`, `rightLowerEyelashMask`
- `leftUpperLashRootLine`, `leftLowerLashRootLine`
- `rightUpperLashRootLine`, `rightLowerLashRootLine`

### Eyebrows

- `leftBrowHead`, `leftBrowInner`, `leftBrowMid`, `leftBrowArch`, `leftBrowTail`, `leftBrowOuter`
- `rightBrowHead`, `rightBrowInner`, `rightBrowMid`, `rightBrowArch`, `rightBrowTail`, `rightBrowOuter`
- `browCenterX`: center between left and right eyebrows.
- `browCenterY`: average vertical brow position.

### Nose

- `noseRoot`: top of nose bridge between eyes.
- `noseBridgeTop`, `noseBridgeMid`, `noseBridgeLower`
- `noseTip`: front tip of nose.
- `noseBaseCenter`: center under nose where nose meets philtrum.
- `subnasale`: same or near `noseBaseCenter`.
- `noseLeftWing`, `noseRightWing`
- `noseLeftBase`, `noseRightBase`
- `nostrilLeft`, `nostrilRight`
- `noseCenterX`: nose center x-axis, often derived from nose wings.
- `noseW`: width between `noseLeftWing` and `noseRightWing`.
- Treat the nose as bridge planes + tip bulb + left/right wing volumes + base, not as a single center line.
- Detect `nostrilLeft` and `nostrilRight` separately as local dark openings under the tip and inside each wing.
- Do not define nostrils as identical black circles from total nose width only.

### Mouth

- `mouthLeftCorner`, `mouthRightCorner`
- `mouthCenter`: midpoint between mouth corners.
- `mouthInnerTop`, `mouthInnerBottom`, `mouthInnerLeft`, `mouthInnerRight`
- `innerMouthMask`: visible inside-mouth region.
- `teethMask`: visible teeth region, if any.

### Lips

- `upperLipTopCenter`, `upperLipCenter`
- `upperLipCupidLeft`, `upperLipCupidRight`
- `upperLipLeftPeak`, `upperLipRightPeak`
- `lowerLipCenter`, `lowerLipBottomCenter`
- `outerLipMask`, `upperLipMask`, `lowerLipMask`
- `lipSurfaceMask`
- `vermilionBorderMask`
- `lipTextureMask`
- `lipCrackMask`
- `lipPeelingMask`
- Treat lips as upper/lower soft volume pads, not as a single flat strip.
- The lower lip is usually a little fuller than the upper lip.
- Keep mouth corners softly connected inward instead of ending them as hard cut line tips.

### Philtrum

- `philtrumTop`: near `noseBaseCenter` / `subnasale`.
- `philtrumCenter`: center of philtrum groove.
- `philtrumBottom`: near `upperLipTopCenter`.
- `leftPhiltrumRidge`, `rightPhiltrumRidge`
- `philtrumMask`: skin region between nose base and upper lip.
- Treat the philtrum as a center groove with two soft ridges, not as two hard lines.
- Philtrum guidance should help ROI placement and protection, not force a visible deep groove.

### Cheeks

- `leftCheekOuter`, `rightCheekOuter`
- `leftCheekCenter`, `rightCheekCenter`
- `leftCheekSkinMask`, `rightCheekSkinMask`
- `leftCheekHighlightCenter`, `rightCheekHighlightCenter`
- `leftCheekHollowCenter`, `rightCheekHollowCenter`

### Cheekbones

- `leftZygomaPeak`, `rightZygomaPeak`
- `leftCheekboneMask`, `rightCheekboneMask`
- `leftCheekHighlightMask`, `rightCheekHighlightMask`
- `leftCheekHollowMask`, `rightCheekHollowMask`

### Nasolabial Folds

- `leftNasolabialStart`, `rightNasolabialStart`
- `leftNasolabialFoldMask`, `rightNasolabialFoldMask`

### Mouth Area Lines

- `leftMouthCornerLineMask`, `rightMouthCornerLineMask`
- `leftMarionetteLineMask`, `rightMarionetteLineMask`
- `perioralFineLineMask`

### Chin

- `chinPoint`: lowest center point of chin.
- `leftChinSide`, `rightChinSide`
- `chinCenterX`
- `chinMask`
- `mentalCreaseMask`
- `chinTextureMask`

### Jawline

- `leftMandibleStart`, `rightMandibleStart`
- `leftJawAngle`, `rightJawAngle`
- `leftJawBodyUpper`, `rightJawBodyUpper`
- `leftJawBodyMid`, `rightJawBodyMid`
- `leftJawBodyLower`, `rightJawBodyLower`
- `leftChinTransition`, `rightChinTransition`
- `jawlineMask`
- `leftJawContourPoints[]`, `rightJawContourPoints[]`

### Square Jaw And Masseter

- `leftMasseterCenter`, `rightMasseterCenter`
- `leftMasseterMask`, `rightMasseterMask`
- `jawAngleMask`
- `jawBodyMask`
- `chinTransitionMask`

### Under-Chin

- `submentalCenter`
- `leftUnderJawPoint`, `rightUnderJawPoint`
- `submentalMask`
- `underJawShadowMask`
- `doubleChinFoldMask`
- `neckSeparationMask`

### Neck

- `neckLeft`, `neckRight`, `neckCenter`
- `neckMask`
- `neckHorizontalWrinkleMask`
- `neckVerticalWrinkleMask`
- `collarMask`
- `necklaceProtectionMask`

### Forehead

- `hairlineCenter`
- `hairlineLeftTemple`, `hairlineRightTemple`
- `foreheadCenter`
- `foreheadTopCenter`, `foreheadBottomCenter`
- `foreheadLeftBoundary`, `foreheadRightBoundary`
- `foreheadMask`
- `foreheadWrinkleMask`
- `foreheadShineMask`

Forehead surface-flow guide:

- Forehead texture and tone should not be read as one flat panel.
- The frontalis flow is largely vertical from brow area toward the hairline, while the skull volume still rolls sideward into the temples.
- A useful reading is vertical lift plus soft horizontal skull curvature.
- Forehead center often keeps the clearest broad plane.
- Toward the temples, the surface usually wraps away and should be treated as a side plane rather than the same front plane.

### Glabella

- `glabella`: center region between eyebrows above nose root.
- `glabellaMask`
- `glabellaWrinkleMask`
- `glabellaShadowMask`

Glabella surface-flow guide:

- The glabella is not just a dark spot between the brows.
- Its pull often gathers from the brow heads toward the center and slightly downward.
- If expression is present, read it as a narrow compressed valley with adjacent raised planes, not as a single black line.
- Use brow-head-to-center convergence, tone compression, and small ridge/valley contrast as structure clues.

### Hair

- `hairMask`
- `hairCoreMask`
- `hairSoftEdgeMask`
- `hairStrandMask`
- `flyawayHairMask`
- `bangsMask`
- `hairlineMask`
- `templeHairMask`
- `sideburnMask`
- `hairOcclusionMask`

### Ears

Common face landmark models usually do not provide detailed ear anatomy landmarks. Ear points are virtual estimated points from ROI and ear mask, not direct face-landmark anatomy.

- `leftEarCenterApprox`, `rightEarCenterApprox`: rough ear location from pose model, if available.
- `leftEarMaskEstimated`, `rightEarMaskEstimated`
- `leftEarTopEstimated`, `leftEarBottomEstimated`
- `leftEarOuterEstimated`, `leftEarInnerAttachEstimated`, `leftEarCenterEstimated`
- `rightEarTopEstimated`, `rightEarBottomEstimated`
- `rightEarOuterEstimated`, `rightEarInnerAttachEstimated`, `rightEarCenterEstimated`

Do not require helix, antihelix, concha, tragus, antitragus, inner-ear ridge, or detailed earlobe landmarks by default.

### Skin

- `skinMask`: all visible skin regions.
- `faceSkinMask`: visible face skin excluding eyes, lips, hair, teeth, and clothing.
- `cleanSkinMask`: skin region without major blemish, wrinkle, hair, shadow, or makeup.
- `skinTextureMask`
- `poreMask`
- `blemishMask`
- `acneMask`
- `moleMask`
- `freckleMask`
- `pigmentationMask`
- `rednessMask`
- `scarMask`
- `photoDamageMask`

### Dark Circle

- `leftUnderEyeMask`, `rightUnderEyeMask`
- `leftTearTroughMask`, `rightTearTroughMask`
- `leftEyeBagMask`, `rightEyeBagMask`
- `leftUnderEyeShadowMask`, `rightUnderEyeShadowMask`
- `leftUnderEyePigmentationMask`, `rightUnderEyePigmentationMask`
- `leftUnderEyeVascularMask`, `rightUnderEyeVascularMask`

### Global Protection Masks

- `eyeProtectionMask`: protects eyes from skin/lip/face correction.
- `eyelashProtectionMask`: protects lashes from smoothing and eye whitening.
- `eyebrowProtectionMask`: protects eyebrows from skin correction.
- `lipProtectionMask`: protects lips from skin correction.
- `teethProtectionMask`: protects teeth from lip/mouth correction.
- `hairProtectionMask`: protects hair from skin/background correction.
- `beardProtectionMask`: protects beard or shaving texture.
- `nostrilProtectionMask`: protects nostril dark regions.
- `wrinkleProtectionMask`: preserves structural lines when needed.
- `moleIdentityProtectionMask`: protects identity-defining moles or marks.
- `skinTextureProtectionMask`: protects pores and natural skin grain.
- `clothingProtectionMask`: protects clothes and collar.
- `backgroundProtectionMask`: prevents background from bleeding into hair/skin.

## Core Anchors

Base measurements:

- `faceW`: visible left/right face width.
- `faceH`: visible top/chin face height.
- `eyeL`, `eyeR`: left/right eye centers.
- `eyeDist`: distance between left/right eye centers.
- `noseTip`: nose tip.
- `noseBase`: center under nose wings.
- `mouthCenter`: mouth center.
- `lipTop`, `lipBottom`: upper/lower lip bounds.
- `mouthLeft`, `mouthRight`: mouth corners.
- `browL`, `browR`: left/right brow centers.

Important:

- Measure after face roll correction when possible.
- Use the original face as the baseline.
- Only values that are unusually out of range should become weak correction candidates.

## Whole Face

Recommended front-facing range:

- `faceH / faceW`: `1.35 ~ 1.55`

Men may look slightly longer or more angular. Women may look slightly softer or shorter. This is not a fixed rule.

## Face Contour And Jawline

Face contour metrics define outer contour, jawline, chin, cheek, and lower-face geometry. They are for natural retouch guidance and over-correction prevention, not fixed beauty standards.

Contour correction is identity-sensitive. Preserve identity, age, bone structure, weight impression, and natural asymmetry.

### Face Contour Anchors

Dense contour target anchors:

- `faceLeft`, `faceRight`, `faceTop`, `faceBottom`
- `leftTemple`, `rightTemple`
- `leftCheekOuter`, `rightCheekOuter`
- `leftZygoma`, `rightZygoma`
- `leftJawAngle`, `rightJawAngle`
- `leftJawMid`, `rightJawMid`
- `leftChinSide`, `rightChinSide`
- `chinPoint`
- `foreheadCenter`, `browCenter`, `eyeCenter`, `noseBaseCenter`, `mouthCenter`
- `neckLeft`, `neckRight`
- `faceContourPoints[]`, `leftContourPoints[]`, `rightContourPoints[]`

Reduce confidence when yaw, pitch, wide-angle distortion, hair, beard, shadow, crop, neck blending, scarf/clothing, low resolution, blur, or background/skin similarity affects the contour.

### Primary Widths And Heights

Widths:

- `templeW = distance(leftTemple, rightTemple)`
- `cheekW = distance(leftCheekOuter, rightCheekOuter)`
- `zygomaW = distance(leftZygoma, rightZygoma)`
- `jawAngleW = distance(leftJawAngle, rightJawAngle)`
- `jawMidW = distance(leftJawMid, rightJawMid)`
- `chinW = distance(leftChinSide, rightChinSide)`
- `neckW = distance(neckLeft, neckRight)`

Heights:

- `upperFaceH = browCenter.y - faceTop`
- `midFaceH = noseBaseCenter.y - browCenter.y`
- `lowerFaceH = chinPoint.y - noseBaseCenter.y`
- `mouthToChinH = chinPoint.y - mouthCenter.y`
- `jawHeight = chinPoint.y - ((leftJawAngle.y + rightJawAngle.y) / 2)`
- `chinHeight = chinPoint.y - ((leftChinSide.y + rightChinSide.y) / 2)`

### Overall Contour Ratios

- `FaceHeightWidthRatio = faceH / faceW`: `1.35 ~ 1.55`
- `CheekFaceWidthRatio = cheekW / faceW`: `0.88 ~ 1.00`
- `JawFaceWidthRatio = jawAngleW / faceW`: `0.68 ~ 0.86`
- `ChinFaceWidthRatio = chinW / faceW`: `0.18 ~ 0.32`
- `NeckFaceWidthRatio = neckW / faceW`: `0.45 ~ 0.70`

Interpretation:

- Low `faceH / faceW`: wide/round face or lens/crop issue.
- High `faceH / faceW`: long face or pitch/crop issue.
- High `jawAngleW`: square jaw or strong lower face.
- Low `chinW`: narrow/pointed chin.
- High `chinW`: broad chin.

These are soft ranges. Do not force face shape into one ideal.

### Face Width Slices

Measure contour width at normalized y positions:

- `widthAt20 = faceTop + faceH * 0.20`
- `widthAt35 = faceTop + faceH * 0.35`
- `widthAt50 = faceTop + faceH * 0.50`
- `widthAt65 = faceTop + faceH * 0.65`
- `widthAt80 = faceTop + faceH * 0.80`
- `widthAt90 = faceTop + faceH * 0.90`

Ratios:

- `ForeheadWidthRatio = widthAt20 / faceW`
- `EyeLevelWidthRatio = widthAt35 / faceW`
- `CheekLevelWidthRatio = widthAt50 / faceW`
- `MouthLevelWidthRatio = widthAt65 / faceW`
- `JawLevelWidthRatio = widthAt80 / faceW`
- `ChinLevelWidthRatio = widthAt90 / faceW`

Typical reading:

- `widthAt50` is often widest or near-widest.
- High `widthAt80`: strong jawline.
- High `widthAt90`: broad chin.
- Smooth decrease from `widthAt65` to `widthAt90`: natural V/oval lower face.
- Sudden collapse from `widthAt65` to `widthAt90`: over-sharpened contour or detection error.

### Jawline Core Metrics

- `JawWidthRatio = jawAngleW / cheekW`: `0.72 ~ 0.92`
- `JawMidRatio = jawMidW / cheekW`: `0.62 ~ 0.82`
- `ChinWidthCheekRatio = chinW / cheekW`: `0.20 ~ 0.36`
- `JawToChinTaperRatio = chinW / jawAngleW`: `0.25 ~ 0.45`
- `JawHeightRatio = jawHeight / faceH`: `0.18 ~ 0.28`
- `LowerFaceRatio = lowerFaceH / faceH`: `0.30 ~ 0.38`
- `MouthToChinRatio = mouthToChinH / lowerFaceH`: `0.45 ~ 0.62`

Interpretation:

- High `jawAngleW / cheekW`: square or strong jaw.
- Low `chinW / jawAngleW`: sharp chin.
- High `chinW / jawAngleW`: broad chin.
- High `lowerFaceH`: long lower face.
- Low `lowerFaceH`: short lower face.

Jawline correction changes identity strongly. Avoid making every face V-line.

### Jaw Angle And Smoothness

Jaw angles:

- `LeftJawAngleDeg`: angle between `leftCheekOuter -> leftJawAngle` and `leftJawAngle -> chinPoint`.
- `RightJawAngleDeg`: angle between `rightCheekOuter -> rightJawAngle` and `rightJawAngle -> chinPoint`.
- `AvgJawAngleDeg = (LeftJawAngleDeg + RightJawAngleDeg) / 2`
- `JawAngleBalanceScore = abs(LeftJawAngleDeg - RightJawAngleDeg)`

Guide:

- `0 ~ 4 deg`: natural.
- `4 ~ 8 deg`: weak asymmetry.
- `8+ deg`: pose, contour detection, or asymmetry issue first.

Smoothness:

- `LeftJawCurveSmoothness`: curvature variance from left jaw angle to chin.
- `RightJawCurveSmoothness`: curvature variance from right jaw angle to chin.
- `JawCurveBalanceScore = abs(LeftJawCurveSmoothness - RightJawCurveSmoothness)`
- `ContourNoiseScore`: local zigzag/edge instability along jaw contour.

High contour noise may come from hair, beard, shadow, compression, or segmentation error. Smooth mask first, not face shape.

### Chin Metrics

- `ChinCenterX = (leftChinSide.x + rightChinSide.x + chinPoint.x) / 3`
- `ChinCenterScore = abs(ChinCenterX - faceCenterX) / faceW`
  - `0.00 ~ 0.03`: natural.
  - `0.03 ~ 0.05`: weak offset.
  - `0.05+`: yaw, pose, or detection issue first.
- `ChinNoseCenterScore = abs(chinPoint.x - noseBaseCenter.x) / faceW`: `0.00 ~ 0.035`
- `ChinMouthCenterScore = abs(chinPoint.x - mouthCenter.x) / faceW`: `0.00 ~ 0.04`
- `ChinWidthRatio = chinW / faceW`: `0.18 ~ 0.32`
- `ChinHeightRatio = chinHeight / faceH`: `0.045 ~ 0.085`
- `ChinProjectionVisualScore`: shadow/highlight-based chin protrusion estimate.

Chin should be judged with eye/nose/chin axis, not face outline alone. Mouth is expression-sensitive and lower confidence.

Safe limits:

- Chin horizontal move: `0.5% ~ 1.5%` of `faceW`.
- Chin vertical move: `0.5% ~ 1.5%` of `faceH`.
- Chin width change: `3% ~ 6%` of original `chinW`.
- `10%+` chin change likely changes identity.

### Cheek And Zygoma

- `CheekWidthRatio = cheekW / faceW`: `0.88 ~ 1.00`
- `ZygomaJawRatio = zygomaW / jawAngleW`: `1.10 ~ 1.35`
- `ZygomaChinRatio = zygomaW / chinW`: `2.80 ~ 4.80`
- `CheekBalanceScore`: left/right cheek distance difference divided by `faceW`
  - `0.00 ~ 0.04`: natural.
  - `0.04 ~ 0.07`: weak asymmetry.
  - `0.07+`: yaw, light, or detection issue first.

Cheek and zygoma width are important for identity. Do not reduce cheekbone automatically. In portraits, cheek shadow cleanup is safer than cheek geometry change.

### Left/Right Contour Balance

For each normalized y slice:

- `leftDistY = faceCenterX - leftContourXAtY`
- `rightDistY = rightContourXAtY - faceCenterX`
- `ContourBalanceAtY = abs(leftDistY - rightDistY) / faceW`
- `ContourBalanceScore = average ContourBalanceAtY over y = 0.25 ~ 0.90`
- `LowerContourBalanceScore = average ContourBalanceAtY over y = 0.60 ~ 0.90`

Guide:

- `0.00 ~ 0.035`: natural.
- `0.035 ~ 0.060`: weak asymmetry.
- `0.060+`: pose, hair, shadow, or contour error first.

Use multiple height slices instead of one jaw point. If only one slice is abnormal, suspect hair/shadow/segmentation.

### Face Shape Reading

Use only as soft classification, not correction target.

- Round-ish: lower face wide, cheek width high, soft jaw angle.
- Oval: cheek widest, jaw narrower, smooth taper to chin.
- Square: high jaw/cheek ratio, strong jaw angle, broad chin.
- V-line/heart-like: high cheek/zygoma, narrow jaw/chin, strong taper.
- Long: high `faceH / faceW`, high lower-face or mid-face height.

Do not reshape all faces toward oval or V-line.

### Contour Confidence

Suggested confidence:

- `ContourMetricConfidence = frontalPoseConfidence * faceSegmentationConfidence * jawLandmarkConfidence * contourVisibilityConfidence * lightingConfidence * cropConfidence`

Reduce confidence when:

- Hair covers cheek/jaw contour.
- Beard or mustache affects jaw boundary.
- Dark shadow under jaw.
- Neck blends into jaw.
- Face is cropped.
- Profile/yaw is strong.
- Wide-angle lens distortion is visible.
- Strong smile changes cheek and jaw.
- Low resolution or blur.
- Background color is close to skin.
- Double chin, scarf, or clothing hides jaw.
- Segmentation mask is unstable.

### Contour Retouch Priority

1. Correct face roll/basic alignment.
2. Check pose/yaw/pitch confidence.
3. Verify face segmentation and jaw landmarks.
4. Compare left/right contour by multiple y slices.
5. Check jaw width, chin width, and lower-face ratio.
6. Check whether issue is hair, shadow, or neck boundary.
7. Clean contour mask and local shadows first.
8. Apply minimal geometry correction only if multiple metrics agree.

Prefer shadow cleanup, edge cleanup, and mask smoothing over reshaping.

### Contour Safe Correction Limits

- Face outer contour move: `0.5% ~ 1.5%` of `faceW` per side.
- Cheek width change: `1% ~ 3%` of original `cheekW`.
- Jaw width change: `2% ~ 5%` of original `jawAngleW`.
- Jaw mid change: `2% ~ 5%` of original `jawMidW`.
- Chin width change: `3% ~ 6%` of original `chinW`.
- Chin vertical move: `0.5% ~ 1.5%` of `faceH`.
- Chin horizontal move: `0.5% ~ 1.5%` of `faceW`.
- Jaw angle softening: small local warp only.
- Contour smoothing: smooth segmentation edge without changing bone structure.

`10%+` contour change is identity-changing. For ID photo, memorial portrait, restoration, and professional portrait, use smaller limits.

## Face Shape Analysis

Face shape analysis defines visible contour, normalized width levels, face shape scores, occlusion flags, and confidence for portrait retouch safety. It is not automatic reshaping or beautification.

Purpose:

- Contour understanding.
- Jawline confidence.
- Cheekbone confidence.
- Forehead/temple relation.
- Local retouch safety.
- Over-correction prevention.
- Slider range limitation.

Core rules:

- Face shape is identity-sensitive.
- Do not force an ideal face shape.
- Do not automatically make V-line, oval face, small face, slim jaw, or high cheekbone.
- Analyze structure only.
- Visible correction runs only when the user activates a related control.
- Preserve original face shape by default.

Face shape analysis must not:

- Reshape, slim, reduce jaw, reduce cheekbone, lift chin, change face width, force symmetry, or apply correction when a tab opens.

Face shape analysis must:

- Identify visible face contour.
- Estimate reliable structural points.
- Separate true face contour from hair, beard, shadow, clothing, and background.
- Determine confidence before contour-related correction.
- Protect identity and original shape.
- Provide safe limits for manual tools.

### Face Shape Regions And Masks

Primary regions:

- `foreheadRegion`
- `templeRegion`
- `cheekboneRegion`
- `midFaceRegion`
- `cheekRegion`
- `jawAngleRegion`
- `jawBodyRegion`
- `chinRegion`
- `underChinRegion`
- `neckRegion`

Required masks:

- `faceContourMask`
- `faceSkinMask`
- `foreheadMask`
- `templeMask`
- `cheekboneMask`
- `cheekMask`
- `jawlineMask`
- `chinMask`
- `neckMask`

Protection and confusion masks:

- `hairMask`
- `bangsMask`
- `sideHairMask`
- `beardMask`
- `sideburnMask`
- `longBeardMask`
- `underJawShadowMask`
- `neckShadowMask`
- `clothingMask`
- `backgroundMask`
- `glassesMask`
- `earMask`

### Face Shape Landmarks

Primary landmarks:

- `faceLeft`, `faceRight`, `faceTop`, `faceBottom`
- `faceCenterX`, `faceCenterY`, `faceW`, `faceH`
- `hairlineCenter`, `hairlineLeftTemple`, `hairlineRightTemple`
- `leftTemplePoint`, `rightTemplePoint`
- `leftZygomaPeak`, `rightZygomaPeak`
- `leftCheekOuter`, `rightCheekOuter`
- `leftCheekCenter`, `rightCheekCenter`
- `leftJawAngle`, `rightJawAngle`
- `leftJawBodyUpper`, `rightJawBodyUpper`
- `leftJawBodyMid`, `rightJawBodyMid`
- `leftJawBodyLower`, `rightJawBodyLower`
- `leftChinTransition`, `rightChinTransition`
- `chinPoint`, `chinCenterX`
- `submentalCenter`, `neckLeft`, `neckRight`

Landmark note:

- Direct or semi-direct points include face outline, chin point, approximate jaw contour, and eye/nose/mouth anchors.
- Temples, cheekbone peaks, jaw angles, jaw-body segments, chin transition, true face edge under hair, true jaw edge under beard, and under-chin contour are estimated from contour and masks.
- If hair, beard, shadow, or clothing hides contour, reduce confidence, do not guess aggressively, and do not use hidden contour for reshaping.

### Normalized Width Levels

Measure horizontal face width at multiple roll-corrected vertical levels:

- `foreheadWidthY`: `0.20 ~ 0.30` of `faceH`
- `templeWidthY`: `0.30 ~ 0.38` of `faceH`
- `cheekboneWidthY`: `0.42 ~ 0.56` of `faceH`
- `midFaceWidthY`: `0.55 ~ 0.65` of `faceH`
- `jawAngleWidthY`: `0.68 ~ 0.80` of `faceH`
- `jawBodyWidthY`: `0.76 ~ 0.88` of `faceH`
- `chinWidthY`: `0.88 ~ 0.96` of `faceH`

Width values:

- `foreheadW`
- `templeW`
- `cheekboneW`
- `midFaceW`
- `jawAngleW`
- `jawBodyW`
- `chinW`

Hair and beard can falsely increase width. Face yaw reduces shape confidence.

### Face Shape Ratios

Primary ratios:

- `FaceAspectRatio = faceH / faceW`
- `ForeheadCheekRatio = foreheadW / cheekboneW`
- `TempleCheekRatio = templeW / cheekboneW`
- `CheekJawRatio = cheekboneW / jawAngleW`
- `JawCheekRatio = jawAngleW / cheekboneW`
- `JawChinRatio = jawAngleW / chinW`
- `ChinFaceRatio = chinW / faceW`
- `ChinJawRatio = chinW / jawAngleW`
- `JawTaperRatio = (jawAngleW - chinW) / jawAngleW`
- `LowerFaceRatio = distance(mouthCenter, chinPoint) / faceH`
- `UpperFaceRatio = distance(faceTop, browCenterY) / faceH`
- `MidFaceRatio = distance(browCenterY, noseBaseCenter.y) / faceH`
- `LowerThirdRatio = distance(noseBaseCenter.y, chinPoint) / faceH`
- `CheekbonePositionYRatio = avg(leftZygomaPeak.y, rightZygomaPeak.y) / faceH`
- `JawAnglePositionYRatio = avg(leftJawAngle.y, rightJawAngle.y) / faceH`
- `ChinPointOffsetRatio = abs(chinPoint.x - faceCenterX) / faceW`

### Face Shape Candidates

Do not classify as absolute identity. Use soft probability scores.

- `ovalFaceCandidate`: longer face, cheekbone slightly wider than forehead/jaw, smooth chin taper, soft jaw angles.
- `roundFaceCandidate`: lower faceH/faceW, cheek and jaw widths close, soft jawline, compact lower-face impression.
- `squareFaceCandidate`: forehead, cheekbone, and jaw widths similar, strong jaw angle, straight jaw body, broad chin, low taper.
- `rectangleFaceCandidate`: long face with similar forehead, cheekbone, and jaw widths.
- `heartFaceCandidate`: forehead/temple wider than jaw, cheekbone/forehead wide, narrower chin, strong taper.
- `diamondFaceCandidate`: cheekbone widest, forehead narrower, jaw/chin narrower, high cheekbone prominence.
- `triangleFaceCandidate`: jaw wider than forehead/cheek, visually heavier lower face, broad jaw/chin.
- `invertedTriangleFaceCandidate`: forehead/temple widest, jaw and chin narrow.
- `longFaceCandidate`: high faceH/faceW, elongated mid/lower face, narrow width impression.
- `shortFaceCandidate`: low faceH/faceW, compact vertical proportions.

Example scoring:

- `OvalScore = smoothJawTaper + cheekboneW slightly greater than jawAngleW + moderate faceAspectRatio + low jaw angularity`
- `RoundScore = low faceAspectRatio + soft jawline + cheekW close to jawW + low angularity + cheek fullness`
- `SquareScore = jawAngleW close to cheekboneW + broad chin + high jaw angularity + straight jaw body + low jaw taper`
- `RectangleScore = high faceAspectRatio + jawAngleW close to cheekboneW + foreheadW close to jawAngleW + straight vertical side contour`
- `HeartScore = foreheadW or templeW greater than jawAngleW + narrow chin + high jaw taper + cheekbone not smaller than jaw`
- `DiamondScore = cheekboneW greater than foreheadW + cheekboneW greater than jawAngleW + narrow chin + visible cheekbone prominence`
- `TriangleScore = jawAngleW greater than cheekboneW + jawAngleW greater than foreheadW + broad lower face`
- `LongFaceScore = high faceAspectRatio + longer mid/lower thirds + narrow width impression`

### Contour Features

Useful contour metrics:

- `JawAngularityScore`
- `JawStraightnessScore`
- `JawSoftnessScore`
- `JawTaperScore`
- `ChinSharpnessScore`
- `ChinBroadnessScore`
- `CheekboneProminenceScore`
- `CheekFullnessScore`
- `TempleNarrownessScore`
- `ForeheadWidthScore`
- `FaceLengthScore`
- `FaceRoundnessScore`
- `FaceBlockinessScore`

### Face Shape Asymmetry

Asymmetry metrics:

- `LeftRightContourBalanceScore`
- `JawAngleBalanceScore`
- `CheekboneBalanceScore`
- `ChinCenterBalanceScore`
- `ForeheadBalanceScore`

Asymmetry can be caused by pose, lighting, hair, beard, or expression. Do not auto-correct asymmetry. Use it for confidence and optional manual guide only.

### Pose, Hair, Beard, And Shadow Confusion

Pose reduces face shape reliability:

- Yaw makes one cheek or jaw wider.
- Pitch down makes forehead larger and chin smaller.
- Pitch up makes jaw/neck more visible.
- Roll tilts symmetry.
- Wide-angle lenses enlarge central face and distort contour.

Hair confusion:

- Side hair can increase face width.
- Bangs can hide forehead.
- Hairline can hide true face top.
- Loose hair can distort cheek contour.

Beard confusion:

- Jaw beard can hide true jawline.
- Long beard can hide chin and neck.
- Mustache can affect mouth/chin analysis.
- Sideburn can change side-face boundary.

Shadow confusion:

- Under-jaw shadow can look like jaw shape.
- Cheek shadow can exaggerate cheekbone.
- Neck shadow can create false double chin.
- Harsh side lighting can change contour perception.

Rule:

- If contour is hidden by hair, beard, shadow, clothing, or background blend, mark the region hidden and reduce confidence.
- Do not estimate aggressive shape correction from occluded regions.

### Face Shape Confidence

Suggested confidence:

- `FaceShapeConfidence = faceContourConfidence * frontalPoseConfidence * hairOcclusionInverseConfidence * beardOcclusionInverseConfidence * shadowConfusionInverseConfidence * landmarkConfidence * resolutionConfidence * lightingConfidence`

Decision:

- High confidence: frontal face, clear contour, little hair/beard occlusion, stable lighting, good resolution.
- Medium confidence: minor occlusion or mild pose; use soft classification only.
- Low confidence: hidden contour, strong pose, heavy beard/hair, low resolution, or strong shadow; do not classify strongly.

### Face Shape Output

Output should include:

- `faceShapeScores`: `ovalScore`, `roundScore`, `squareScore`, `rectangleScore`, `heartScore`, `diamondScore`, `triangleScore`, `invertedTriangleScore`, `longFaceScore`, `shortFaceScore`
- `primaryShapeCandidate`
- `secondaryShapeCandidate`
- `shapeConfidence`
- `widthMeasurements`: `foreheadW`, `templeW`, `cheekboneW`, `midFaceW`, `jawAngleW`, `jawBodyW`, `chinW`
- `ratioMeasurements`: `FaceAspectRatio`, `ForeheadCheekRatio`, `CheekJawRatio`, `JawChinRatio`, `JawTaperRatio`, `ChinFaceRatio`
- `contourMetrics`: `JawAngularityScore`, `JawStraightnessScore`, `JawSoftnessScore`, `ChinSharpnessScore`, `CheekboneProminenceScore`, `FaceRoundnessScore`, `FaceBlockinessScore`
- `occlusionFlags`: `hairOccludedForehead`, `hairOccludedCheek`, `beardOccludedJaw`, `longBeardOccludedChin`, `shadowOccludedUnderJaw`, `clothingOccludedNeck`
- `debugMasks`: `faceContourMask`, `cheekboneRegion`, `jawAngleRegion`, `chinRegion`, `occlusionMask`

### Use In Retouch Modules

- Skin smoothing: does not use face shape for automatic smoothing; only protects contour edges.
- Jawline module: uses face shape for confidence and safe limits; does not auto-slim jaw.
- Cheekbone module: uses cheekbone prominence and occlusion confidence; does not auto-reduce cheekbones.
- Chin module: uses chin width and chin point confidence; does not auto-sharpen chin.
- Dark-circle, wrinkle, and tone modules: face shape provides region stability only, no shape correction.
- Hair/beard modules: if hair/beard hides contour, reduce face shape confidence.

### Face Shape Manual Tool Limits

If the user activates a contour-related slider:

- Natural mode: contour movement limit `1% ~ 3%` of `faceW`, preserve identity.
- Moderate mode: contour movement limit `3% ~ 5%` of `faceW`, require high confidence.
- Strong mode: contour movement limit `5% ~ 8%` of `faceW`, warning recommended, avoid identity change.

Forbidden:

- Automatic V-line.
- Automatic face slimming.
- Symmetrical reshaping without user command.
- Moving hidden contour under beard/hair.
- Changing bone structure from low-confidence analysis.

Final face shape rule:

- Do not define face shape as something to fix.
- Define visible contour, width levels, ratios, zones, occlusion, confidence, and soft shape scores.
- Preferred usage: contour understanding, jawline/cheek/chin confidence, protection mask refinement, manual tool safe limits, and over-correction prevention.
- Avoid automatic beauty correction, ideal oval forcing, automatic V-line, automatic jaw reduction, automatic cheekbone reduction, and asymmetry correction without user action.

## Square Jaw, Masseter, And Mandibular Angle

Square-jaw metrics define mandibular angle, masseter area, jaw body, lower-face blockiness, jaw-to-chin taper, under-jaw shadow, and neck separation. They are for natural retouch guidance and over-correction prevention. They are not beauty standards.

Core rules:

- Do not automatically create V-line.
- Do not automatically reduce square jaw.
- Square jaw is not just wide jaw.
- Square jaw can come from mandibular angle width, jaw-body straightness, jaw-angle sharpness, slow taper to chin, masseter fullness, lower-face blockiness, shadow, beard, hair, lens distortion, or segmentation error.
- Shadow, mask, beard/hair protection, and jaw-neck separation cleanup come before geometry.
- Geometry correction comes last and must be very small.
- Preserve identity, gender impression, age, bone structure, weight impression, and natural asymmetry.

### Square Jaw Preconditions

Before square-jaw analysis:

- Normalize roll using the eye-center line.
- Estimate yaw and pitch.
- Detect face contour, jawline landmarks, cheekbone/zygoma width, chin width, neck, under-jaw shadow, beard, hair, sideburn, collar/scarf, and background.
- Reduce confidence if yaw, pitch, beard, hair, sideburn, collar, under-jaw shadow, neck boundary, lens distortion, crop, low resolution, or segmentation is unstable.

A square look can be natural bone, masseter volume, fat/swelling, strong under-jaw shadow, face yaw, beard/sideburn, or narrow cheekbones making the jaw look relatively wide. Do not correct from one metric only.

### Square Jaw Regions

Target anchors and masks:

- `leftJawAngle`, `rightJawAngle`: mandibular angle below/near ear.
- `leftMandibleStart`, `rightMandibleStart`
- `leftJawBodyUpper`, `rightJawBodyUpper`
- `leftJawBodyMid`, `rightJawBodyMid`
- `leftJawBodyLower`, `rightJawBodyLower`
- `leftChinTransition`, `rightChinTransition`
- `leftChinSide`, `rightChinSide`
- `leftMasseterCenter`, `rightMasseterCenter`
- `leftUnderJawPoint`, `rightUnderJawPoint`, `submentalCenter`
- `neckLeft`, `neckRight`
- `leftJawContourPoints[]`, `rightJawContourPoints[]`
- `leftMasseterMask`, `rightMasseterMask`
- `jawAngleMask`, `jawBodyMask`, `chinTransitionMask`
- `underJawShadowMask`, `neckMask`
- `beardMask`, `hairOcclusionMask`, `sideburnMask`, `collarOcclusionMask`, `shadowMask`, `skinProtectionMask`

Square-jaw zones:

- Mandibular angle: rear jaw corner; main square-jaw indicator and highly affected by hair, sideburn, beard, and shadow.
- Masseter area: jaw muscle area in front of the mandibular angle; can make lower face look wide or heavy.
- Jaw body: line from jaw angle to chin transition; square jaw often has a straighter, less-tapered body.
- Chin transition: where jaw body turns into chin; blocky faces often have broad transition and slow inward taper.
- Chin width: broad chin increases blocky lower-face impression.
- Under-jaw shadow: can exaggerate jaw angle.
- Neck separation: weak separation can make the lower face look heavier.

Do not treat square jaw as one width value. Use jaw angle, jaw body, taper, chin, masseter, shadow, and neck together.

### Core Square Jaw Ratios

- `JawAngleFaceRatio = jawAngleW / faceW`: `0.68 ~ 0.86`, square candidate `0.86+`
- `JawAngleCheekRatio = jawAngleW / cheekW`: `0.72 ~ 0.92`, square candidate `0.92+`
- `JawAngleZygomaRatio = jawAngleW / zygomaW`: `0.75 ~ 0.95`, square candidate `0.95+`
- `JawBodyMidCheekRatio = jawBodyMidW / cheekW`: `0.62 ~ 0.82`, square candidate `0.82+`
- `JawBodyLowerCheekRatio = jawBodyLowerW / cheekW`: `0.50 ~ 0.72`, square candidate `0.72+`
- `ChinTransitionCheekRatio = chinTransitionW / cheekW`: `0.34 ~ 0.55`, blocky lower-face candidate `0.55+`
- `ChinJawRatio = chinW / jawAngleW`: `0.25 ~ 0.45`, broad/blocky candidate `0.45+`
- `JawToChinTaperRatio = chinW / jawAngleW`: `0.25 ~ 0.45`, square/blocky candidate `0.45+`

High jaw angle to cheek ratio means the jaw is close to cheek width. High jaw body to cheek ratio means the lower face keeps width instead of tapering. High chin to jaw ratio means the chin is broad rather than tapered. Square-jaw confidence rises only when multiple ratios are high together.

### Jaw Taper Profile

Taper metrics:

- `JawTaperUpper = jawBodyUpperW / jawAngleW`
- `JawTaperMid = jawBodyMidW / jawAngleW`
- `JawTaperLower = jawBodyLowerW / jawAngleW`
- `ChinTransitionTaper = chinTransitionW / jawAngleW`
- `FinalChinTaper = chinW / jawAngleW`
- `SquareJawTaperScore`

Natural taper usually follows:

- `jawAngleW >= jawBodyUpperW >= jawBodyMidW >= jawBodyLowerW >= chinTransitionW >= chinW`

Square-jaw profile:

- Jaw body remains close to jaw-angle width.
- Jaw body mid/lower widths remain high.
- Chin transition is broad.
- Taper into chin is slow.

Higher `SquareJawTaperScore` means slower taper and more blocky lower-face reading. Do not force fast taper.

### Mandibular Angle And Jaw Body

Mandibular angle metrics:

- `LeftMandibularAngleDeg`
- `RightMandibularAngleDeg`
- `AvgMandibularAngleDeg`
- `MandibularAngleBalanceDeg`
- `JawAngleSharpnessScore`

Guide:

- `0 ~ 4 deg`: natural balance.
- `4 ~ 8 deg`: weak asymmetry.
- `8+ deg`: pose, shadow, hair, beard, or detection issue first.

Jaw-body metrics:

- `LeftJawBodyStraightnessScore`
- `RightJawBodyStraightnessScore`
- `AvgJawBodyStraightnessScore`
- `JawBodySlopeBalanceDeg`

Square-jaw reading is supported by high straightness, wide jaw body, slow taper, and visible mandibular angle. Wide jaw without clear corner may be round or heavy lower face, not square jaw.

### Lower-Face Blockiness

`LowerFaceBlockinessScore` combines:

- `JawAngleCheekRatio`
- `JawBodyMidCheekRatio`
- `JawBodyLowerCheekRatio`
- `ChinTransitionCheekRatio`
- `ChinJawRatio`
- `AvgJawBodyStraightnessScore`
- `JawAngleSharpnessScore`

Blockiness means the lower face visually keeps width. It can come from bone, muscle, fat, swelling, beard, or shadow. Cause classification must come before correction.

### Masseter Area

Masseter metrics:

- `LeftMasseterArea`
- `RightMasseterArea`
- `MasseterAreaFaceRatio`
- `LeftMasseterVolumeVisualScore`
- `RightMasseterVolumeVisualScore`
- `MasseterBalanceScore`
- `MasseterBulgeScore`

Masseter fullness can make a face look square even if bone angle is not strong. It is affected by expression, clenching, smile, lighting, and beard. Tone/shadow cleanup is safer than geometry.

### Cause Classification

Cause scores:

- `BoneSquareCandidateScore`: wide jaw, sharp angle, straight jaw body, stable contour.
- `MasseterSquareCandidateScore`: masseter fullness and outward bulge without necessarily sharp angle.
- `FatOrSwellingCandidateScore`: soft contour, weak jaw angle, high submental depth, weak jaw-neck separation.
- `ShadowSquareCandidateScore`: strong under-jaw or one-sided shadow with low geometry support.
- `PoseSquareCandidateScore`: yaw/pitch, one-side enlargement, center mismatch.
- `BeardOrHairSquareCandidateScore`: beard/sideburn/hair overlap and contour instability.

Decision:

- Bone square: geometry metrics are meaningful.
- Masseter square: shape correction must be subtle.
- Fat/swelling: jaw/neck separation and shadow cleanup first.
- Shadow square: tone/shadow correction, not geometry.
- Pose square: no square-jaw geometry correction.
- Beard/hair square: protect hair/beard and avoid geometry.

### Under-Jaw And Neck Separation

Useful metrics:

- `UnderJawShadowStrength`
- `UnderJawShadowContinuity`
- `JawNeckSeparationScore`
- `SubmentalDepthRatio = submentalDepth / faceH`: `0.015 ~ 0.050`
- `NeckWidthFaceRatio`

Weak jaw-neck separation can make the lower face look heavy or square. Strong shadow can exaggerate angular jaw. Do not carve an artificial jawline or create a hard cutout edge.

### Under-Jaw Shadow Softening

Under-jaw shadow metrics define the shadow below the chin, the jaw-neck boundary, the submental area, lower-jaw shadow, double-chin shadow, and neck-transition shadow. This is a tone/shadow cleanup module, not a jaw-shape module.

Jawline and under-chin surface-flow guide:

- Jawline should be read as a hard turning edge between side face and under-jaw plane, not as one painted contour stroke.
- The side face often falls more vertically toward the jaw edge, while the under-chin plane turns inward and backward toward the neck.
- Under-chin tone usually groups into a broader darker mass because it receives less direct light.
- Keep the separation between jaw brightness above and under-chin depth below, but avoid carving a fake cutout edge.
- Neck transition can follow a broad cylindrical wrap; do not flatten chin and neck into one plane.

Core rules:

- Under-jaw shadow is not always a defect.
- It gives natural separation between jaw and neck.
- Do not erase it completely.
- Soften only dirty, harsh, uneven, or overly dark shadow.
- Preserve natural jaw depth, neck separation, face structure, age, and weight impression.
- Do not create artificial V-line.
- Do not carve a fake jawline.
- Geometry correction is off by default.

Required regions and masks:

- `chinMask`
- `jawlineMask`
- `leftJawlineMask`, `rightJawlineMask`
- `submentalMask`
- `underJawShadowMask`
- `neckMask`
- `doubleChinFoldMask`
- `neckWrinkleMask`
- `collarMask`
- `hairMask`
- `beardMask`
- `clothingMask`
- `backgroundMask`

Important regions:

- `UnderJawShadowRegion`: directly below the jawline and chin, between lower jaw contour and upper neck.
- `SubmentalRegion`: soft area under chin; may include natural volume or double-chin fold.
- `JawNeckBoundary`: transition line between jaw and neck.
- `DoubleChinFoldRegion`: fold or crease below chin; classify separately from simple shadow.

Primary metrics:

- `UnderJawShadowStrength`: darkness compared with nearby jaw/neck skin.
- `UnderJawShadowContrast`: local contrast between jaw skin and shadow area.
- `UnderJawShadowAreaRatio`: `area(underJawShadowMask) / area(submentalMask + upperNeckMask)`.
- `UnderJawShadowContinuity`: continuity of the shadow band under the jawline.
- `UnderJawShadowHardness`: edge sharpness of the shadow boundary.
- `UnderJawShadowPatchiness`: uneven blotchy darkness inside the shadow area.
- `JawNeckSeparationScore`: edge/contrast clarity between jawline and neck.
- `SubmentalVolumeScore`: soft bulge or volume under chin.
- `DoubleChinFoldDepthScore`: dark crease strength below chin.
- `PitchDrivenShadowScore`: head tilted down plus compressed neck.
- `LightingDrivenShadowScore`: directional light explains the shadow.
- `BeardShadowConfusionScore`: beard/shaving texture confused with under-jaw shadow.
- `ClothingShadowConfusionScore`: collar/clothing shadow affects lower neck.

Classification:

- `naturalJawShadow`: soft shadow following jaw shape; preserve.
- `harshUnderJawShadow`: too dark or too sharp; soften.
- `patchyUnderJawShadow`: blotchy lighting, beard, compression, or tone issue; tone-balance locally.
- `doubleChinFoldShadow`: fold or soft-volume shadow; soften carefully, never erase.
- `poseDrivenShadow`: head tilt driven; avoid geometry correction.
- `lightingDrivenShadow`: light-direction driven; preserve depth, reduce harshness only.
- `beardOrHairShadow`: protect beard/hair texture; do not brighten as skin.
- `clothingOrCollarShadow`: exclude from skin correction.
- `photoDamageShadow`: restoration cleanup allowed only when confirmed artifact.

Detection flow:

1. Detect jawline and chin boundary.
2. Define `UnderJawSearchROI`:
   - `y_start = chinPoint.y - 0.02 * faceH`
   - `y_end = chinPoint.y + 0.16 * faceH`
   - `x_start = leftJawAngle.x`
   - `x_end = rightJawAngle.x`
3. Separate jaw skin, neck skin, shadow, beard/hair, collar/clothing, double-chin fold, and neck wrinkles.
4. Estimate `underJawShadowMask` using local darkness, soft band shape, jawline relation, continuity, edge softness, skin-color consistency, and occlusion exclusions.
5. Calculate confidence before correction.

Confidence:

- `UnderJawShadowConfidence = jawlineMaskConfidence * neckMaskConfidence * skinMaskConfidence * lightingConfidence * beardOcclusionInverseConfidence * hairOcclusionInverseConfidence * clothingOcclusionInverseConfidence * poseConfidence * resolutionConfidence`

Reduce confidence when jawline is hidden, beard covers lower face, hair covers jaw/neck, collar/scarf covers neck, head is strongly tilted down, lighting is strongly directional, neck is cropped, resolution is low, old photo damage exists, background blends with neck, or double-chin fold overlaps shadow.

Decision:

- High confidence: local shadow softening allowed.
- Medium confidence: tone balancing only; avoid strong shadow removal.
- Low confidence: do not modify geometry, do not erase shadow, and use only very subtle cleanup if safe.

Correction types:

- Shadow softening: reduce excessive darkness while keeping jaw-neck separation.
- Patchiness cleanup: even dirty/blotchy shadow while preserving depth.
- Edge softening: soften overly sharp boundary without blurring the jawline.
- Neck separation balancing: improve transition without carving a line.
- Double-chin fold shadow softening: reduce harsh fold darkness while preserving submental volume.
- Artifact restoration: remove stain/scan damage only when confirmed.

Safe limits:

- `UnderJawShadowDarknessReduction`: `10% ~ 35%` by default.
- `HarshShadowReduction`: `20% ~ 45%` only with high confidence.
- `PatchyShadowToneBalancing`: `20% ~ 50%` local tone balancing.
- `DoubleChinFoldShadowReduction`: `10% ~ 30%`.
- `JawNeckSeparationChange`: subtle only.
- `ShadowEdgeSoftening`: low to medium; avoid blurred jawline.
- `GeometryChange`: off by default.
- `FullShadowRemoval`: not allowed.

Keep at least `40% ~ 70%` of the natural shadow impression. Never brighten the area until the jaw and neck become one flat plane, and never create a hard bright band under the chin.

Texture rules:

- Preserve skin texture, neck texture, beard texture, natural neck wrinkles, jaw boundary, and skin grain.
- Do not smear chin into neck.
- Do not create a flat patch, halo, hard cutout edge, or muddy gray shadow.
- Corrected area must match nearby jaw/neck texture.

Double-chin interaction:

- If `doubleChinFoldMask` exists, reduce harsh dark crease and dirty shadow, improve jaw-neck separation subtly, and preserve natural submental volume.
- Do not erase fold completely, pull jawline upward, carve fake V-line, flatten neck/chin structure, or remove age/weight impression automatically.
- `DoubleChinShadowCorrectionStrength = baseStrength * UnderJawShadowConfidence * inverse(PitchDrivenShadowScore) * texturePreservationWeight`

Protection:

- If `beardMask` overlaps `underJawShadowMask`, reduce skin correction strength and protect beard texture.
- If `hairMask` overlaps, protect hair strands and avoid smearing hair into neck skin.
- If `collarMask` or `clothingMask` overlaps, exclude clothing and collar shadow from skin correction.
- If necklace or jewelry exists, protect the object and cast shadow unless user asks.

Retouch priority:

1. Detect jawline, chin, neck, and submental region.
2. Detect occlusions: beard, hair, collar, scarf, jewelry, background, and photo damage.
3. Classify darkness as natural shadow, harsh shadow, patchy shadow, fold shadow, pose-driven, lighting-driven, beard/hair, clothing, or artifact.
4. If safe, soften shadow darkness, clean patchiness, preserve jaw-neck separation, and restore texture.
5. Check for no fake jawline, no flat neck, no erased age/weight structure, no halo, and no plastic texture.

Final under-jaw shadow rule:

- Do not define under-jaw shadow as a defect.
- Define whether it is natural, harsh, patchy, fold-driven, pose-driven, lighting-driven, beard/hair-driven, clothing-driven, or artifact.
- Preferred usage: dirty shadow cleanup, harsh under-jaw shadow softening, natural jaw-neck transition balancing, double-chin fold shadow reduction, and texture-preserving local tone correction.
- Avoid removing all jaw shadow, fake V-line creation, hard jaw carving, flat chin/neck, complete double-chin erasure, beard-shadow brightening as skin, and geometry edits in the shadow module.

### Square Jaw Balance

- `JawAngleBalanceScore = abs(leftJawAngleDist - rightJawAngleDist) / faceW`
- `JawBodyBalanceScore = abs(leftJawBodyMidDist - rightJawBodyMidDist) / faceW`
- `MasseterPositionBalanceScore = abs(leftMasseterDist - rightMasseterDist) / faceW`

Guide:

- `0.00 ~ 0.035`: natural.
- `0.035 ~ 0.060`: weak asymmetry.
- `0.060+`: yaw, shadow, hair, beard, or segmentation error first.

If only one point is abnormal, do not correct. If jaw angle, jaw body, masseter, and contour slices all agree, correction is more reliable.

### Square Jaw Confidence

Suggested confidence:

- `SquareJawMetricConfidence = facePoseConfidence * yawInverseConfidence * pitchConfidence * jawLandmarkConfidence * contourSegmentationConfidence * beardOcclusionInverseConfidence * hairOcclusionInverseConfidence * lightingConfidence * neckSeparationConfidence * resolutionConfidence`

Decision:

- High confidence: square-jaw classification usable; tiny geometry allowed only if user intent supports it.
- Medium confidence: shadow, tone, and mask cleanup only.
- Low confidence: do not classify as real square jaw; no geometry correction.

### Square Jaw Retouch Priority

1. Align face roll, estimate yaw/pitch, check crop and lens distortion.
2. Verify jaw angle landmarks and contour mask.
3. Detect beard, hair, sideburn, collar, and shadow.
4. Calculate jaw ratios, taper profile, straightness, and angle sharpness.
5. Classify cause: bone, masseter, fat/swelling, shadow, pose, beard/hair, narrow-cheek illusion.
6. Apply non-geometry cleanup first: jaw mask smoothing, under-jaw shadow cleanup, neck separation cleanup, beard/hair protection, local tone balancing.
7. Apply small geometry only if confidence is high, multiple metrics agree, identity is preserved, safe limits are respected, and user intent supports natural jaw softening.

### Square Jaw Safe Limits

- Jaw angle width change: `1% ~ 4%` of original `jawAngleW`.
- Jaw body mid/lower change: `1% ~ 4%` of original width.
- Masseter area shape change: `1% ~ 3%` of local `faceW`.
- Chin transition change: `1% ~ 4%` of original `chinTransitionW`.
- Chin width change: `2% ~ 5%` of original `chinW`.
- Mandibular angle softening: small local warp only; avoid removing jaw angle completely.
- Under-jaw shadow cleanup: tone/contrast preferred; geometry usually not needed.
- Jaw-neck separation cleanup: subtle contrast and edge cleanup; avoid hard cutout.

`5%+` square-jaw geometry change is visible. `10%+` jaw reduction is identity-changing. For ID photo, memorial portrait, and restoration, use smaller limits.

Final square-jaw rule:

- Do not define an ideal jaw.
- Do not define square jaw from width alone.
- Define whether the lower face is truly square/blocky, whether the appearance is bone/masseter/fat/shadow/pose/beard/hair/mask error, and whether correction should be mask, shadow, neck separation, tone, or geometry.
- Preferred usage: square jaw detection, over-correction prevention, jaw angle confidence check, cause classification, under-jaw shadow cleanup, jaw/neck separation cleanup, and subtle natural jaw softening only when safe.
- Avoid automatic V-line, automatic square-jaw reduction, heavy mandibular angle removal, narrow lower faces by default, gender/age/identity changes, one-ratio correction, and correction when beard/hair/shadow hides jaw.

## Cheekbone And Zygoma

Cheekbone metrics define zygoma width, cheekbone prominence, cheek hollow, highlight, shadow, symmetry, and cheek-to-jaw transition. They are for natural portrait retouch guidance and over-correction prevention. They are not beauty standards.

Core rules:

- Do not automatically reduce cheekbones.
- Cheekbone correction is identity-sensitive.
- Cheekbone is not only outer face width; it includes bone width, mid-face volume, highlight, shadow, and cheek-to-jaw transition.
- Distinguish real structure from lighting, smile, pose, hair shadow, beard, and makeup contour first.
- Tone, shadow, highlight, makeup, and mask cleanup come before geometry.
- Geometry correction comes last and must be very small.
- Preserve identity, age, facial bone structure, weight impression, and natural asymmetry.

### Cheekbone Preconditions

Before cheekbone analysis:

- Normalize roll using the eye-center line.
- Estimate yaw and pitch.
- Detect face contour, eyes, nose, mouth, jawline, cheek skin mask, cheekbone highlight, cheek hollow shadow, hair overlap, beard, and makeup/contour shading.
- Reduce confidence for yaw, pitch, smile, hair overlap, beard overlap, directional shadow, strong makeup contour, crop, low resolution, blur, unstable face segmentation, or overexposed cheek.

Cheekbone width changes strongly with yaw. Smile raises cheek volume. Makeup contour can mimic cheek hollow. Directional lighting can make one cheek look larger.

Cheek and cheekbone surface-flow guide:

- Zygoma and cheek should be read as different but connected flows.
- Cheekbone flow often runs from the outer under-eye area toward the mouth corner on a diagonal support.
- Mid-cheek flow is softer, rounder, and more cushion-like than the firmer zygoma plane.
- Cheek texture and tone should usually wrap in larger curved surface arcs rather than straight bands.
- Distinguish highlight on the cheekbone, soft cheek body, cheek hollow, and cheek-to-jaw transition before deciding any boundary or correction.

### Cheekbone Regions

Target anchors and masks:

- `leftCheekOuter`, `rightCheekOuter`: widest visible cheek/zygoma outer contour points.
- `leftZygomaPeak`, `rightZygomaPeak`: cheekbone prominence points, often where highlight or width is strongest.
- `leftCheekHighlightCenter`, `rightCheekHighlightCenter`
- `leftCheekHollowCenter`, `rightCheekHollowCenter`
- `leftNasolabialStart`, `rightNasolabialStart`
- `leftCheekSkinMask`, `rightCheekSkinMask`
- `leftCheekboneMask`, `rightCheekboneMask`
- `leftCheekHighlightMask`, `rightCheekHighlightMask`
- `leftCheekShadowMask`, `rightCheekShadowMask`
- `leftCheekHollowMask`, `rightCheekHollowMask`
- `hairOcclusionMask`, `makeupContourMask`, `beardMask`, `shadowMask`
- `skinProtectionMask`, `jawProtectionMask`, `noseProtectionMask`, `mouthProtectionMask`

Cheekbone zones:

- Outer zygoma width: affects face width and identity strongly.
- Zygoma peak: visible by highlight, contour, or bone structure.
- Upper cheek: under outer eye and beside nose; smile-sensitive.
- Cheek highlight: creates lifted/defined cheek impression.
- Cheek hollow: shadow below cheekbone; creates sculpted or thin-face impression.
- Mid-cheek volume: soft tissue between nose, mouth, and zygoma; age and weight-sensitive.
- Cheek-to-jaw transition: important for face shape.
- Nasolabial side cheek: expression-sensitive.

Do not treat cheekbone as a single point. Width, highlight, shadow, volume, and contour must be separated.

### Cheekbone Width And Position

Widths:

- `cheekW = distance(leftCheekOuter, rightCheekOuter)`
- `zygomaPeakW = distance(leftZygomaPeak, rightZygomaPeak)`
- `jawAngleW = distance(leftJawAngle, rightJawAngle)`
- `jawMidW = distance(leftJawBodyMid, rightJawBodyMid)`
- `eyeOuterW = distance(leftEyeOuter, rightEyeOuter)`
- `midFaceW = width of face contour at noseBaseCenter.y`
- `mouthLevelW = width of face contour at mouthCenter.y`

Position:

- `CheekboneVerticalPositionRatio = avgZygomaPeakYRatio`: `0.42 ~ 0.58`

High zygoma peak can read as high cheekbone. Low zygoma peak can read as mid-cheek/nose-base prominence. Smile and pitch can change this, so do not move cheekbone vertically automatically.

### Cheekbone Ratios

- `CheekWidthFaceRatio = cheekW / faceW`: `0.88 ~ 1.00`
- `ZygomaWidthFaceRatio = zygomaPeakW / faceW`: `0.78 ~ 0.96`
- `CheekJawRatio = cheekW / jawAngleW`: `1.10 ~ 1.35`
- `ZygomaJawRatio = zygomaPeakW / jawAngleW`: `1.05 ~ 1.32`
- `CheekChinRatio = cheekW / chinW`: soft guide only.
- `CheekEyeOuterRatio = cheekW / eyeOuterW`: `1.15 ~ 1.45`
- `CheekMidFaceRatio = cheekW / midFaceW`: `1.02 ~ 1.25`

High cheek/jaw ratio can suggest V/heart-like upper face impression. Low cheek/jaw ratio can suggest square or round lower-face impression. Use cheek width mainly as shape reading, not correction target.

### Cheekbone Balance

- `CheekOuterBalanceScore = abs(leftCheekDist - rightCheekDist) / faceW`
  - `0.00 ~ 0.035`: natural.
  - `0.035 ~ 0.060`: weak asymmetry.
  - `0.060+`: yaw, hair, shadow, or contour error first.
- `ZygomaPeakBalanceScore = abs(leftZygomaDist - rightZygomaDist) / faceW`
  - `0.00 ~ 0.035`: natural.
  - `0.035 ~ 0.060`: weak asymmetry.
  - `0.060+`: yaw, lighting, or detection issue first.
- `CheekYBalanceScore = abs(leftCheekOuter.y - rightCheekOuter.y) / faceH`
  - `0.00 ~ 0.020`: natural.
  - `0.020 ~ 0.040`: weak tilt or expression.
  - `0.040+`: pose, roll, smile, or detection issue first.

Cheekbone asymmetry is often pose or lighting. Do not shrink one cheek unless contour and light both support real asymmetry.

### Cheek Prominence, Highlight, And Hollow

Cheekbone prominence should combine geometry, light, and shadow:

- `LeftCheekProminenceScore`
- `RightCheekProminenceScore`
- `CheekProminenceBalanceScore`
- `CheekProjectionVisualScore`

Highlight metrics:

- `LeftCheekHighlightStrength`
- `RightCheekHighlightStrength`
- `CheekHighlightBalanceScore`
- `CheekHighlightAreaRatio`
- `CheekHighlightPositionRatio`

Hollow/shadow metrics:

- `LeftCheekHollowStrength`
- `RightCheekHollowStrength`
- `CheekHollowBalanceScore`
- `CheekHollowAreaRatio`
- `CheekHollowDepthVisualScore`

Strong highlight plus strong hollow shadow can read as prominent/sculpted cheekbone. Shadow without width may be makeup or lighting. Highlight alone may be oily shine or flash. Do not erase all highlights or all cheek hollows; both help facial structure.

### Cheek-To-Jaw Transition

- `CheekToJawTaperRatio = jawAngleW / cheekW`: `0.72 ~ 0.92`
- `CheekToJawMidTaperRatio = jawMidW / cheekW`: `0.62 ~ 0.82`
- `CheekToMouthLevelRatio = mouthLevelW / cheekW`
- `CheekLowerFaceTransitionSmoothness`
- `CheekJawTransitionBalanceScore`

Smooth transition can read natural oval/round. Strong taper can read V/heart-like. Sudden inward pull below cheek can look liquified or artificial. Do not create sudden cheek-to-jaw collapse.

### Expression, Makeup, Hair, And Beard Effects

- `SmileCheekLiftScore`
- `MakeupContourCandidateScore`
- `BlushCandidateScore`
- `HighlighterCandidateScore`
- `HairCheekOverlapRatio`
- `BeardCheekOverlapRatio`
- `ShadowOcclusionCheekScore`
- `CheekContourVisibilityScore`

Smile raises cheeks and changes cheek-mouth distance. Makeup contour can imitate cheekbone structure. Hair, beard, and shadow reduce contour confidence. Preserve makeup unless the user asks.

### Cheek Skin And Texture

Useful metrics:

- `CheekTextureStrength`
- `CheekTextureBalanceScore`
- `CheekBlemishMask`
- `CheekRednessScore`
- `CheekPatchinessScore`
- `CheekSmoothnessScore`

Cheeks often show pores and natural texture. Preserve skin texture, especially in highlight areas. Use local blemish cleanup first, tone balancing second, and texture-preserving smoothing only.

### Cheekbone Confidence

Suggested confidence:

- `CheekboneMetricConfidence = facePoseConfidence * contourLandmarkConfidence * cheekSkinMaskConfidence * highlightShadowConfidence * occlusionInverseConfidence * lightingConfidence * expressionNeutralConfidence * resolutionConfidence`

Decision:

- High confidence: tone/highlight/shadow cleanup allowed; tiny geometry only if multiple metrics agree.
- Medium confidence: tone and shadow cleanup only.
- Low confidence: no cheekbone shape correction; only safe local skin cleanup.

### Cheekbone Retouch Categories

Safe order:

1. Cheek skin cleanup: blemish, redness, patchiness, texture preservation.
2. Highlight control: reduce harsh shine, preserve natural cheekbone highlight.
3. Shadow/hollow cleanup: soften dirty/harsh shadow, preserve structure.
4. Makeup balance: preserve makeup; balance uneven contour/blush only if needed.
5. Cheek contour cleanup: refine mask edge; do not shrink cheek automatically.
6. Cheek volume/geometry: last resort, extremely conservative, high confidence and user intent required.

### Cheekbone Safe Limits

- Cheek outer contour move: `0.5% ~ 1.5%` of `faceW` per side.
- Cheek width change: `1% ~ 3%` of original `cheekW`.
- Zygoma peak move: `0.5% ~ 1.5%` of `faceW` or `faceH`.
- Highlight reduction: reduce harsh shine only; preserve natural brightness.
- Hollow shadow softening: subtle; do not erase face structure.
- Texture smoothing: low to medium; preserve pores/fine texture.
- Redness correction: subtle; preserve natural skin/blush unless requested.
- Makeup contour change: avoid unless requested.

`5%+` cheek geometry change is often visible. `10%+` changes identity strongly. Do not automatically create V-line by pulling cheek and jaw inward.

Final cheekbone rule:

- Do not define an ideal cheekbone.
- Define where cheekbone width and zygoma peak appear, whether prominence is real structure/light/shadow/smile/makeup/hair/mask error, and whether correction is skin, highlight, shadow, contour, or geometry.
- Preferred usage: cheekbone mask refinement, highlight control, hollow shadow softening, texture preservation, makeup-aware correction, left/right balance check, and over-correction prevention.
- Avoid automatic cheekbone reduction, automatic V-line creation, flat highlights, erased hollows, face-width edits from one metric, makeup-as-anatomy, and perfect cheek symmetry.

## Eyes

Eye geometry should prevent over-correction. It is not a beauty-standard definition.

Use all eye metrics after basic roll alignment. If yaw, pitch, blink, smile, squint, glasses, heavy lashes, glare, or blur are strong, reduce eye metric confidence.

### Eye Anchors

Dense landmark target anchors:

- `leftEyeOuter`, `leftEyeInner`
- `leftEyeUpperMid`, `leftEyeLowerMid`
- `leftPupilCenter`, `leftIrisRadius`
- `leftBrowInner`, `leftBrowOuter`, `leftBrowMid`
- `rightEyeInner`, `rightEyeOuter`
- `rightEyeUpperMid`, `rightEyeLowerMid`
- `rightPupilCenter`, `rightIrisRadius`
- `rightBrowInner`, `rightBrowOuter`, `rightBrowMid`
- `noseCenterX`, `noseRoot`, `noseBaseCenter`

Derived eye centers:

- `leftEyeCenter.x = (leftEyeOuter.x + leftEyeInner.x) / 2`
- `leftEyeCenter.y = (leftEyeUpperMid.y + leftEyeLowerMid.y) / 2`
- `rightEyeCenter.x = (rightEyeInner.x + rightEyeOuter.x) / 2`
- `rightEyeCenter.y = (rightEyeUpperMid.y + rightEyeLowerMid.y) / 2`
- `eyeCenterX = (leftEyeCenter.x + rightEyeCenter.x) / 2`
- `eyeCenterY = (leftEyeCenter.y + rightEyeCenter.y) / 2`

Vertical position:

- `eyeY / faceH`: `0.42 ~ 0.48`

Center distance:

- `eyeDist / faceW`: `0.38 ~ 0.46`

Eye size:

- `eyeW / faceW`: `0.18 ~ 0.23`
- `eyeH / eyeW`: `0.25 ~ 0.38`
- `eyeGap / avgEyeW`: `0.90 ~ 1.20`
- `avgIrisD / avgEyeW`: `0.28 ~ 0.42`

Pupil guide:

- `pupilXRatio`: `0.45 ~ 0.55`
- `pupilYRatio`: `0.42 ~ 0.58`
- Example: if visible eye width is about `100 px`, a pupil may appear as a dark circle about `8 px` in diameter inside the iris, and a white catchlight around `4 px x 4 px` may partially cover it.

Do not force pupils to mathematical center. Small asymmetry is natural.

### Eye Core Distances

- `leftEyeW = distance(leftEyeOuter, leftEyeInner)`
- `rightEyeW = distance(rightEyeOuter, rightEyeInner)`
- `avgEyeW = (leftEyeW + rightEyeW) / 2`
- `leftEyeH = distance(leftEyeUpperMid, leftEyeLowerMid)`
- `rightEyeH = distance(rightEyeUpperMid, rightEyeLowerMid)`
- `avgEyeH = (leftEyeH + rightEyeH) / 2`
- `eyeDist = distance(leftEyeCenter, rightEyeCenter)`
- `eyeGap = distance(leftEyeInner, rightEyeInner)`
- `avgIrisD = (leftIrisRadius * 2 + rightIrisRadius * 2) / 2`
- `avgBrowW = (leftBrowW + rightBrowW) / 2`
- `avgBrowEyeDist = (leftBrowEyeDist + rightBrowEyeDist) / 2`

### Eye Position Ratios

- `EyeCenterYRatio = eyeCenterY / faceH`: `0.42 ~ 0.48`
- `EyeDistFaceRatio = eyeDist / faceW`: `0.38 ~ 0.46`
- `EyeGapEyeWidthRatio = eyeGap / avgEyeW`: `0.90 ~ 1.20`
- `AvgEyeWidthFaceRatio = avgEyeW / faceW`: `0.18 ~ 0.23`
- `LeftEyeAspectRatio = leftEyeH / leftEyeW`: `0.25 ~ 0.38`
- `RightEyeAspectRatio = rightEyeH / rightEyeW`: `0.25 ~ 0.38`
- `AvgEyeAspectRatio = avgEyeH / avgEyeW`: `0.25 ~ 0.38`
- `IrisEyeWidthRatio = avgIrisD / avgEyeW`: `0.28 ~ 0.42`
- `BrowWidthEyeWidthRatio = avgBrowW / avgEyeW`: `1.05 ~ 1.35`
- `BrowEyeRatio = avgBrowEyeDist / avgEyeH`: `0.80 ~ 1.80`

### Eye Left/Right Symmetry

- `EyeWidthBalanceScore = abs(leftEyeW - rightEyeW) / avgEyeW`
  - `0.00 ~ 0.06`: natural.
  - `0.06 ~ 0.10`: weak asymmetry.
  - `0.10+`: correction candidate or pose/detection issue.
- `EyeHeightBalanceScore = abs(leftEyeH - rightEyeH) / avgEyeH`
  - `0.00 ~ 0.08`: natural.
  - `0.08 ~ 0.12`: weak asymmetry.
  - `0.12+`: correction candidate or expression/detection issue.
- `EyeLevelScore = abs(leftEyeCenter.y - rightEyeCenter.y) / faceH`
  - `0.00 ~ 0.015`: natural.
  - `0.015 ~ 0.030`: weak tilt/asymmetry.
  - `0.030+`: check face roll, expression, or detection first.
- `EyeCenterToFaceCenterScore = abs(eyeCenterX - faceCenterX) / faceW`
  - `0.00 ~ 0.03`: natural.
  - `0.03 ~ 0.05`: weak offset.
  - `0.05+`: check yaw, crop, or detection first.

### Eye Angle And Tilt

- `EyeLineAngleDeg = atan2(rightEyeCenter.y - leftEyeCenter.y, rightEyeCenter.x - leftEyeCenter.x) * 180 / PI`
- `LeftEyeTiltDeg = atan2(leftEyeInner.y - leftEyeOuter.y, leftEyeInner.x - leftEyeOuter.x) * 180 / PI`
- `RightEyeTiltDeg = atan2(rightEyeOuter.y - rightEyeInner.y, rightEyeOuter.x - rightEyeInner.x) * 180 / PI`
- `EyeTiltBalanceDeg = abs(LeftEyeTiltDeg - RightEyeTiltDeg)`

Guide:

- `0 ~ 3 deg`: natural.
- `3 ~ 6 deg`: weak asymmetry.
- `6+ deg`: correction candidate or expression issue.

### Pupil And Iris Position

- `LeftPupilXRatio = (leftPupilCenter.x - leftEyeOuter.x) / leftEyeW`: `0.45 ~ 0.55`
- `RightPupilXRatio = (rightPupilCenter.x - rightEyeInner.x) / rightEyeW`: `0.45 ~ 0.55`
- `LeftPupilYRatio = (leftPupilCenter.y - leftEyeUpperMid.y) / leftEyeH`: `0.42 ~ 0.58`
- `RightPupilYRatio = (rightPupilCenter.y - rightEyeUpperMid.y) / rightEyeH`: `0.42 ~ 0.58`
- A small bright catchlight can overlap the pupil, so use the dark-circle trend plus iris context together instead of requiring a perfect solid black disk.
- `PupilXBalanceScore = abs(LeftPupilXRatio - RightPupilXRatio)`
  - `0.00 ~ 0.05`: natural.
  - `0.05 ~ 0.10`: possible gaze difference.
  - `0.10+`: side gaze or detection issue first.
- `PupilYBalanceScore = abs(LeftPupilYRatio - RightPupilYRatio)`
  - `0.00 ~ 0.06`: natural.
  - `0.06+`: eyelid asymmetry, gaze, or detection issue.

### Eyelid And Openness

- `LeftUpperScleraRatio = max(0, (leftPupilCenter.y - leftIrisRadius - leftEyeUpperMid.y)) / leftEyeH`
- `LeftLowerScleraRatio = max(0, (leftEyeLowerMid.y - (leftPupilCenter.y + leftIrisRadius))) / leftEyeH`
- `RightUpperScleraRatio = max(0, (rightPupilCenter.y - rightIrisRadius - rightEyeUpperMid.y)) / rightEyeH`
- `RightLowerScleraRatio = max(0, (rightEyeLowerMid.y - (rightPupilCenter.y + rightIrisRadius))) / rightEyeH`

Large lower sclera can be widened eyes, surprise, fear expression, or lower-lid asymmetry. Small eye aspect ratio can be smile, sleepy eyes, or blinking. Do not treat expression-driven openness as a defect automatically.

### Brow Relation From Eye Metrics

- `LeftBrowEyeRatio = leftBrowEyeDist / leftEyeH`: `0.80 ~ 1.80`
- `RightBrowEyeRatio = rightBrowEyeDist / rightEyeH`: `0.80 ~ 1.80`
- `BrowEyeBalanceScore = abs(LeftBrowEyeRatio - RightBrowEyeRatio)`
  - `0.00 ~ 0.20`: natural.
  - `0.20+`: brow asymmetry, expression, or detection issue.
- `LeftBrowSlopeDeg = atan2(leftBrowOuter.y - leftBrowInner.y, leftBrowOuter.x - leftBrowInner.x) * 180 / PI`
- `RightBrowSlopeDeg = atan2(rightBrowOuter.y - rightBrowInner.y, rightBrowOuter.x - rightBrowInner.x) * 180 / PI`
- `BrowSlopeBalanceDeg = abs(LeftBrowSlopeDeg - RightBrowSlopeDeg)`
  - `0 ~ 5 deg`: natural.
  - `5+ deg`: visible brow asymmetry or expression.

### Eye To Nose

- `EyeNoseCenterScore = abs(eyeCenterX - noseCenterX) / faceW`
  - `0.00 ~ 0.03`: natural.
  - `0.03 ~ 0.05`: weak offset.
  - `0.05+`: yaw, nose asymmetry, or detection issue.
- `LeftEyeToNoseDist = distance(leftEyeInner, noseRoot)`
- `RightEyeToNoseDist = distance(rightEyeInner, noseRoot)`
- `EyeNoseBalanceScore = abs(LeftEyeToNoseDist - RightEyeToNoseDist) / avgEyeW`
  - `0.00 ~ 0.08`: natural.
  - `0.08+`: yaw, nose offset, or asymmetry.

### Eye Confidence Rules

Reduce confidence when:

- One eye is occluded.
- Head yaw or pitch is large.
- Blink, smile, or squint is strong.
- Glasses rims block eyelid corners.
- Heavy eyeliner/lashes distort upper eyelid edge.
- Hair shadow covers brow or outer corner.
- Iris boundary is weak due to blur or glare.

Suggested confidence:

- `EyeMetricConfidence = frontalPoseConfidence * landmarkConfidence * irisDetectionConfidence * visibilityConfidence`

### Eye Retouch Priority

1. Correct face roll/basic alignment.
2. Verify landmark confidence.
3. Analyze eye level and eye size balance.
4. Analyze pupil/gaze position.
5. Analyze brow-eye relation.
6. Analyze eye-nose relation.
7. Apply minimal shape correction only if multiple metrics agree.
8. Prefer tone/shadow/highlight cleanup over geometric deformation.

Eye shape changes are highly visible. Preserve expression. Do not force both eyes to identical geometry.

### Eye Safe Correction Limits

- Eye center move: `0.5% ~ 1.5%` of `faceW`.
- Eye width change: `3% ~ 6%` of original eye width.
- Eye height change: `3% ~ 8%` of original eye height.
- Eye tilt correction: `1 ~ 3 deg` usually safe; `3 ~ 5 deg` only with high confidence.
- Pupil move: `2% ~ 4%` of eye width.
- Brow vertical move: `1% ~ 3%` of `faceH` or `5% ~ 12%` of `eyeH`.

General rule: geometry correction should be weaker than user expectation. If uncertain, preserve original shape.

### Eyelash And Eye Detail Protection

Eyelashes are high-frequency eye detail, not skin. Use eyelash metrics mainly for protection and over-correction prevention. Do not automatically lengthen, darken, thicken, or reshape eyelashes.

Core rules:

- Eyelash protection comes before eye and skin correction.
- Eyelash beautification is off by default.
- Geometry and density changes require explicit user intent.
- Preserve natural lash direction, density, age, makeup style, lower lashes, lash shadows, and identity.
- Do not treat eyelashes as wrinkles, blemishes, eyeliner, skin texture, or dark circles.

Before eyelash analysis:

- Detect eye landmarks first.
- Detect upper/lower eyelids, iris, pupil, sclera, eyelid skin, eyeliner, mascara, and eye shadow if possible.
- Normalize roll using the eye-center line.
- Reduce confidence if the eye is closed, blinking, squinting, blurred, covered by hair, glasses, shadow, or heavy makeup.

Target masks:

- `leftUpperEyelashMask`, `leftLowerEyelashMask`
- `rightUpperEyelashMask`, `rightLowerEyelashMask`
- `leftUpperLashRootLine`, `leftLowerLashRootLine`
- `rightUpperLashRootLine`, `rightLowerLashRootLine`
- `leftEyelinerMask`, `rightEyelinerMask`
- `leftMascaraMask`, `rightMascaraMask`
- `leftEyelashShadowMask`, `rightEyelashShadowMask`
- `underEyeSkinMask`
- `eyelidSkinProtectionMask`
- `scleraProtectionMask`, `irisProtectionMask`, `pupilProtectionMask`
- `hairOcclusionMask`, `glassesOcclusionMask`, `makeupShadowMask`

Eyelash zones:

- Upper lash root: lash base along upper eyelid, easily confused with eyeliner.
- Upper lash body: main visible upper lashes, strongest high-frequency detail.
- Upper lash tips: thin semi-transparent ends, easily destroyed by smoothing or erosion.
- Lower lash root: easily confused with under-eye wrinkles or dark circles.
- Lower lash tips: fine short strands; must be protected during dark-circle correction.
- Mascara/clump region: thick grouped lashes; avoid solid black blocks.
- Lash shadow: shadow cast by lashes; should not always be removed.

Useful metrics:

- `UpperLashLengthAvg`
- `LowerLashLengthAvg`
- `UpperLashDensity`
- `LowerLashDensity`
- `UpperLashThicknessAvg`
- `LowerLashThicknessAvg`
- `UpperLashSharpness`
- `LowerLashSharpness`
- `UpperLashContrast`
- `LowerLashContrast`
- `UpperLashDirectionField`
- `LowerLashDirectionField`
- `LashDirectionCoherence`

Position and overlap:

- `UpperLashToEyeHeightRatio = UpperLashLengthAvg / eyeH`: `0.15 ~ 0.45`
- `LowerLashToEyeHeightRatio = LowerLashLengthAvg / eyeH`: `0.05 ~ 0.25`
- `UpperLashEyeWidthCoverage`
- `LowerLashEyeWidthCoverage`
- `LashScleraOverlapRatio`
- `LashIrisOverlapRatio`
- `LashUnderEyeOverlapRatio`
- `LashEyelidOverlapRatio`

Some overlap with sclera or iris is natural. Do not erase lashes that cross the eye white area. If lash overlap is high, reduce eye whitening and under-eye smoothing strength locally.

Upper/lower balance:

- `UpperLowerLashLengthRatio = UpperLashLengthAvg / LowerLashLengthAvg`: `1.8 ~ 4.5`
- `UpperLowerLashDensityRatio = UpperLashDensity / LowerLashDensity`: `1.5 ~ 4.0`
- `LeftRightUpperLashBalanceScore`
- `LeftRightLowerLashBalanceScore`
- `LeftRightLashLengthBalanceScore`

Perfect eyelash symmetry is not required. Differences can come from lighting, blink, gaze, mascara, blur, or pose.

Makeup separation:

- `EyelinerMask`: dark continuous line along eyelid edge.
- `MascaraMask`: dark thickened regions attached to lash strands.
- `EyelashMask`: thin directional strand-like structures extending from eyelid root.
- `EyelinerSeparationScore`
- `MascaraClumpScore`
- `MascaraSmudgeScore`
- `MascaraOverdarkScore`

Eyeliner, mascara, lashes, and eye shadow must be separated. Do not treat all dark eye pixels as eyelashes.

Lash shadow:

- `UpperLashShadowScore`
- `LowerLashShadowScore`
- `LashShadowDirectionConsistency`
- `LashShadowBlemishConfusionScore`

Lash shadows are natural. Soften harsh accidental smudge/shadow, but preserve natural lash shadow for realism.

Texture and detail:

- `LashTextureFrequency`
- `LashTextureStrength`
- `LashClarityScore`
- `LashBlurScore`
- `LashOverSharpenScore`
- `LashNoiseConfusionScore`

Eyelashes should be clear but not crunchy. Over-sharpened lashes look artificial; over-smoothed lashes disappear.

Protection rules:

- During skin smoothing, protect upper/lower eyelash masks, root lines, and tips with soft alpha.
- During under-eye correction, protect lower lashes and partially protect lower lash shadow.
- During eye whitening, protect lashes overlapping sclera; do not paint white over lashes.
- During iris/pupil enhancement, protect foreground lashes crossing iris/pupil.
- During eyelid correction, separate eyelid skin from lash root.
- During makeup correction, separate eyeliner, mascara, eyelashes, and eye shadow.

Confidence:

- `EyelashMetricConfidence = eyeLandmarkConfidence * eyelidLandmarkConfidence * lashMaskConfidence * makeupSeparationConfidence * resolutionConfidence * blurInverseConfidence * occlusionInverseConfidence * lightingConfidence`

Decision:

- High confidence: protect lashes and allow minor detail cleanup.
- Medium confidence: protect detected lash areas; avoid geometry/density changes.
- Low confidence: do not modify lash shape; only avoid damaging eye detail.

Safe limits:

- Eyelash geometry change: avoid by default.
- Upper lash length change: `0%` by default; if user-requested beauty retouch, `2% ~ 6%` max.
- Lower lash length change: `0%` by default; if user-requested beauty retouch, `1% ~ 4%` max.
- Lash darkening: subtle; avoid solid black blocks.
- Lash sharpening: very low; avoid halos and crunchy edges.
- Lash smoothing: do not smooth strand detail; remove only noise/artifacts not aligned with lash direction.
- Mascara smudge reduction: local only; preserve intended makeup.
- Lash shadow softening: subtle; do not remove natural shadow completely.
- Lash density increase: avoid by default; if requested, texture-aware, strand-like, low opacity.

Final eyelash rule:

- Do not define ideal eyelashes.
- Define where upper/lower lashes, root, body, tips, mascara, eyeliner, shadow, and occlusion exist.
- Preferred usage: eye-detail protection, lower-lash protection during dark-circle correction, whitening protection, mascara smudge detection, over-sharpening prevention, and makeup-aware eye retouch.
- Avoid automatic lash lengthening, darkening, perfect symmetry, lower-lash erasure, white painting over lashes, and solid black lash lines.

### Dark Circle And Under-Eye Shadow

Dark-circle metrics define under-eye darkness, lower eyelid shadows, tear troughs, eye bag shadows, pigmentation, vascular blue/purple tone, lower-lash shadow, makeup shadow, and glasses shadow. This module corrects color and shadow components; it does not reshape eye volume.

Core rules:

- Dark circle is not one problem.
- Do not simply brighten the under-eye area.
- Do not erase lower eyelid structure.
- Do not erase lower eyelashes.
- Do not flatten the tear trough completely.
- Correct color, shadow, and texture separately.
- Preserve natural under-eye volume, age, expression, thin skin texture, and eye identity.
- Geometry correction is off by default.

Required masks:

- `leftUnderEyeMask`, `rightUnderEyeMask`
- `leftLowerEyelidMask`, `rightLowerEyelidMask`
- `leftTearTroughMask`, `rightTearTroughMask`
- `leftEyeBagMask`, `rightEyeBagMask`
- `leftUnderEyePigmentationMask`, `rightUnderEyePigmentationMask`
- `leftUnderEyeVascularMask`, `rightUnderEyeVascularMask`
- `leftUnderEyeShadowMask`, `rightUnderEyeShadowMask`
- `leftLowerLashShadowMask`, `rightLowerLashShadowMask`
- `leftUnderEyeWrinkleMask`, `rightUnderEyeWrinkleMask`
- `leftCrowFeetMask`, `rightCrowFeetMask`
- `glassesShadowMask`
- `makeupShadowMask`
- `hairShadowMask`

Protection masks:

- `eyeProtectionMask`
- `scleraProtectionMask`
- `irisProtectionMask`
- `pupilProtectionMask`
- `upperEyelashProtectionMask`
- `lowerEyelashProtectionMask`
- `eyelinerProtectionMask`
- `eyeshadowProtectionMask`
- `lowerEyelidCreaseProtectionMask`
- `skinTextureProtectionMask`
- `wrinkleProtectionMask`

Under-eye structure zones:

- Lower eyelid edge: directly below eye opening; lower lash line and lower eyelid crease must be protected.
- Tear trough: diagonal hollow from inner eye downward/outward; soften, do not erase.
- Eye bag: soft bulge below lower eyelid; adjust highlight/shadow, do not flatten.
- Under-eye skin: thin color-correction area; texture must remain.
- Outer under-eye / crow's feet: expression lines; preserve natural eye expression.
- Lower lash shadow: lower-lash darkness; not a dark-circle defect.
- Makeup/glasses shadow: external darkening source; classify before correction.

Common dark-circle types:

- `pigmentationDarkCircle`: brown or gray-brown soft patch; color-correct gently.
- `vascularDarkCircle`: blue, purple, red-purple, or gray-blue tone from thin skin/blood visibility; neutralize color, avoid heavy brightening.
- `hollowShadowDarkCircle`: tear-trough or volume-depression shadow; soften shadow, not structure.
- `eyeBagShadow`: shadow below puffiness; preserve volume.
- `fatigueDarkCircle`: diffuse mixed color/shadow; moderate correction.
- `makeupShadow`: eyeshadow, mascara smudge, or eyeliner migration; preserve intentional makeup.
- `lowerLashShadow`: lower-lash line/dots; protect.
- `glassesShadow`: frame/lens cast shadow; separate from skin pigmentation.
- `photoDamageDarkness`: old photo stain, scan damage, or compression artifact; restore only if confirmed.
- `mixedDarkCircle`: multiple causes; correct each component separately.

Primary metrics:

- `UnderEyeDarknessScore = surroundingCheekLuminance - underEyeLuminance`
- `UnderEyeColorDelta`
- `UnderEyeBrownnessScore`
- `UnderEyeBluePurpleScore`
- `UnderEyeRednessScore`
- `UnderEyeSaturationScore`
- `UnderEyeShadowDepthScore`
- `TearTroughDepthScore`
- `EyeBagShadowScore`
- `UnderEyeAreaRatio`
- `UnderEyeTextureStrength`
- `UnderEyeWrinkleOverlapScore`
- `LowerLashOverlapScore`
- `MakeupOverlapScore`
- `GlassesShadowOverlapScore`

Left/right balance:

- `LeftDarkCircleScore`
- `RightDarkCircleScore`
- `DarkCircleBalanceScore = abs(LeftDarkCircleScore - RightDarkCircleScore)`
- `LeftRightColorBalanceScore`
- `LeftRightShadowBalanceScore`

Do not force both under-eye areas identical. Asymmetry can come from lighting, pose, glasses shadow, makeup, or real facial asymmetry.

Classification rule:

- Brown/gray-brown dominant: `pigmentationDarkCircle`.
- Blue/purple/red-purple dominant: `vascularDarkCircle`.
- Diagonal hollow from inner eye downward: `hollowShadowDarkCircle`.
- Shadow below lower eyelid bulge: `eyeBagShadow`.
- Overlap with lower eyelashes: `lowerLashShadow`.
- Overlap with eyeshadow/mascara/eyeliner: `makeupShadow` or `mascaraSmudge`.
- Alignment with glasses frame: `glassesShadow`.
- Scratch/stain/compression pattern: `photoDamageDarkness`.
- Multiple causes: `mixedDarkCircle`.

Pigmentation dark circle:

- `PigmentationUnderEyeMask`
- `PigmentationDepthScore`
- `PigmentationSoftEdgeScore`
- Reduce brown/gray cast toward nearby cheek tone.
- Preserve depth and thin skin texture.
- Safe limits: pigmentation color reduction `20% ~ 60%`; brightness lift low to medium; no flat white under-eye.

Vascular dark circle:

- `VascularUnderEyeMask`
- `VascularBluePurpleScore`
- `VascularThinSkinScore`
- Neutralize blue/purple gently.
- Avoid yellow, gray, or orange overcorrection.
- Safe limits: blue/purple neutralization `20% ~ 55%`; brightness lift low.

Tear trough:

- `TearTroughMask`
- `TearTroughDepthScore`
- `TearTroughContinuityScore`
- `TearTroughSymmetryScore`
- Tear trough is a structural line. Reduce harsh diagonal darkness while preserving under-eye contour.
- Safe limits: tear-trough shadow reduction `10% ~ 35%`; deep trough `10% ~ 25%`; geometry off by default.

Eye bag:

- `EyeBagMask`
- `EyeBagHighlightScore`
- `EyeBagLowerShadowScore`
- `EyeBagVolumeScore`
- `EyeBagShadowScore`
- Soften lower shadow and only subtly reduce harsh highlight.
- Preserve lower eyelid structure and do not flatten the entire under-eye.
- Safe limits: eye-bag shadow reduction `10% ~ 35%`; highlight reduction subtle; volume geometry off by default.

Lower lash, eyeliner, and mascara:

- `LowerLashShadowMask`
- `MascaraSmudgeMask`
- `EyelinerBleedMask`
- Lower eyelashes must be protected before dark-circle correction.
- Do not brighten over lower lashes.
- Do not remove lash dots as blemishes.
- Do not erase natural lower-lash shadow completely.
- If accidental mascara smudge is detected, local cleanup is allowed while preserving intentional makeup style.

Under-eye wrinkle interaction:

- `UnderEyeWrinkleMask`
- `WrinkleDarkCircleOverlapScore`
- Dark-circle correction must not globally blur under-eye wrinkles.
- Wrinkle module controls line softening.
- Dark-circle module controls color and shadow.
- Preserve thin skin texture.

Glasses and makeup:

- `GlassesShadowMask`: soften cast shadow only; protect glasses frame and do not treat frame shadow as pigmentation.
- `MakeupShadowMask`: preserve intentional eye makeup; clean only accidental smudge or patchiness.

Texture rules:

- Preserve fine under-eye texture, lower eyelid crease, natural transition, and eye expression.
- Restore texture after color correction if a patch becomes too smooth.
- Do not blur globally, remove all fine lines, erase lower lashes, create a flat bright crescent, or make under-eye skin smoother than cheeks.

Confidence:

- `DarkCircleMetricConfidence = underEyeMaskConfidence * eyeLandmarkConfidence * lowerLashMaskConfidence * lightingConfidence * makeupSeparationConfidence * glassesShadowInverseConfidence * textureConfidence * resolutionConfidence * poseConfidence`

Reduce confidence when glasses shadow is strong, makeup is heavy, lower lashes overlap strongly, resolution is low, blur/overexposure/underexposure exists, lighting is directional, yaw/pitch is high, squint/smile is strong, eye is partly closed, hair crosses eye, or old photo damage exists.

Decision:

- High confidence: component-based color/shadow correction allowed.
- Medium confidence: gentle tone balancing only.
- Low confidence: do not brighten aggressively; protect eye detail and avoid structure changes.

Safe limits:

- `PigmentationColorReduction`: `20% ~ 60%`.
- `VascularColorNeutralization`: `20% ~ 55%`.
- `GeneralUnderEyeBrightnessLift`: `5% ~ 20%`.
- `TearTroughShadowReduction`: `10% ~ 35%`.
- `EyeBagShadowReduction`: `10% ~ 35%`.
- `LowerLashShadowReduction`: `0% ~ 15%`, usually preserve.
- `MascaraSmudgeCleanup`: local only if accidental.
- `UnderEyeWrinkleSoftening`: handled by wrinkle module.
- `TextureSmoothing`: very low only.
- `FullDarkCircleRemoval`: not allowed.

Retouch priority:

1. Detect under-eye mask, lower eyelid, lower lashes, sclera, iris, makeup, and glasses shadow.
2. Protect eye details.
3. Classify dark-circle type: pigmentation, vascular, tear-trough shadow, eye-bag shadow, lower-lash shadow, makeup, glasses, photo damage, or mixed.
4. Correct by component: pigmentation color cast, vascular blue/purple, tear-trough shadow, eye-bag shadow, accidental smudge, or glasses cast shadow.
5. Preserve lower eyelid structure, lower lashes, eye wrinkles, skin texture, natural under-eye depth, and age consistency.
6. Check for no flat bright patch, no erased lower lashes, no plastic skin, no yellow/gray/orange overcorrection, and no lost eye expression.

Scoring:

- `DarkCircleScore = UnderEyeDarknessScore + UnderEyeColorDelta + UnderEyeShadowDepthScore + UnderEyeAreaRatio`
- `PigmentationDarkCircleScore = UnderEyeBrownnessScore + pigmentation depth + soft-edged patch confidence`
- `VascularDarkCircleScore = UnderEyeBluePurpleScore + vascular thin-skin confidence`
- `TearTroughScore = TearTroughDepthScore + TearTroughContinuityScore`
- `EyeBagShadowScore = EyeBagLowerShadowScore + EyeBagVolumeScore`
- `LashShadowConfusionScore = LowerLashOverlapScore + lower lash darkness`
- `MakeupConfusionScore = MakeupOverlapScore + makeup intentional score`
- `GlassesShadowConfusionScore = GlassesShadowOverlapScore + frame-aligned shadow`
- `CorrectionStrength = baseStrength * DarkCircleMetricConfidence * componentConfidence * texturePreservationWeight * agePreservationWeight`

Final dark-circle rule:

- Do not define dark circle as simply a dark area under eye.
- Define whether it is pigmentation, vascular color, tear-trough shadow, eye-bag shadow, lower-lash shadow, makeup, glasses shadow, or artifact.
- Preferred usage: classification, color cast reduction, tear-trough shadow softening, eye-bag shadow softening, lower-lash protection, makeup/glasses-aware correction, and texture-preserving under-eye retouch.
- Avoid aggressive brightening, lower eyelid erasure, lower-lash removal, complete tear-trough flattening, white crescent under eyes, global blur, makeup-as-skin correction, and eye shape/volume changes in this module.

## Brows

Eyebrow geometry controls expression strongly. Use brow metrics for mask refinement, density balancing, patchiness cleanup, and over-correction prevention. Do not use them as fixed beauty standards.

Core rules:

- Do not force both eyebrows to be identical.
- Do not automatically lift, thicken, or reshape brows.
- Preserve identity, age, expression, gender impression, and natural asymmetry.
- Determine whether asymmetry comes from expression, pose, hair, glasses, makeup, lighting, or mask error before any shape change.
- Brow geometry correction comes last. Density, edge, color, and mask cleanup come first.

### Brow Preconditions

Before brow analysis:

- Normalize roll using the eye-center line.
- Verify eye landmarks first.
- Verify brow mask quality.
- Reduce confidence for yaw, pitch, bangs, hair overlap, glasses, makeup, shadow, low contrast, blur, sparse brows, tattooed/drawn brows, or directional lighting.

Brow landmarks are less stable than eye landmarks. Brow hair is soft, broken, semi-transparent, and lighting-sensitive. Do not reshape brows if the brow mask is unstable.

In the 3D/AnchorMesh guide, an eyebrow can be described as a 30-point closed free polygon around the visible brow hair mass. This is guide geometry only. The mask itself should stay evidence-only and should not be painted by a generated brush. Its angle, length, arch height, density, continuity, and thickness can vary naturally; the head is often thicker, the tail may fade, and some sections can be broken or missing. If a real brow hair bundle is not visible, the model should report low or missing evidence instead of drawing a guessed brow.

Eyebrow detection must be orbit-guided:

- Estimate `orbitalCenter` from `eyeCenter` and `irisCenter`/`pupilCenter` when available.
- Use `eyeW = distance(eyeInner, eyeOuter)` and `eyeH = distance(eyeUpperMid, eyeLowerMid)` as the local size basis.
- Build a soft upper orbital arc above `eyeUpperMid`; practical search range is roughly `0.25 ~ 1.60 * eyeH` above the upper eyelid with a face-ratio safety band.
- Brow candidates should be slightly wider than the visible eye, usually `1.15 ~ 1.45 * eyeW`, and should sit near the upper orbital arc rather than in a full face box.
- Reject candidates that are too close to the eyelid, too high in the forehead, only a thin floating line, mostly eyelash/eyelid shadow, or lacking hair-like texture.
- Use the nose wing to inner corner, outer iris edge, and outer corner lines as soft head / arch / tail placement guides, not as forced beauty-template lines.

### Brow Anchors

Dense landmark target anchors:

- `leftBrowHead`, `leftBrowMid`, `leftBrowArch`, `leftBrowTail`
- `leftBrowUpperHead`, `leftBrowLowerHead`, `leftBrowUpperArch`, `leftBrowLowerArch`
- `rightBrowHead`, `rightBrowMid`, `rightBrowArch`, `rightBrowTail`
- `rightBrowUpperHead`, `rightBrowLowerHead`, `rightBrowUpperArch`, `rightBrowLowerArch`
- `leftBrowContourPoints[]`, `rightBrowContourPoints[]`
- `leftBrowUpperContourPoints[]`, `leftBrowLowerContourPoints[]`
- `rightBrowUpperContourPoints[]`, `rightBrowLowerContourPoints[]`
- `leftBrowMask`, `rightBrowMask`
- `foreheadSkinMask`, `upperEyelidMask`, `hairOcclusionMask`, `glassesOcclusionMask`, `shadowMask`

Primary derived centers:

- `leftBrowCenter.x = (leftBrowHead.x + leftBrowTail.x) / 2`
- `leftBrowCenter.y = (leftBrowHead.y + leftBrowTail.y + leftBrowArch.y) / 3`
- `rightBrowCenter.x = (rightBrowHead.x + rightBrowTail.x) / 2`
- `rightBrowCenter.y = (rightBrowHead.y + rightBrowTail.y + rightBrowArch.y) / 3`
- `browCenterX = (leftBrowCenter.x + rightBrowCenter.x) / 2`
- `browCenterY = (leftBrowCenter.y + rightBrowCenter.y) / 2`

### Brow Zones

Divide eyebrows into zones:

- Head: inner brow near nose root; controls serious/soft expression.
- Body: main brow body; controls thickness and density.
- Arch: highest or most curved point; controls expression and age impression.
- Tail: outer brow end; controls lift, tired look, and eye shape impression.
- Upper boundary: top brow edge.
- Lower boundary: edge near eyelid.
- Density mask: visible brow hair or pigment area.

Do not treat the eyebrow as one straight line. Brow head, arch, body, and tail must be analyzed separately.

### Brow Position

- `BrowCenterYRatio = browCenterY / faceH`: `0.32 ~ 0.40`
- `LeftBrowYRatio = leftBrowCenter.y / faceH`: `0.32 ~ 0.40`
- `RightBrowYRatio = rightBrowCenter.y / faceH`: `0.32 ~ 0.40`
- `BrowEyeRatio = avgBrowEyeDist / avgEyeH`: `0.80 ~ 1.80`
- `LeftBrowEyeRatio = leftBrowEyeDist / leftEyeH`: `0.80 ~ 1.80`
- `RightBrowEyeRatio = rightBrowEyeDist / rightEyeH`: `0.80 ~ 1.80`
- `BrowEyeFaceRatio = avgBrowEyeDist / faceH`: `0.025 ~ 0.065`

Low brow-eye ratio can look heavier, serious, tired, or masculine. High brow-eye ratio can look open, surprised, soft, or aged. Do not lift brows automatically.

### Brow Width And Tail

- `BrowWidthEyeWidthRatio = avgBrowW / avgEyeW`: `0.95 ~ 1.40`
- `LeftBrowWidthEyeRatio = leftBrowW / leftEyeW`: `0.95 ~ 1.40`
- `RightBrowWidthEyeRatio = rightBrowW / rightEyeW`: `0.95 ~ 1.40`
- `BrowWidthFaceRatio = avgBrowW / faceW`: `0.20 ~ 0.32`
- `BrowTailExtensionRatio = distance(browTail, eyeOuter) / eyeW`: `0.05 ~ 0.30`
- `BrowHeadToEyeInnerRatio = distance(browHead, eyeInner) / eyeW`: `0.00 ~ 0.25`

The brow usually starts near or slightly inside the inner eye corner and extends slightly beyond the outer eye corner. Extending the tail should be subtle and should use original density/color signal when possible.

### Brow Thickness

- `BrowThicknessEyeHeightRatio = AvgBrowThickness / avgEyeH`: `0.12 ~ 0.85`
- `BrowThicknessEyeWidthRatio = AvgBrowThickness / avgEyeW`: `0.06 ~ 0.16`
- `HeadToTailThicknessRatio = browHeadThickness / browTailThickness`: `1.10 ~ 2.20`

The brow head is usually thicker than the brow tail, and the tail usually tapers. Prefer density balancing over geometric thickening.

### Brow Arch And Slope

- `BrowArchDepthRatio = AvgBrowArchDepth / avgBrowW`: `0.03 ~ 0.12`
- `ArchPositionRatio = distance(browHead, browArch) / browW`: `0.55 ~ 0.75`
- `BrowSlopeBalanceDeg = abs(LeftBrowSlopeDeg - RightBrowSlopeDeg)`
  - `0 ~ 5 deg`: natural.
  - `5 ~ 10 deg`: weak asymmetry.
  - `10+ deg`: expression, pose, or detection issue first.

Do not create a high arch automatically. Do not over-symmetrize arch position.

### Brow Left/Right Balance

- `BrowHeightBalanceScore = abs(leftBrowCenter.y - rightBrowCenter.y) / faceH`
  - `0.00 ~ 0.015`: natural.
  - `0.015 ~ 0.030`: weak asymmetry.
  - `0.030+`: expression, pose, or detection issue first.
- `BrowWidthBalanceScore = abs(leftBrowW - rightBrowW) / avgBrowW`
  - `0.00 ~ 0.08`: natural.
  - `0.08 ~ 0.15`: weak asymmetry.
  - `0.15+`: detection, occlusion, or true asymmetry.
- `BrowThicknessBalanceScore = abs(LeftAvgBrowThickness - RightAvgBrowThickness) / AvgBrowThickness`
  - `0.00 ~ 0.12`: natural.
  - `0.12 ~ 0.22`: weak density/thickness asymmetry.
  - `0.22+`: makeup, lighting, occlusion, or true asymmetry.
- `BrowEyeBalanceScore = abs(LeftBrowEyeRatio - RightBrowEyeRatio)`
  - `0.00 ~ 0.20`: natural.
  - `0.20 ~ 0.35`: weak asymmetry.
  - `0.35+`: expression, pose, or detection issue.

Perfect brow symmetry can look artificial. Minor asymmetry is normal and identity-preserving.

### Brow Density And Color

Useful future metrics:

- `LeftBrowDensity`, `RightBrowDensity`, `AvgBrowDensity`
- `BrowDensityBalanceScore`
- `BrowSkinContrast`
- `BrowEdgeSoftness`
- `BrowPatchinessScore`
- `BrowTailFadeScore`
- `BrowHeadHarshnessScore`

Eyebrows are hair, not solid shapes. Natural brows have density variation, a softer head, and a tail fade. Avoid painted-looking fills, flat masks, and over-darkening.
They can also be partially disconnected or sparse. Preserve those gaps unless the user explicitly requests brow fill or restoration.

### Brow Masks

Recommended masks:

- Left/right full brow masks.
- Brow head, body, arch, and tail masks.
- Brow upper/lower edge masks.
- 30-point brow bundle guide polygon with upper/lower contour points.
- Forehead protection mask.
- Upper eyelid protection mask.
- Hair/glasses occlusion masks.
- Shadow mask.

Shape correction uses brow contour only with high confidence. Density correction uses brow masks. Forehead and upper eyelid masks prevent color bleeding.

### Brow Confidence

Suggested confidence:

- `BrowMetricConfidence = frontalPoseConfidence * eyeLandmarkConfidence * browLandmarkConfidence * browMaskConfidence * occlusionInverseConfidence * lightingConfidence * textureConfidence`

Decision:

- High confidence: small density and geometry correction allowed.
- Medium confidence: density/tone cleanup only, very small geometry if multiple metrics agree.
- Low confidence: no brow shape correction; only local cleanup if safe.

### Brow Retouch Priority

1. Correct face roll.
2. Verify eye landmarks and brow mask.
3. Check pose, hair, glasses, shadow, and makeup.
4. Classify brow state: natural, sparse, makeup-heavy, occluded, asymmetric, expression-raised, tail-missing, over-dark, or low-contrast.
5. Analyze brow-eye distance, height balance, width/length, thickness, arch, tail, density, and patchiness.
6. Apply non-geometry cleanup first: density balancing, patchiness reduction, edge softening, tail restoration if original signal exists, color/tone adjustment.
7. Apply small geometry only if confidence is high, multiple metrics agree, expression is neutral, identity is preserved, and limits are respected.

### Brow Safe Limits

- Brow vertical move: `0.5% ~ 1.5%` of `faceH`.
- Brow horizontal move: `0.5% ~ 1.5%` of `faceW`.
- Brow width change: `2% ~ 6%` of original `browW`.
- Brow thickness change: `3% ~ 8%` of original thickness.
- Brow arch height change: `3% ~ 8%` of original arch depth or very small only.
- Brow tail extension: `2% ~ 6%` of original `browW`.
- Brow head move: very small only, `0.5% ~ 1.0%` of `faceW`.
- Brow density and darkening: subtle; preserve hair texture and natural gradient.

`10%+` brow geometry change is expression-changing. For ID photo, memorial portrait, restoration, and natural retouch, use smaller limits.

### Brow Shape Reading

Use only to understand brow geometry, not as a target:

- Straight brow: low arch depth, flatter slope.
- Arched brow: higher arch depth, arch usually `55% ~ 75%` from head.
- Downturned tail: can look tired, sad, aged, or natural.
- Lifted tail: can look sharp, lifted, stylized, or expressive.
- Sparse brow: low density, high patchiness, low edge confidence.
- Makeup-heavy brow: high density, strong edge contrast, low texture variation.

Final brow rule:

- Do not define an ideal eyebrow.
- Define where the real eyebrow is, whether it is visible or hidden, whether the issue is shape/density/color/mask error, and whether correction is safe.

## Forehead

Forehead metrics define upper-face skin, hairline relation, brow relation, wrinkles, shine, shadow, texture, and occlusion. They are for natural portrait retouch guidance and over-correction prevention. They are not beauty standards.

Core rules:

- Forehead is affected by hairline, eyebrows, expression, age, lighting, and camera angle.
- Do not automatically shorten, enlarge, smooth, flatten, or reshape the forehead.
- First separate forehead skin from hair, eyebrows, shadows, glasses, and background.
- Skin/tone cleanup comes before geometry.
- Geometry correction and hairline movement are avoided by default.
- Preserve identity, age, expression, natural forehead height, wrinkles, and hairline character.

### Forehead Preconditions

Before forehead analysis:

- Detect face landmarks, eyebrows, hairline, hair mask, forehead skin mask, wrinkles, shine, shadow, and pose.
- Check whether the forehead is fully visible, partly covered by bangs, cropped, overexposed, or shadowed.
- Reduce confidence if bangs cover the forehead, hairline is unclear, head pitch is strong, shine hides texture, or shadows hide the skin boundary.

Forehead height is strongly affected by hairstyle and hairline. Do not judge forehead ratios if hairline confidence is low.

### Forehead Regions

Target anchors and masks:

- `leftBrowCenter`, `rightBrowCenter`, `browCenterY`
- `leftBrowUpperContour`, `rightBrowUpperContour`
- `noseRoot`, `glabella`
- `hairlineCenter`, `hairlineLeftTemple`, `hairlineRightTemple`
- `foreheadTopCenter = hairlineCenter`
- `foreheadBottomCenter = glabella or browCenterY region`
- `foreheadLeftBoundary`, `foreheadRightBoundary`
- `foreheadMask`, `foreheadCoreMask`, `foreheadSoftEdgeMask`
- `hairMask`, `bangsMask`, `hairlineMask`, `eyebrowMask`, `glassesMask`
- `shadowMask`, `shineMask`, `wrinkleMask`, `foreheadTextureMask`
- `skinProtectionMask`, `hairProtectionMask`, `browProtectionMask`

Forehead zones:

- Central forehead: main skin area, reliable for tone and texture correction.
- Upper forehead/hairline border: identity-sensitive; must not bleed into hair.
- Lower forehead/brow border: affected by expression and brow shadows.
- Left/right temple forehead: often affected by hair and lighting.
- Glabella: between brows and above nose root; expression wrinkles and shadows are common.
- Wrinkle bands: horizontal forehead lines and vertical glabella lines.
- Shine/highlight region: oily or light reflection area.
- Hair/bangs occlusion: forehead covered by hair strands or bangs.

Do not treat the forehead as one flat skin patch. Hairline, central skin, brow border, temples, and glabella need separate masks.

### Forehead Dimensions

Soft guide metrics:

- `ForeheadHeight = hairlineCenter.y - browCenterY`
- `ForeheadHeightFaceRatio = ForeheadHeight / faceH`: `0.13 ~ 0.24`
- `ForeheadWidth = foreheadRightBoundary.x - foreheadLeftBoundary.x`
- `ForeheadWidthFaceRatio = ForeheadWidth / faceW`: `0.55 ~ 0.85`
- `ForeheadAreaRatio = area(foreheadMask) / area(faceMask)`
- `TempleWidthBalanceScore = abs(TempleWidthLeft - TempleWidthRight) / faceW`

High forehead can be natural, hairstyle-related, age-related, or caused by pulled-back hair. Low forehead can be bangs, low hairline, pose, or crop. Do not adjust forehead height unless the user explicitly asks.

### Hairline Relation

Hairline metrics:

- `HairlineCenterYRatio = hairlineCenter.y / faceH`
- `HairlineLeftTempleYRatio = hairlineLeftTemple.y / faceH`
- `HairlineRightTempleYRatio = hairlineRightTemple.y / faceH`
- `HairlineSymmetryScore = abs(hairlineLeftTemple.y - hairlineRightTemple.y) / faceH`
- `TempleRecessionLeft = hairlineLeftTemple.y - hairlineCenter.y`
- `TempleRecessionRight = hairlineRightTemple.y - hairlineCenter.y`
- `TempleRecessionBalanceScore = abs(TempleRecessionLeft - TempleRecessionRight) / faceH`
- `HairlineIrregularityScore`
- `HairlineConfidence`

Hairline is naturally uneven. Temple recession can be natural and identity-defining. Avoid hairline movement by default. Preserve baby hair and natural irregularity.

### Bangs And Hair Occlusion

Occlusion metrics:

- `BangsCoverageForeheadRatio = area(bangsMask overlapping foreheadMask) / area(foreheadMask)`
- `BangsLowerEdgeY`
- `BangsOpacityScore`
- `HairStrandOnForeheadScore`
- `HairShadowOnForeheadScore`

Decision:

- `BangsCoverageForeheadRatio > 0.50`: forehead height and wrinkle metrics are low confidence.
- `0.15 ~ 0.50`: retouch visible central skin locally; avoid global forehead decisions.
- `< 0.15`: forehead analysis is more reliable.

Do not remove bangs unless the user asks. Do not smooth skin through hair strands.

### Forehead Wrinkles And Glabella

Wrinkle metrics:

- `ForeheadHorizontalWrinkleMask`
- `GlabellaVerticalWrinkleMask`
- `ForeheadWrinkleCount`
- `ForeheadWrinkleLengthAvg`
- `ForeheadWrinkleDepthScore`
- `ForeheadWrinkleContinuityScore`
- `ForeheadWrinkleAreaRatio`
- `GlabellaWrinkleDepthScore`
- `GlabellaWrinkleCount`
- `ExpressionWrinkleCandidateScore`
- `AgeWrinkleCandidateScore`

Glabella metrics:

- `GlabellaMask`
- `GlabellaShadowScore`
- `GlabellaVerticalLineScore`
- `GlabellaFrownScore`
- `BrowHeadTensionScore`

Forehead wrinkles can be age, expression, lighting, or skin fold. Reduce wrinkle contrast, do not erase structure. Glabella lines strongly affect expression; over-cleaning can change character.

### Forehead Shine And Shadows

Shine metrics:

- `ForeheadShineMask`
- `ForeheadShineAreaRatio`
- `ForeheadShineStrength`
- `ForeheadSpecularScore`
- `ForeheadHighlightCenterScore`
- `ForeheadTextureLossInHighlight`

Shadow metrics:

- `HairShadowOnForeheadScore`
- `BrowShadowOnForeheadScore`
- `LightingGradientScore`
- `ForeheadUnevenToneScore`
- `ShadowDirectionConsistency`

Forehead often has natural shine. Reduce harsh specular highlight locally, preserve brightness gradient, and restore subtle texture. Shadows are not blemishes; soften harsh shadows while preserving depth.

### Forehead Texture And Tone

Texture/tone metrics:

- `ForeheadTextureStrength`
- `ForeheadTextureUniformity`
- `ForeheadBlemishMask`
- `ForeheadBlemishCount`
- `ForeheadBlemishAreaRatio`
- `ForeheadRednessScore`
- `ForeheadDryPatchScore`
- `ForeheadSmoothnessScore`
- `ForeheadColorMean`
- `CheekColorMean`
- `NoseColorMean`
- `ForeheadCheekColorDelta`
- `ForeheadNoseColorDelta`
- `ForeheadBrightnessRatio`
- `ForeheadSaturationRatio`
- `ForeheadColorPatchinessScore`

Forehead skin should not be over-smoothed. Pores and fine texture must remain. Correct local blemishes first, tone second, and texture-preserving smoothing last.

### Forehead Confidence

Suggested confidence:

- `ForeheadMaskConfidence = forehead skin segmentation confidence * hairline confidence * bangs inverse confidence * brow protection confidence * lighting confidence * resolution confidence * pose confidence`

Decision:

- High confidence: local skin, tone, wrinkle, and shine correction allowed.
- Medium confidence: central forehead cleanup allowed; avoid hairline/geometry decisions.
- Low confidence: no global forehead correction; only safe local cleanup on visible skin.

### Forehead Retouch Categories

Safe order:

1. Skin blemish cleanup: local only, preserve texture.
2. Shine control: reduce harsh highlights, restore subtle texture.
3. Wrinkle softening: reduce contrast, not erase; preserve age/expression.
4. Tone balancing: reduce patchiness/redness/yellow cast, preserve lighting gradient.
5. Hairline boundary cleanup: reduce color bleed, preserve baby hair and natural edge.
6. Shadow cleanup: soften harsh hair/brow shadows, preserve depth.
7. Geometry/forehead height: avoid by default; only user-requested and very subtle.

### Forehead Safe Limits

- Forehead geometry change: avoid by default.
- Hairline move: avoid by default; if explicitly requested, `0.3% ~ 1.0%` of `faceH`.
- Forehead height change: avoid by default.
- Wrinkle softening: reduce contrast by `15% ~ 45%` depending on age/request; do not erase fully.
- Shine reduction: reduce harsh specular intensity; do not remove all highlight.
- Blemish removal: local only; do not blur entire forehead.
- Texture smoothing: low to medium; preserve pore/fine texture.
- Tone balancing: subtle; preserve lighting direction.
- Hair shadow softening: soften, not erase.
- Glabella line softening: very conservative; avoid expression change.

`10%+` forehead geometry or hairline change is identity/age-changing. For ID photo, memorial portrait, and restoration, use smaller correction.

Final forehead rule:

- Do not define an ideal forehead.
- Define visible forehead skin, hairline/bangs occlusion, wrinkle, shine, shadow, color, blemish, texture, and mask confidence.
- Prefer forehead skin mask refinement, shine control, wrinkle contrast softening, local blemish cleanup, tone balancing, hairline boundary protection, glabella softening, and over-correction prevention.
- Avoid automatic resizing, hairline lowering/filling, removing all wrinkles, flattening texture, smoothing through bangs, or changing expression by over-cleaning glabella.

## Wrinkles, Folds, And Age Lines

Wrinkle metrics define wrinkles, folds, creases, sagging lines, expression lines, age lines, and shadow-based line detection. They are for age-preserving portrait retouch guidance and over-correction prevention. They are not targets for wrinkle-free skin.

Core rules:

- Wrinkles are not all defects.
- Wrinkles contain age, expression, identity, skin texture, and facial structure.
- Do not remove all wrinkles.
- Reduce harsh contrast, dirty shadow, and uneven color first.
- Preserve natural skin texture and age-appropriate lines.
- Geometry correction is rarely needed and is off by default for the wrinkle module.
- Wrinkle correction must be local, mask-based, and confidence-weighted.

### Wrinkle Definitions

Classify line-like detail before correction:

- `wrinkle`: thin or medium-width line from skin crease, expression, aging, dryness, or texture.
- `fold`: deeper and wider structural crease from skin folding, volume transition, sagging, or anatomy.
- `shadowLine`: line-like darkness from lighting, hair shadow, facial volume, or makeup.
- `textureLine`: fine pore/skin texture, usually preserved.
- `expressionLine`: line caused by current expression, such as smile, frown, squint, or brow raise.
- `ageLine`: stable line associated with age and skin structure.
- `damageLine`: scratch, scan damage, old photo crack, or compression artifact.

Do not treat all dark lines as wrinkles.

### Global Wrinkle Masks

Required wrinkle/fold masks:

- `foreheadWrinkleMask`
- `glabellaWrinkleMask`
- `leftCrowFeetMask`, `rightCrowFeetMask`
- `leftUnderEyeLineMask`, `rightUnderEyeLineMask`
- `leftNasolabialFoldMask`, `rightNasolabialFoldMask`
- `leftMouthCornerLineMask`, `rightMouthCornerLineMask`
- `leftMarionetteLineMask`, `rightMarionetteLineMask`
- `leftCheekWrinkleMask`, `rightCheekWrinkleMask`
- `chinCreaseMask`
- `chinTextureWrinkleMask`
- `neckHorizontalWrinkleMask`
- `neckVerticalWrinkleMask`
- `doubleChinFoldMask`
- `submentalCreaseMask`

Protection masks:

- `eyeProtectionMask`
- `eyelashProtectionMask`
- `eyebrowProtectionMask`
- `lipProtectionMask`
- `nostrilProtectionMask`
- `hairProtectionMask`
- `beardProtectionMask`
- `jewelryProtectionMask`
- `clothingProtectionMask`

### Common Wrinkle Metrics

Useful metrics:

- `WrinkleDepthScore`: local darkness/contrast compared with nearby skin.
- `WrinkleWidth`
- `WrinkleLength`
- `WrinkleContinuityScore`
- `WrinkleSharpnessScore`
- `WrinkleShadowScore`
- `WrinkleHighlightRidgeScore`
- `WrinkleTexturePreservationScore`
- `WrinkleDirection`
- `WrinkleCurvature`
- `WrinkleAreaRatio`
- `WrinkleSymmetryScore`
- `WrinkleExpressionConfidence`
- `WrinkleAgeConfidence`
- `WrinkleShadowConfidence`
- `WrinkleDamageConfidence`

### Classification Rule

Classify every wrinkle-like line as one of:

- Real fine wrinkle.
- Deep fold.
- Expression line.
- Age line.
- Shadow line.
- Makeup or contour line.
- Hair shadow.
- Beard/mustache texture.
- Skin texture.
- Photo damage, scratch, or compression artifact.

Correction depends on class:

- Fine wrinkle: soften contrast lightly and preserve texture.
- Deep fold: reduce shadow, not structure.
- Expression line: preserve expression and soften only if harsh.
- Age line: preserve age and reduce excessive contrast only.
- Shadow line: tone/shadow cleanup, no geometry.
- Makeup line: preserve unless requested.
- Hair/beard line: protect hair/beard mask.
- Skin texture: preserve.
- Damage line: restoration allowed if confirmed artifact.

### Region Policies

Forehead wrinkles:

- Include horizontal forehead bands and glabella vertical lines.
- Reduce line contrast, soften harsh shadow, preserve age-appropriate lines, preserve skin texture, avoid plastic forehead.
- Fine forehead wrinkle contrast reduction: `15% ~ 45%`.
- Deep forehead wrinkle contrast reduction: `10% ~ 30%`.
- Glabella line reduction: conservative, `10% ~ 25%`.

Eye wrinkles, crow's feet, and under-eye lines:

- Protect eyelashes first.
- Protect eyelid crease and lower eyelid structure.
- Crow's feet often appear with a natural smile.
- Do not erase lower lashes as wrinkles.
- Crow's feet reduction: `10% ~ 35%`.
- Under-eye fine line reduction: `10% ~ 30%`.
- Lower eyelid crease reduction: very conservative.

Nasolabial folds:

- Palja folds are cheek volume and mouth-area structure boundaries, not simple wrinkles.
- Reduce dirty shadow and fold contrast, but preserve cheek-mouth structure.
- Do not blur nose side or mouth corner.
- Light fold softening: `10% ~ 30%`.
- Deep fold shadow reduction: `10% ~ 25%`.
- Smile expression folds: very conservative.

Mouth corner, marionette, and perioral lines:

- Clean dark mouth-corner shadow.
- Soften marionette contrast.
- Protect lip boundary and beard/mustache texture.
- Do not move mouth corners unless the mouth module requests it.
- Mouth-corner darkness reduction: `15% ~ 40%`.
- Marionette fold reduction: `10% ~ 30%`.
- Perioral fine-line reduction: `10% ~ 25%`.

Cheek wrinkles:

- Cheek wrinkles mix pore texture, smile lines, sagging lines, and skin texture.
- Preserve pores and cheek texture, especially in highlight regions.
- Do not flatten cheek highlight or shadow.
- Cheek fine-line softening: `10% ~ 30%`.
- Sag-line contrast reduction: `10% ~ 25%`.

Chin wrinkles:

- Include mental crease, lower-lip shadow, and chin dimpling/texture.
- Protect lower lip and beard/skin texture.
- Avoid changing chin shape.
- Mental crease reduction: `10% ~ 30%`.
- Chin texture smoothing: low.

Neck wrinkles:

- Neck wrinkles are affected by posture, age, skin fold, neck length, lighting, hair, necklace, and collar.
- Do not make face age and neck age inconsistent.
- Protect necklace, collar, and hair.
- Neck wrinkle reduction: `10% ~ 35%`.
- Deep neck band reduction: `10% ~ 25%`.

Double-chin and submental folds:

- Double chin is wrinkle, volume, pose, and shadow together.
- Classify as pose-driven, shadow-driven, structure-driven, collar/clothing-driven, or hair-driven first.
- Clean shadow first, improve jaw-neck separation softly, preserve natural under-chin volume.
- Do not carve a hard jawline.
- Fold shadow reduction: `10% ~ 30%`.

### Global Wrinkle Confidence

Suggested confidence:

- `WrinkleMetricConfidence = skinMaskConfidence * landmarkConfidence * regionVisibilityConfidence * lightingConfidence * expressionConfidence * occlusionInverseConfidence * textureConfidence * resolutionConfidence`

Reduce confidence for:

- Strong expression, smile, squinting, or frowning.
- Head pitch/yaw.
- Hair, beard, mustache, glasses shadow, clothing, or jewelry covering the region.
- Harsh lighting, heavy makeup, blur, low resolution, old photo damage, compression artifacts, overexposure, or underexposure.

Decision:

- High confidence: wrinkle contrast softening allowed.
- Medium confidence: local tone/shadow cleanup only.
- Low confidence: do not remove line; first classify as shadow, occlusion, or artifact.

### Wrinkle Retouch Priority

1. Detect region masks.
2. Detect protection masks.
3. Detect hair, beard, glasses, clothing, jewelry, and other occlusions.
4. Classify line type.
5. Calculate depth, width, length, continuity, sharpness, shadow component, and age/expression confidence.
6. Choose correction type: contrast reduction, shadow cleanup, tone balancing, texture-preserving smoothing, restoration, or no correction.
7. Preserve age, expression, identity, skin texture, face structure, and natural asymmetry.
8. Never apply global blur.
9. Never remove all wrinkles.
10. Never create plastic skin.

### Wrinkle Scoring

Composite scores:

- `ForeheadWrinkleScore = ForeheadWrinkleDepthScore + ForeheadWrinkleContinuityScore + ForeheadWrinkleAreaRatio`
- `EyeWrinkleScore = CrowFeetDepthScore + UnderEyeLineDepthScore + EyeSquintExpressionScore`
- `NasolabialFoldScore = NasolabialFoldDepthScore + NasolabialFoldLength + CheekVolumeFoldScore`
- `MouthCornerWrinkleScore = MouthCornerDarknessScore + MarionetteLineDepthScore + PerioralFineLineDepthScore`
- `CheekWrinkleScore = CheekFineWrinkleScore + CheekSmileFoldScore + CheekSagLineScore`
- `ChinWrinkleScore = MentalCreaseDepthScore + ChinTextureWrinkleScore + ChinShadowScore`
- `NeckWrinkleScore = NeckHorizontalDepthScore + NeckVerticalLineScore + NeckWrinkleAreaRatio`
- `DoubleChinFoldScore = DoubleChinFoldDepthScore + SubmentalCreaseDepthScore + DoubleChinSoftVolumeScore + inverse JawNeckSeparationScore`

Correction strength:

- `WrinkleCorrectionStrength = baseStrength * WrinkleMetricConfidence * regionPolicyWeight * agePreservationWeight * expressionPreservationWeight`

### Wrinkle Safe Limits

- Fine wrinkle softening: `10% ~ 35%` contrast reduction.
- Deep fold softening: `10% ~ 25%` contrast/shadow reduction.
- Forehead fine lines: `15% ~ 45%`.
- Forehead deep lines: `10% ~ 30%`.
- Eye wrinkles: `10% ~ 35%`.
- Nasolabial folds: `10% ~ 30%`.
- Mouth-corner darkness: `15% ~ 40%`.
- Cheek wrinkles: `10% ~ 30%`.
- Chin crease: `10% ~ 30%`.
- Neck wrinkles: `10% ~ 35%`.
- Double-chin fold: `10% ~ 30%` shadow reduction.
- Texture preservation: always preserve local pore/fine skin detail.
- Geometry change: off by default.

Absolute wrinkle rules:

- Do not erase 100%.
- Do not blur whole region.
- Do not flatten structural folds.
- Do not make face age and neck age inconsistent.
- Do not remove expression identity.

Final wrinkle rule:

- Do not define perfect wrinkle-free skin.
- Define where the wrinkle/fold is, what type of line it is, whether it is skin/expression/age/shadow/makeup/hair/beard/artifact, and whether correction should reduce contrast, clean shadow, balance tone, or do nothing.
- Preferred usage: wrinkle detection, wrinkle classification, age-preserving softening, expression-preserving cleanup, local shadow cleanup, texture-preserving skin retouch, and over-correction prevention.
- Avoid removing all wrinkles, global blur, plastic skin, erasing age, changing expression, flattening nasolabial folds, erasing neck age while face remains aged, and treating shadows/hair/beard as wrinkles.

## Spots, Acne, Blemishes, And Pigmentation

Spot metrics define moles, acne, blemishes, freckles, pigmentation, melasma, acne scars, redness, dark spots, blackheads, whiteheads, pores, scars, and photo damage. They are for local, texture-preserving portrait retouch guidance and over-correction prevention.

Core rules:

- Not all spots are defects.
- Some marks are identity features.
- Some marks are temporary skin issues.
- Some marks are age-related pigmentation.
- Some marks are makeup, shadow, hair, beard, or image damage.
- Classify before removal.
- Use small local masks and confidence weighting.
- Local correction only. No global blur.
- Preserve natural skin texture, pores, age, and recognizable features.

### Spot Categories

Common categories:

- Mole / 점: stable dark or brown mark, often round/oval, possibly identity-defining. Default preserve.
- Acne / 여드름: red, raised, inflamed, or white/yellow center. Usually temporary and can be corrected more aggressively than moles.
- Pimple redness: local red inflammation without strong bump. Correct color first.
- Acne scar: depressed, raised, dark, or red residual mark. Soften; do not over-flatten.
- Blemish / 잡티: small local uneven mark, color irregularity, old acne mark, or tiny spot not otherwise classified.
- Freckle / 주근깨: small brown spots, often clustered or patterned. Default preserve.
- Dark spot / 색소점: localized brown/dark mark; can be age spot, sun spot, PIH, mole, or freckle.
- Melasma / 기미: broader soft-edged brown/gray-brown patch. Tone balancing, not spot erase.
- Redness / 홍조: broad or local red area. Color correction, not texture removal.
- Pore / 모공: natural skin texture; preserve by default.
- Blackhead/whitehead: small pore-related dot or bump, especially nose/chin; selective local cleanup allowed.
- Scar / 흉터: line, patch, indentation, raised area, or color change. Default soften, not erase.
- Photo damage / dust / scratch: not skin; restoration cleanup allowed.

### Spot Masks

Skin masks:

- `skinMask`, `faceSkinMask`
- `foreheadSkinMask`
- `leftCheekSkinMask`, `rightCheekSkinMask`
- `noseSkinMask`, `chinSkinMask`, `jawSkinMask`, `neckSkinMask`

Candidate masks:

- `moleMask`
- `acneMask`
- `acneRednessMask`
- `acneWhiteheadMask`
- `acneScarMask`
- `blemishMask`
- `freckleMask`
- `pigmentationMask`
- `melasmaMask`
- `darkSpotMask`
- `rednessMask`
- `blackheadMask`
- `whiteheadMask`
- `poreTextureMask`
- `scarMask`
- `photoDamageMask`

Protection masks:

- `eyeProtectionMask`
- `eyebrowProtectionMask`
- `eyelashProtectionMask`
- `lipProtectionMask`
- `nostrilProtectionMask`
- `hairProtectionMask`
- `beardProtectionMask`
- `moleIdentityProtectionMask`
- `skinTextureProtectionMask`
- `wrinkleProtectionMask`

### Common Spot Metrics

Useful metrics:

- `SpotColorDelta`
- `SpotLuminanceDelta`
- `SpotSaturationDelta`
- `SpotRednessScore`
- `SpotBrownnessScore`
- `SpotDarknessScore`
- `SpotBrightnessScore`
- `SpotSize`
- `SpotRadius`
- `SpotCircularity`
- `SpotEdgeSharpness`
- `SpotSoftness`
- `SpotRaisednessScore`
- `SpotDepressionScore`
- `SpotTextureDisruptionScore`
- `SpotClusterScore`
- `SpotStabilityConfidence`
- `SpotRemovalSafetyScore`

### Region Policies

Forehead:

- Acne, shine, small blemishes, wrinkles, and hair shadow are common.
- Protect hairline and eyebrows.

Cheeks:

- Freckles, pigmentation, acne scars, redness, pores, and blush are common.
- Preserve natural skin texture.

Nose:

- Blackheads, pores, redness, and shine are common.
- Do not treat all pores as blemishes.

Chin and jawline:

- Acne, redness, shaving marks, ingrown hair, beard shadow, and texture are common.
- Do not remove beard texture as blemish.

Neck:

- Pigmentation, redness, wrinkles, shaving marks, and necklace/collar interactions are common.
- Match face retouch strength to neck.

### Moles

Mole metrics:

- `MoleDarknessScore`
- `MoleCircularityScore`
- `MoleEdgeScore`
- `MoleSizeScore`
- `MoleIdentityScore`
- `MoleRemovalRiskScore`

Rules:

- Moles can be identity information.
- Large moles, prominent unique marks, stable-looking marks, and facially prominent marks are protected automatically.
- Tiny low-identity moles may be softened only in beauty-cleanup mode.
- User-requested mole removal can use local inpaint.

Default: preserve moles.

### Acne And Acne Scars

Acne metrics:

- `AcneRednessScore`
- `AcneRaisednessScore`
- `AcneWhiteheadScore`
- `AcneInflammationScore`
- `AcneClusterScore`
- `AcneSeverityScore`

Acne correction:

- Reduce redness.
- Remove white/yellow center.
- Soften bump shadow/highlight.
- Reconstruct local skin texture.
- Preserve pores around lesion.

Safe range:

- Acne redness reduction: `40% ~ 80%`.
- Whitehead removal: allowed.
- Bump texture softening: medium local.

Acne scar metrics:

- `PittedScarScore`
- `RaisedScarScore`
- `AcneScarColorScore`
- `AcneScarTextureDisruptionScore`
- `ScarDepthScore`

Acne scars can be structural. Reduce red/brown pigmentation and dark pit shadows, but avoid flat airbrushed patches.

Safe range:

- Scar color reduction: `20% ~ 60%`.
- Pitted scar shadow reduction: `15% ~ 40%`.
- Structural flattening: avoid by default.

### Blemishes And Freckles

Blemish rules:

- Blemish is a wide category.
- First exclude mole, freckle, melasma, acne, wrinkle, shadow, beard, and hair.
- Small temporary blemishes can be cleaned locally.
- Broad blemishes should become pigmentation/tone work.

Safe range:

- Small blemish cleanup: allowed.
- Color reduction: `20% ~ 70%`.

Freckle rules:

- Freckles are skin character and can define identity.
- Default preserve.
- Natural retouch reduces only harsh/distracting spots.
- Beauty-cleanup mode can soften globally but should not erase all freckles.
- Full removal requires explicit user request.

Safe range:

- Natural freckle softening: `0% ~ 25%`.
- Beauty-mode softening: `20% ~ 50%`.

### Pigmentation, Melasma, And Redness

Pigmentation metrics:

- `PigmentationAreaRatio`
- `PigmentationSoftEdgeScore`
- `PigmentationColorDepthScore`
- `PigmentationPatchinessScore`

Melasma and pigmentation:

- Treat as broad tone issue, not dot-by-dot removal.
- Use soft masks and tone balancing.
- Preserve skin texture and lighting gradient.

Safe range:

- Pigmentation depth reduction: `20% ~ 60%`.
- Melasma softening: `20% ~ 50%`.

Redness metrics:

- `RednessIntensityScore`
- `RednessAreaRatio`
- `RednessPatchinessScore`
- `AcneRednessCandidate`
- `IrritationCandidate`
- `BlushMakeupCandidate`

Redness rules:

- Redness can be acne, rosiness, irritation, makeup, or lighting.
- Preserve blush if makeup is detected.
- Do not make skin gray.

Safe range:

- Acne redness reduction: `40% ~ 80%`.
- Broad redness reduction: `20% ~ 50%`.

### Blackheads, Whiteheads, And Pores

Rules:

- Pores are skin texture, not blemishes by default.
- Blackheads and whiteheads can be selectively cleaned.
- Nose/chin pore pattern must remain.
- Do not over-smooth nose or chin into plastic skin.

Safe range:

- Blackhead reduction: `20% ~ 60%`.
- Whitehead cleanup: local allowed.
- Pore smoothing: low.

### Scars And Photo Damage

Scar rules:

- Scars may carry identity, memory, age, or history.
- Default soften, not erase.
- Full removal only by explicit request.
- Preserve skin texture and nearby facial structure.

Photo damage rules:

- Dust, scratch, scan artifact, compression damage, and old print damage are not skin.
- Restoration cleanup is allowed if detected as artifact.
- Do not confuse actual moles/scars with photo damage.

### Identity Protection

Protect automatically:

- Large moles.
- Prominent unique marks.
- Stable-looking birthmarks.
- Distinctive freckle patterns.
- Identity-defining scars.
- Age-related natural pigmentation when removal would change identity.

`SpotAutoRemovalAllowed` only if:

- Small.
- Low identity score.
- High temporary-defect confidence.
- Not near critical features.
- Not part of freckle pattern.
- Not hair, beard, makeup, or shadow.
- High skin reconstruction confidence.

If uncertain: soften or preserve.

### Spot Confidence

Suggested confidence:

- `SpotMetricConfidence = skinMaskConfidence * regionVisibilityConfidence * lightingConfidence * textureConfidence * resolutionConfidence * occlusionInverseConfidence * classificationConfidence`

Decision:

- High confidence temporary defect: local cleanup allowed.
- High confidence identity mark: protect.
- Medium confidence: soften only.
- Low confidence: do not remove; classify again or leave unchanged.

### Spot Retouch Priority

1. Detect clean skin region.
2. Apply protection masks for eyes, eyelashes, eyebrows, lips, nostrils, hair, beard, wrinkles, and identity moles.
3. Detect candidate marks.
4. Classify mark type.
5. Assign identity, temporary, or artifact confidence.
6. Choose correction: protect, soften, color-correct, shadow-correct, local inpaint, texture restore, or no action.
7. Reconstruct skin locally.
8. Preserve nearby texture.
9. Avoid blur halos and flat patches.

### Spot Scoring

- `SpotRemovalSafetyScore = temporaryDefectConfidence * lowIdentityScore * skinReconstructionConfidence * classificationConfidence * regionSafety`
- `MoleIdentityScore = size + contrast + unique location + stable shape + facial prominence`
- `AcneSeverityScore = redness + raisedness + whitehead + cluster + inflammation`
- `BlemishScore = colorDelta + darkness/redness + local irregularity`
- `FreckleIdentityScore = cluster pattern + distribution + natural brownness + uniqueness`
- `PigmentationScore = area + brownness + soft edge + patchiness`
- `RednessScore = red channel excess + area + patchiness`
- `PoreBlackheadScore = dark pore dots + nose/chin location + pore pattern`
- `ScarScore = texture disruption + line/patch shape + color difference + depth`
- `PhotoDamageScore = unnatural sharpness + texture mismatch + artifact pattern`
- `CorrectionStrength = baseStrength * SpotMetricConfidence * SpotRemovalSafetyScore * regionPolicyWeight * identityPreservationInverse`

### Spot Safe Limits

- Mole: default preserve; tiny low-identity softening `0% ~ 30%`; removal only explicit or beauty-cleanup policy.
- Acne: redness reduction `40% ~ 80%`; whitehead removal allowed; bump softening medium local.
- Acne scar: color reduction `20% ~ 60%`; pitted shadow reduction `15% ~ 40%`; structural flattening avoided.
- Blemish: small local cleanup allowed; color reduction `20% ~ 70%`.
- Freckles: default preserve; natural softening `0% ~ 25%`; beauty softening `20% ~ 50%`; full removal only explicit.
- Pigmentation/melasma: soft tone reduction `20% ~ 60%`; no hard-edge removal.
- Redness: acne redness `40% ~ 80%`; broad redness `20% ~ 50%`.
- Blackheads/whiteheads: selective cleanup, preserve pore texture.
- Scar: default soften, full removal only explicit.
- Photo damage: restoration cleanup allowed.

Final spot rule:

- Do not define all spots as defects.
- Define what type of mark it is, whether it is temporary/permanent/identity/makeup/shadow/hair/beard/pore/photo damage, and whether correction should preserve, soften, color-correct, inpaint, or ignore.
- Preferred usage: acne cleanup, blemish cleanup, redness correction, pigmentation tone balancing, blackhead/whitehead selective cleanup, photo damage restoration, identity mark protection, and texture-preserving local retouch.
- Avoid removing all moles or freckles automatically, flattening pores, whole-face blur, texture destruction, confusing beard/hair/shadow with blemish, and changing identity marks without request.

## Skin Smoothing

Skin smoothing defines manual, slider-triggered skin softening behavior. It is not automatic AI beautification.

Core rules:

- Skin smoothing must run only when the user changes a skin smoothing control.
- Default state is no visible correction.
- Do not run skin smoothing automatically when a tab opens.
- Do not smooth the whole face globally by default.
- Do not erase all pores, wrinkles, moles, freckles, age signs, highlights, shadows, or natural skin color variation.
- Do not affect eyes, eyelids, eyelashes, eyebrows, lips, lip texture, hair, teeth, nostrils, beard, clothing, jewelry, or background.
- Preserve skin texture, pores, natural grain, face identity, age, and lighting.
- Plastic skin is failure.

Target regions:

- `foreheadSkinMask`
- `leftCheekSkinMask`, `rightCheekSkinMask`
- `noseSkinMask`
- `underEyeSkinMask`
- `mouthAreaSkinMask`
- `chinSkinMask`
- `jawSkinMask`
- `neckSkinMask`
- `faceSkinMask`: visible skin excluding protected features.
- `cleanSkinMask`: skin excluding eyes, eyebrows, eyelashes, lips, teeth, nostrils, hair, beard, strong wrinkles, identity moles, protected freckles, clothing, and background.

Always protect:

- `eyeProtectionMask`
- `eyelashProtectionMask`
- `eyebrowProtectionMask`
- `lipProtectionMask`
- `teethProtectionMask`
- `nostrilProtectionMask`
- `hairProtectionMask`
- `beardProtectionMask`
- `moleIdentityProtectionMask`
- `freckleProtectionMask`
- `wrinkleProtectionMask`
- `skinTextureProtectionMask`
- `jawlineProtectionMask`
- `underJawShadowProtectionMask`
- `clothingProtectionMask`
- `backgroundProtectionMask`

Protection masks outrank smoothing masks. Boundaries should be feathered, but critical feature protection remains hard enough to prevent bleed.

Allowed smoothing targets:

- Temporary rough skin.
- Minor uneven texture.
- Small non-identity blemish softness.
- Subtle patchiness.
- Minor redness transitions.
- Small dry roughness.
- Local tone unevenness.
- Noise-like skin roughness.
- Minor pore contrast only when the pore map is preserved.

Skin smoothing may reduce excessive local harshness, rough transitions, small uneven skin texture, local color/tone noise, and minor non-structural bumpiness.

Never smooth:

- Eyes, eyelids too strongly, eyelashes, eyebrows, lips, lip texture, teeth, nostrils, hair strands, beard, mustache, shaving texture, jewelry, clothing, or background.
- Strong facial wrinkles, nasolabial fold structure, mouth-corner structure, jawline edge, under-jaw shadow structure, identity moles, and protected freckles.

Inputs:

- `skinSmoothingSlider`: `0.0 ~ 1.0`.
- Optional controls: `skinTexturePreserveSlider`, `skinPorePreserveSlider`, `skinToneBlendSlider`, `skinBlemishBlendSlider`, `skinRednessBlendSlider`, `skinShineBlendSlider`, `skinRegionSelector`.
- Optional region selector: all face skin, forehead, cheeks, nose, under eye, mouth area, chin, jaw, neck.

Slider behavior:

- `0.0`: no smoothing and no visible change.
- `0.1 ~ 0.3`: very light texture softening; preserve nearly all pores.
- `0.3 ~ 0.6`: moderate smoothing; soften roughness and minor unevenness while preserving pore map and fine grain.
- `0.6 ~ 0.8`: strong smoothing; preserve texture and edges, avoid plastic skin.
- `0.8 ~ 1.0`: maximum smoothing; preserve minimum texture and trigger over-smoothing checks.

Higher slider strength controls correction amount, not mask expansion. Higher strength must not invade protected regions or disable texture preservation.

Region-specific limits:

- Forehead: low to medium; protect wrinkles and hairline; reduce shine separately before smoothing.
- Cheeks: low to medium; preserve pores and fine grain strongly; avoid waxy surface.
- Nose: very conservative; preserve pores; blackhead cleanup belongs to blemish module.
- Under-eye: very conservative; protect lower lashes, fine lines, and lower eyelid; dark-circle module handles color/shadow.
- Mouth area: conservative; protect lip border, mouth corners, mustache/beard, and expression texture.
- Chin: low to medium; acne cleanup local only; preserve natural chin texture.
- Jaw: conservative; protect beard/shaving shadow and jawline edge.
- Neck: low to medium; preserve neck lines; match face texture level.

Texture preservation:

- Preserve pores, fine grain, micro-contrast, natural skin noise, small skin texture, local skin structure, and age-appropriate detail.
- `TexturePreservationScore = textureAfterSmoothing / textureBeforeSmoothing`
- Natural mode target: `0.70 ~ 0.90`.
- Portrait cleanup mode target: `0.55 ~ 0.75`.
- Strong beauty mode target: `0.40 ~ 0.60`.
- Never drop below `0.35` unless the user explicitly intends an extreme look.
- Pore map must remain visible, especially on nose and cheeks.
- If local cleanup removes texture, restore matching texture from nearby clean skin.

Preferred method:

- Edge-aware smoothing.
- Frequency-separation style smoothing.
- Bilateral or detail-preserving filtering.
- Local mask-based smoothing.
- Texture-preserving tone blending.
- Defect-aware local correction.

Avoid:

- Gaussian blur over the whole face.
- Global denoise on skin.
- Uniform blur layer.
- Large-radius blur without texture restoration.
- Smoothing over edges or protection masks.

Frequency separation rule:

- Low-frequency layer: skin tone, broad color unevenness, redness, patchiness, smooth light gradients.
- High-frequency layer: pores, fine grain, hair/beard texture, wrinkles, eyelashes, lip texture, skin micro detail.
- Skin smoothing mainly affects low-frequency unevenness and selected medium-frequency roughness.
- Do not blur the high-frequency layer globally.

### Smoothing Scale, Threshold, And Uniformity

Skin smoothing must separate fine, medium, and large skin variation. It must not smooth all frequencies equally.

Fine detail:

- Pores.
- Fine skin grain.
- Micro texture.
- Tiny skin noise.
- Very small roughness.
- Subtle dry texture.
- Fine shaving texture.
- Natural face grain.

Fine detail is usually protected. Do not remove it completely.

Medium detail:

- Small uneven texture.
- Minor bumps.
- Local roughness.
- Small patchy transitions.
- Shallow acne aftermath.
- Uneven pore clusters.
- Mild dry patches.
- Local skin harshness.

Medium detail is the main smoothing target and can be softened carefully.

Large detail:

- Broad tone unevenness.
- Redness patch.
- Yellow or gray patch.
- Wide shadow imbalance.
- Cheek tone mismatch.
- Forehead shine transition.
- Face-neck tone mismatch.
- Large color cast.
- Lighting-related uneven tone.

Large detail belongs mainly to tone/color/shadow modules, not blur.

Adaptive radius guide:

- `FineRadius = 0.0015 * faceW ~ 0.004 * faceW`, usually `1px ~ 4px` on a normal portrait.
- `MediumRadius = 0.005 * faceW ~ 0.015 * faceW`, usually `4px ~ 12px`.
- `LargeRadius = 0.02 * faceW ~ 0.06 * faceW`, usually `15px ~ 60px`.

These are analysis scales, not direct blur radii. Use them to separate skin detail layers. Do not apply Gaussian blur directly across the face with these radii.

Threshold definitions:

- `FineThreshold`: decides whether tiny detail is natural texture or noise/roughness.
- `MediumThreshold`: decides whether local unevenness should be softened.
- `LargeThreshold`: decides whether broad tone difference should be balanced.
- `UniformityThreshold`: decides when a skin area is too uneven compared to nearby clean skin.
- `TextureProtectionThreshold`: minimum texture level that must remain after smoothing.
- `EdgeProtectionThreshold`: prevents smoothing across eyes, lips, hair, beard, nostrils, and jawline.

Recommended threshold logic:

- If `detailSize <= FineRadius`, texture pattern is natural, and region is skin, protect as `skinTexture`.
- If fine detail is compression noise, scan noise, or harsh artifact, allow very light cleanup.
- If detail size is between `FineRadius` and `MediumRadius`, contrast is above nearby clean skin, and it is not wrinkle/hair/beard/mole, allow medium smoothing.
- If variation size is at least `LargeRadius`, color/luminance difference is broad, and it is not natural shadow or makeup, send to tone/color balance.
- If `localSkinVariance > cleanSkinVariance * allowedMultiplier`, mark as `unevenSkinCandidate`.
- If `textureAfter < textureBefore * minimumTextureRatio`, reduce smoothing or restore texture.

Uniformity:

- Uniformity means skin tone and texture are visually consistent enough, but not perfectly flat.
- Natural pores, micro texture, color variation, and micro-contrast must remain.
- `SkinUniformityScore = 1.0 - normalized(localColorVariance + localTextureVariance + patchiness)`
- High uniformity can look clean, but becomes plastic when texture is too low.
- Low uniformity can look blotchy, rough, patchy, or uneven.
- Perfect uniformity is not the goal.

Uniformity safe ranges:

- Natural portrait: `Uniformity target 0.55 ~ 0.72`, `Texture preservation 0.75 ~ 0.90`.
- Clean portrait: `Uniformity target 0.65 ~ 0.82`, `Texture preservation 0.60 ~ 0.80`.
- Strong smoothing: `Uniformity target 0.75 ~ 0.90`, `Texture preservation 0.45 ~ 0.65`.
- Forbidden: `Uniformity > 0.90` and `TexturePreservation < 0.40`, which indicates plastic skin failure.

Detail layer decision:

- `FineProtectedTexture`: pores, natural grain, tiny skin texture; preserve.
- `FineNoiseOrArtifact`: compression noise, scan noise, unnatural speckle; light cleanup allowed.
- `MediumUnevenTexture`: rough patches, small bumps, uneven dry texture; smoothing allowed.
- `MediumDefect`: acne, spot, scar, ingrown hair; send to blemish or beard module.
- `LargeToneUnevenness`: broad redness/yellow/gray patch; send to tone module.
- `LargeShadow`: natural light/shadow; preserve or send to shadow module.
- `MakeupOrBlush`: protect unless user requests correction.
- `HairOrBeard`: protect.
- `WrinkleOrFold`: protect unless wrinkle slider is active.

Slider-to-layer mapping:

- Slider `0.1 ~ 0.3`: fine smoothing `0% ~ 5%`, medium smoothing `10% ~ 25%`, large tone blend `0% ~ 10%`.
- Slider `0.3 ~ 0.6`: fine smoothing `5% ~ 12%`, medium smoothing `25% ~ 50%`, large tone blend `10% ~ 25%`.
- Slider `0.6 ~ 0.8`: fine smoothing `10% ~ 18%`, medium smoothing `50% ~ 70%`, large tone blend `20% ~ 35%`.
- Slider `0.8 ~ 1.0`: fine smoothing `15% ~ 25%`, medium smoothing `70% ~ 85%`, large tone blend `30% ~ 45%`, with over-smoothing checks required.

Absolute scale rules:

- Fine layer must never be removed completely.
- Medium layer is the primary smoothing target.
- Large layer is for tone, color, or shadow balancing, not blur.
- Large tone correction must not flatten lighting.

Region-specific scale rules:

- Cheeks: medium smoothing is the main target; preserve fine pores; send large redness to tone module.
- Nose: preserve fine pores strongly; keep medium smoothing low; send blackheads to blemish module.
- Under-eye: protect fine lines; keep medium smoothing very low; send dark tone to dark-circle module.
- Forehead: medium smoothing allowed; protect wrinkles; handle shine separately.
- Mouth area: protect lips, mustache, and mouth corners; keep medium smoothing low.
- Chin: medium smoothing allowed; send acne/ingrown hair to blemish or beard module.
- Jaw: protect beard/shaving texture and jawline edge.
- Neck: allow large tone match; protect neck wrinkles.

Threshold adaptation:

- Low resolution: do not chase fine detail, reduce fine smoothing, and avoid texture synthesis.
- Very sharp image: protect fine texture more carefully.
- Heavy makeup: reduce smoothing and avoid removing powder texture.
- Beard/stubble: protect beard texture and do not smooth as skin.
- Older face: preserve age texture and avoid forcing high uniformity.

Scale-aware over-smoothing failure:

- `FineTextureLoss`: pores or fine grain disappear.
- `MediumOverBlend`: skin surface becomes waxy.
- `LargeFlattening`: shadows/highlights disappear and face volume becomes flat.
- `UniformityTooHigh`: skin is too even compared to nearby regions.
- `TextureUniformityMismatch`: cheek, nose, and neck textures no longer match naturally.
- `PlasticSkinScore = FineTextureLoss + UniformityTooHigh + MicroContrastLoss + EdgeBlur + ToneFlatness`

If `PlasticSkinScore` is high, reduce medium smoothing, restore fine texture, reduce uniformity target, restore micro-contrast, and protect more regions.

Edge protection:

- Protect edges around eyes, eyelids, eyelashes, eyebrows, nostrils, lip boundary, mouth corners, jawline, hairline, ears, beard/mustache, wrinkle folds, and neck/collar boundary.
- `EdgeBleedCheck`: smoothing must not cross from skin into feature masks, smear hair into skin, smear skin into background, blur eye/lip boundaries, or create halos.

Blemish interaction:

- Skin smoothing is not blemish removal.
- Blemish module handles acne, whiteheads, blackheads, spots, pigmentation, scars, and photo damage.
- Skin smoothing handles general roughness, small uneven transitions, texture harshness, and minor tone noise.
- Do not remove large blemishes by smoothing.
- After blemish cleanup, smoothing may blend local texture.

Wrinkle interaction:

- Wrinkles are not skin texture noise.
- If wrinkle slider is inactive, skin smoothing should preserve wrinkle structure.
- Skin smoothing may soften surrounding roughness but must not flatten structural wrinkles, nasolabial folds, mouth-corner lines, or age lines.

Dark-circle interaction:

- Dark-circle module handles under-eye color, tear-trough shadow, eye-bag shadow, and vascular/pigment tone.
- Skin smoothing must not brighten under-eye automatically, erase lower lashes, blur lower eyelid, or remove under-eye structure.
- Under-eye smoothing strength should be lower than cheek smoothing.

Shine interaction:

- Shine module handles oily shine and specular highlights.
- Skin smoothing must not remove shine by blur alone, flatten highlights, or create dull gray skin.
- If shine reduction is active, restore subtle texture inside the shine area.

Beard, shaving texture, and makeup:

- If `beardMask` or `shavingShadowMask` exists, reduce smoothing strength strongly and protect hair texture.
- Do not treat beard dots as pores.
- Do not blur beard into skin or brighten beard shadow as skin patch.
- If `makeupMask` exists, preserve makeup texture, blush, contour, and highlight style unless requested.
- Do not treat makeup powder as skin roughness.

Over-smoothing detection:

- `PlasticSkinScore = low pore density + low fine grain + low micro contrast + flat tone + large uniform skin patch + missing texture + blurred edges`
- `OverSmoothCandidate` if texture preservation is too low, pore density drops too much, micro-contrast drops too much, face texture no longer matches neck, cheeks look waxy, nose pores disappear, or under-eye becomes a flat bright patch.
- If over-smoothing is detected, reduce smoothing strength, restore texture and micro-contrast, reduce mask area, and protect more regions.

Texture restoration after smoothing:

- `TextureRestoreSource`: nearby clean skin from the same region, lighting zone, skin tone, and texture scale.
- Cheek texture should come from nearby cheek.
- Nose texture should come from nearby nose.
- Under-eye texture should not use nose pores.
- Neck texture should not copy cheek texture directly.
- Avoid repeated patterns and artificial grain unless needed.
- `TextureRestoreStrength = TextureLossScore * regionTexturePolicy`

Confidence:

- `SkinSmoothingConfidence = skinMaskConfidence * protectionMaskConfidence * textureConfidence * lightingConfidence * resolutionConfidence * occlusionInverseConfidence`
- High confidence: normal slider behavior allowed.
- Medium confidence: reduce max smoothing strength and preserve texture more strongly.
- Low confidence: only very light tone blending, no strong smoothing, and no texture synthesis.

Safe limits:

- Global smoothing: not allowed.
- Face-wide smoothing: allowed only through skin mask and protection masks, with texture preservation.
- Default maximum strength: natural `0.35`, portrait cleanup `0.55`, strong `0.75`, max `1.0` with strict texture guard.
- Texture loss should not exceed `30%` in natural mode, `45%` in portrait cleanup mode, or `60%` in strong mode.
- Under-eye and nose smoothing caps are lower than cheek smoothing.
- Neck should match face retouch strength and age consistency.

Output requirements:

- `smoothedImageLayer`
- `skinSmoothingMaskUsed`
- `protectedMaskUsed`
- `texturePreservationScore`
- `plasticSkinScore`
- `overSmoothWarning`
- `regionStrengthMap`
- `textureRestoreMap` if used

Debug views:

- Target skin mask.
- Protection mask.
- Texture protection mask.
- Over-smoothed candidate map.
- Before/after texture difference map.

Final skin smoothing rule:

- Do not define smooth skin as perfect skin.
- Define where skin can be softened, where it must be protected, how much texture must remain, whether smoothing damaged natural detail, and whether the user actually requested smoothing.
- Preferred usage: manual skin smoothing slider, local texture softening, tone transition smoothing, post-blemish texture blending, texture-preserving portrait cleanup, and plastic-skin prevention.
- Avoid automatic beautification, global blur, removing all pores, smoothing all wrinkles, smoothing lips/eyes/hair, making the face younger automatically, changing identity or age impression, and running on tab open.

## Skin Texture, Pores, And Natural Detail

Skin texture metrics define pores, fine grain, micro-contrast, natural skin detail, smoothing limits, texture preservation, and texture restoration. They are the safety guard against plastic skin.

Core rules:

- Skin texture is not a defect.
- Pores, fine grain, tiny unevenness, and natural micro-shadows must be preserved.
- Retouch should remove temporary defects, uneven tone, harsh shine, acne, and distracting blemishes.
- Retouch must not destroy skin texture.
- No global blur.
- No plastic skin.
- No flat painted skin.

### Texture Definitions

- `skinTexture`: natural fine detail including pores, small grain, tiny surface variation, and micro-contrast.
- `pore`: small repeated skin opening or dot-like texture, especially on nose, cheeks, forehead, and chin.
- `fineGrain`: very small high-frequency skin texture that gives photographic realism.
- `microContrast`: small local brightness variation that makes skin dimensional.
- `textureNoise`: camera noise, compression artifact, oversharpening grain, or scan noise not aligned with real skin.
- `blemish`: temporary or isolated defect, such as acne, spot, redness, scar mark, or dirt.
- `skinPlasticity`: unnatural smoothness from excessive blur, denoise, low texture, or flat tone.
- `textureLoss`: loss of pore/fine-grain detail after smoothing or inpainting.
- `textureMismatch`: corrected region does not match nearby skin texture.

### Texture Masks

Required masks:

- `skinMask`, `faceSkinMask`
- `foreheadTextureMask`
- `leftCheekTextureMask`, `rightCheekTextureMask`
- `noseTextureMask`
- `underEyeTextureMask`
- `mouthAreaTextureMask`
- `chinTextureMask`
- `jawTextureMask`
- `neckTextureMask`
- `poreMask`
- `fineGrainMask`
- `microTextureMask`
- `textureProtectionMask`
- `textureRestoreMask`
- `overSmoothedSkinMask`
- `plasticSkinCandidateMask`
- `textureMismatchMask`
- `noiseArtifactMask`
- `compressionArtifactMask`
- `scanGrainMask`

Protection masks:

- `eyeProtectionMask`
- `eyelashProtectionMask`
- `eyebrowProtectionMask`
- `lipProtectionMask`
- `nostrilProtectionMask`
- `hairProtectionMask`
- `beardProtectionMask`
- `wrinkleProtectionMask`
- `moleProtectionMask`
- `freckleProtectionMask`

### Region Texture Policy

Forehead:

- Preserve medium texture.
- Reduce shine first.
- Soften wrinkles separately.
- Avoid flattening the large forehead area.

Cheeks:

- Preserve pore/fine grain strongly.
- Avoid waxy cheek surface.
- Restore texture after blemish removal.
- Do not flatten blush or cheek highlight.

Nose:

- Preserve strong pore structure.
- Blackhead cleanup is selective.
- Reduce shine locally.
- Avoid smooth plastic nose.

Under-eye:

- Very conservative.
- Protect lower lashes and fine lines.
- Dark-circle correction must keep thin-skin texture.

Mouth area:

- Protect lip boundary.
- Protect mustache/beard if present.
- Preserve expression-line texture.
- Avoid smearing around mouth corners.

Chin and jaw:

- Preserve pores, bumps, acne context, beard/shaving texture.
- Classify beard/shaving shadow before smoothing.

Neck:

- Match face texture level.
- Preserve neck grain and fine lines.
- Avoid face/neck mismatch.

### Primary Texture Metrics

Useful metrics:

- `TextureStrength`
- `PoreVisibilityScore`
- `FineGrainStrength`
- `MicroContrastScore`
- `TextureUniformityScore`
- `TextureDirectionRandomness`
- `TextureDensity`
- `TextureScale`
- `TextureSharpness`
- `TextureNaturalnessScore`
- `TexturePreservationScore`
- `TextureLossScore`
- `TextureMismatchScore`
- `PlasticSkinScore`

### Pore Metrics

- `PoreMask`
- `PoreDensity`
- `PoreSizeAvg`
- `PoreContrast`
- `PoreDistributionScore`
- `PoreUniformityScore`
- `PoreSuppressionScore`
- `PoreOverRemovalScore`

Pores are normal skin texture. Nose and cheeks naturally have stronger pores. Blackheads are not the same as pores. Reduce only distracting blackheads or extreme pore contrast.

### Real Texture Vs Noise

Real skin texture:

- Follows skin surface.
- Matches nearby pore/grain scale.
- Distributed naturally.
- Has subtle color/luminance variation.
- Appears inside skin mask.

Noise/artifact:

- Random RGB noise.
- Compression blocks or ringing.
- Oversharpened speckles.
- Appears similarly across skin, hair, and background.
- Not tied to skin structure.

Decision:

- Preserve real skin texture.
- Reduce distracting camera/compression/scan noise.
- Do not mistake pores for noise.
- Do not mistake noise for pores.

### Texture And Blemish Separation

Blemish candidate:

- Stronger color difference.
- Larger than normal pore.
- Non-repeated pattern.
- Red/brown/dark abnormality.
- Raised or depressed shadow.
- Isolated component.

Texture candidate:

- Repeated small structure.
- Similar scale to nearby pores.
- Low color abnormality.
- Natural distribution.
- No isolated inflammation.

Rule:

- Remove blemish.
- Preserve texture.
- Reduce extreme pore/blackhead selectively.
- Protect identity marks.

### Texture Preservation During Retouch

Before correction:

- Sample surrounding clean skin texture.
- Measure local texture strength, pore density, micro-contrast, and color/tone base.

During correction:

- Remove target blemish or color issue.
- Do not blur outside local mask.
- Protect pore and fine-grain masks.
- Avoid large flat patches.

After correction:

- Compare corrected area with surrounding skin.
- Restore texture if `TextureLossScore` is high.
- Match pore density and fine grain.
- Check for halo, blur, smudge, and plasticity.

`TextureRestoreNeeded` if:

- `TextureLossScore` is high.
- `TextureMismatchScore` is high.
- `PlasticSkinScore` is high.
- Corrected patch looks flatter than surrounding skin.

### Over-Smoothing Detection

Over-smoothed skin candidate:

- Low `TextureStrength`.
- Low `PoreDensity`.
- Low `MicroContrastScore`.
- Large uniform color patch.
- Blurred edges around blemish removal.
- Mismatch with nearby skin.

`PlasticSkinScore` combines:

- Low texture.
- Low pore density.
- Low micro-contrast.
- Flat tone.
- Over-wide smoothing area.
- Skin/wrinkle boundary loss.

Correction:

- Reduce smoothing strength.
- Restore sampled local texture.
- Restore micro-contrast.
- Avoid uniform blur.

### Texture Restoration

Texture restore source:

- Nearby clean skin patch from the same or similar region.
- Do not copy from nose to under-eye.
- Do not copy from cheek to lip or neck blindly.
- Match color and luminance first.
- Transfer only fine texture, not blemishes.
- Avoid repeating pattern.

Safe restoration:

- Subtle pore/grain restoration.
- Local micro-contrast restoration.
- No visible pattern repetition.
- No artificial global grain overlay unless needed.

### Shine, Wrinkle, Beard, Hair, And Makeup Interaction

Shine:

- Shine can hide texture.
- Reducing shine without restoring texture creates flat patches.
- Nose, forehead, and cheeks need shine-aware texture handling.

Wrinkles:

- Wrinkles are not texture noise.
- Texture should remain around softened wrinkles.
- The wrinkle module controls wrinkle softening; the texture module protects natural detail.

Beard, hair, and makeup:

- Beard/hair texture is not skin texture.
- Makeup texture may be intentional.
- Do not smooth beard as skin.
- Do not remove makeup texture unless requested.
- Do not confuse powder texture with acne.

### Texture Confidence

Suggested confidence:

- `SkinTextureConfidence = skinMaskConfidence * regionVisibilityConfidence * resolutionConfidence * focusSharpnessConfidence * lightingConfidence * noiseSeparationConfidence * occlusionInverseConfidence`

Decision:

- High confidence: texture-preserving retouch and local texture restoration allowed.
- Medium confidence: conservative cleanup; avoid aggressive texture decisions.
- Low confidence: avoid smoothing and synthesis; only safe color/tone cleanup.

### Texture Safe Limits

- Global skin blur: not allowed.
- Large-area smoothing: avoid; if necessary, low strength and texture-preserving.
- Local blemish cleanup: allowed, must restore texture.
- Pore reduction: low strength only; preserve pore map.
- Nose pore visual reduction: `10% ~ 35%` max; blackheads may be reduced more selectively.
- Cheek texture smoothing: low to medium, preserve fine grain.
- Under-eye texture smoothing: very low.
- Forehead texture smoothing: low to medium, shine correction first.
- Chin texture smoothing: low.
- Neck texture smoothing: low, match face/neck age consistency.
- Texture restoration: subtle, nearby-skin matched, no repeated pattern.

Final texture rule:

- Do not define perfect smooth skin.
- Define what is real texture, pore, blemish, noise, shine, artifact, what must be preserved, and what can be corrected locally.
- Preferred usage: texture preservation, pore protection, acne cleanup with texture restoration, shine correction with texture restoration, over-smoothing detection, plastic-skin prevention, face/neck texture consistency.
- Avoid global blur, porcelain skin by default, removing all pores, flattening cheeks/nose/forehead, treating texture as noise, treating noise as texture, repeating synthetic patterns, and making face smoother than neck.

## Skin Color Balance, Body Color Balance, And Tone Matching

Skin color balance metrics define face, neck, ear, body, arm, hand, and visible-skin tone matching. This is a manual tone-harmony module, not automatic whitening or automatic beautification.

Core rules:

- Do not automatically whiten skin.
- Do not automatically brighten all skin.
- Do not force one ideal skin color.
- Do not remove natural skin warmth, makeup, blush, contour, shadows, or highlights by default.
- Do not make face, neck, ears, hands, arms, or body the exact same flat color.
- Do not apply visible correction when a tab opens.
- Preserve original skin tone, identity, lighting direction, age, makeup intent, and natural color variation.
- Correct only visible unnatural mismatch, color cast, patchiness, or face/body tone separation when the user adjusts a tone control.

### Tone Regions And Masks

Main skin regions:

- `faceSkinMask`
- `neckSkinMask`
- `earSkinMask`
- `foreheadSkinMask`
- `leftCheekSkinMask`, `rightCheekSkinMask`
- `noseSkinMask`
- `chinSkinMask`
- `jawSkinMask`
- `bodySkinMask`
- `chestSkinMask`
- `shoulderSkinMask`
- `armSkinMask`
- `handSkinMask`
- `legSkinMask` if visible

Protection and context masks:

- `makeupMask`
- `blushMask`
- `contourMakeupMask`
- `lipProtectionMask`
- `eyeMakeupProtectionMask`
- `hairProtectionMask`
- `beardProtectionMask`
- `clothingProtectionMask`
- `backgroundProtectionMask`
- `shadowMask`
- `highlightMask`

### Core Definitions

- `SkinColorBalance`: balances color differences within visible skin regions, mainly red, yellow, blue, green, gray, and magenta casts.
- `BodyColorBalance`: harmonizes exposed body skin with face/neck naturally, especially when face retouch has separated from neck/body tone.
- `ToneMatching`: aligns brightness, contrast, saturation, warmth, and color cast between skin regions while preserving light direction and natural depth.
- `ColorCast`: unwanted overall tint such as red, yellow, green, blue, magenta, or gray.
- `Patchiness`: uneven local color inside the same skin region, such as red patches, dull gray areas, yellow blotches, uneven foundation, or sunburn patches.
- `SkinToneSeparation`: face, neck, ear, hand, arm, or body look like different people or different lighting.

Recommended internal color spaces:

- `Lab`
- `HSV` / `HSL`
- `YCbCr`
- Linear RGB for brightness operations if available.

Useful values:

- `SkinLuminance`
- `SkinHue`
- `SkinSaturation`
- `SkinWarmth`
- `SkinRedness`
- `SkinYellowness`
- `SkinBlueness`
- `SkinGreenness`
- `SkinGrayness`
- `SkinColorMean`
- `SkinColorMedian`
- `SkinColorVariance`
- `SkinTonePatchinessScore`

### Reference Region Rule

Do not use a fixed global skin target. Choose a reliable reference from clean visible skin.

Preferred reference order:

1. Clean cheek skin with good lighting.
2. Clean forehead or jaw skin if cheek is unreliable.
3. Neck skin when neck/body matching mode is active.
4. Body skin if the user selected body as the reference.

Avoid references from:

- Shine highlights.
- Deep shadows.
- Heavy makeup.
- Blush or contour makeup.
- Acne, redness, pigmentation, or melasma.
- Beard or shaving shadow.
- Under-eye dark circles.
- Lips.
- Ears when very red.
- Hands when lighting is different.
- Overexposed or underexposed skin.

Reference confidence:

- `ReferenceSkinColor = median color of clean selected reference mask`
- `ReferenceSkinToneConfidence = clean skin confidence * lighting confidence * texture confidence * low makeup confidence * low shadow confidence * low highlight confidence`

### Face Color Balance

Face metrics:

- `FaceColorBalanceMask`
- `FaceToneMean`
- `FaceToneVariance`
- `FaceRednessScore`
- `FaceYellownessScore`
- `FaceGraynessScore`
- `FacePatchinessScore`
- `FaceLeftRightColorBalanceScore`

Interpretation:

- The face naturally has redder cheeks/nose and different forehead tone.
- Do not make the face one flat color.
- If makeup exists, protect intentional blush, contour, and highlight.
- Preserve natural cheek warmth.

Correction:

- Reduce unwanted cast.
- Balance patchy tone.
- Preserve local depth.
- Preserve makeup unless a makeup-specific control requests otherwise.
- Protect lips, eyes, brows, hair, and beard.

### Neck, Ear, Body, Hand, And Arm Balance

Neck:

- `NeckToneMean`
- `FaceNeckColorDelta`
- `FaceNeckLuminanceDelta`
- `FaceNeckSaturationDelta`
- `FaceNeckWarmthDelta`
- `NeckPatchinessScore`
- `NeckShadowScore`

Neck is often darker, yellower, redder, or affected by jaw/hair/collar shadow. Match face and neck partially, not exactly. Preserve under-jaw shadow, neck depth, and neck lines.

Ear:

- `EarToneMean`
- `FaceEarColorDelta`
- `EarRednessScore`

Ears are naturally warmer/redder. Reduce only excessive redness and keep ear warmth, structure, and shadow.

Body:

- `BodyToneMean`
- `FaceBodyColorDelta`
- `NeckBodyColorDelta`
- `BodyLuminanceDelta`
- `BodySaturationDelta`
- `BodyWarmthDelta`
- `BodyPatchinessScore`
- `BodyLightingMismatchScore`

Body skin may be darker, tanner, redder, or less retouched than the face. Match toward face/neck softly while preserving body shadows, muscle/shape depth, clothing boundaries, and hair over shoulder/neck.

Hands and arms:

- `HandToneMean`
- `ArmToneMean`
- `FaceHandColorDelta`
- `FaceArmColorDelta`
- `HandRednessScore`
- `HandDarknessScore`
- `ArmTanLineScore`

Hands are often redder or darker than the face, and arms may be more tanned. Harmonize severe mismatch only partially, preserving veins, knuckles, texture, and natural arm/hand tone.

### Tone Components And Cast Classification

Tone components:

- Brightness/luminance: perceived lightness; must preserve lighting direction.
- Contrast: separation between light and dark; must preserve face/body volume.
- Saturation: color intensity; avoid orange artificial skin and gray/dead skin.
- Warmth: red/yellow balance; avoid excessive orange/yellow or blue/gray dullness.
- Tint: green/magenta balance.
- Hue: base color direction; identity-sensitive and should shift very conservatively.

Classify color cast before correction:

- `RedCast`: excess red/pink, common in acne, irritation, ears, nose, cheeks.
- `YellowCast`: excess yellow/orange, common in warm lighting, foundation, old photos, or camera white balance.
- `BlueCast`: cool/blue skin, common in shadow or underexposure.
- `GreenCast`: sickly or fluorescent cast from mixed lighting.
- `MagentaCast`: pink/purple imbalance from camera white balance or makeup.
- `GrayCast`: dull low-saturation look from over-retouch, underexposure, or bad white balance.

Correction should neutralize cast partially, preserve original skin identity, and avoid overcorrection.

### Patchiness And Lighting Preservation

`PatchinessMask` should represent unnatural local color differences after excluding:

- Freckles and moles.
- Identity marks.
- Intentional blush/contour.
- Natural shadows and highlights.
- Beard/shaving shadow.
- Under-eye dark circles.

Patchiness correction:

- Use soft masks.
- Blend low-frequency tone only.
- Preserve texture.
- Avoid blur and hard edges.

Lighting preservation:

- `LightingGradientMap` estimates brightness direction across face/body.
- `ShadowMask` protects natural shadow regions.
- `HighlightMask` protects natural highlight regions.
- Correct lit, midtone, and shadow regions separately when possible.
- Do not equalize all skin to one brightness.
- Do not remove nose, cheek, jaw, neck, or body shape.

### Makeup, Shadow, Highlight, And Region Interactions

Makeup:

- Do not remove intentional blush.
- Do not remove contour as discoloration.
- Do not match neck/body to a blush-heavy face region.
- Do not treat lipstick as face redness.
- Do not force body to match makeup-heavy face exactly.

Shadows and highlights:

- Shadows may be cooler or more saturated than lit skin.
- Highlights may be lower saturation or hide texture.
- Preserve depth and direction; reduce only dirty or mismatched tone.

Region rules:

- Forehead: often brighter or shinier; do not flatten shine behavior.
- Cheeks: natural redness and blush are common; do not remove all warmth.
- Nose: redness and shine should be separated from pore/shine modules.
- Under-eye: use dark-circle module for under-eye color/shadow; do not globally brighten as face tone.
- Mouth area: protect lips and mustache/shaving shadow.
- Chin/jaw: classify beard, acne, and shadow before tone correction.
- Neck: partial face-neck harmony; preserve under-jaw shadow and neck lines.
- Body: partial harmony; preserve body volume and clothing boundaries.
- Hands: preserve natural hand texture and redness/darkness when plausible.

### Tone Slider Behavior

`skinColorBalanceSlider = 0.0`:

- No visible correction.

`0.1 ~ 0.3`:

- Very light color cast balancing.
- Preserve original tone strongly.

`0.3 ~ 0.6`:

- Moderate tone harmony.
- Reduce visible patchiness and mismatch.

`0.6 ~ 0.8`:

- Strong color balancing.
- Preserve lighting and region variation.

`0.8 ~ 1.0`:

- Maximum correction.
- Must still avoid whitening and flat skin.
- Trigger overcorrection check.

Related controls:

- `bodyColorBalanceSlider`: controls body/neck/hand/arm matching strength and must never force exact face tone.
- `toneWarmthSlider`: adjusts warmth/coolness while preserving natural skin hue range.
- `toneBrightnessSlider`: adjusts luminance while preserving highlights and shadows.
- `toneSaturationSlider`: adjusts color intensity while avoiding gray or orange skin.

### Tone Confidence

Suggested confidence:

- `ColorBalanceConfidence = skinMaskConfidence * cleanReferenceConfidence * lightingConfidence * makeupSeparationConfidence * shadowHighlightSeparationConfidence * resolutionConfidence * occlusionInverseConfidence`

Reduce confidence if:

- Strong mixed lighting.
- Heavy makeup.
- Low resolution or blur.
- Overexposure or underexposure.
- Strong shadows.
- Unstable skin mask.
- Hair/beard overlaps skin.
- Clothing color reflects onto skin.
- Old photo discoloration.
- Background color cast spills onto skin.

Decision:

- High confidence: normal slider behavior allowed.
- Medium confidence: reduce max correction and preserve original tone more.
- Low confidence: only very light tone balancing; avoid face/body matching and strong hue shift.

### Tone Safe Limits

- Skin whitening: not a default goal.
- Luminance correction: subtle to medium, preserving shadows/highlights.
- Color cast correction: `10% ~ 35%` natural mode, `25% ~ 55%` portrait cleanup, `45% ~ 70%` strong mode.
- Face-neck mismatch correction: partial only, typically `20% ~ 60%`.
- Face-body mismatch correction: partial harmony only, typically `15% ~ 55%`.
- Ear redness reduction: `10% ~ 40%`.
- Broad redness reduction: `20% ~ 50%`.
- Patchiness correction: `20% ~ 60%`.
- Saturation change: conservative.
- Hue shift: very conservative and identity-sensitive.

### Tone Output Requirements

Output should include:

- `skinColorBalanceMaskUsed`
- `bodyColorBalanceMaskUsed`
- `referenceSkinRegionUsed`
- `colorCastMap`
- `patchinessMap`
- `faceNeckDeltaBeforeAfter`
- `faceBodyDeltaBeforeAfter`
- `correctionStrengthMap`
- `protectedMaskUsed`
- `overCorrectionWarning`

Optional debug views:

- Clean skin reference map.
- Face/neck/body color sample points.
- Shadow/highlight separated tone map.
- Makeup protection map.
- Before/after Lab delta map.

Final tone rule:

- Do not define ideal skin color.
- Define visible skin regions, reliable references, whether mismatch is lighting, makeup, shadow, body tone, redness, pigmentation, or camera cast, what correction type is safe, and whether the user actually requested tone correction.
- Preferred usage: manual skin color balance, face-neck harmony, body tone matching, red/yellow/gray cast reduction, patchy tone balancing, makeup-aware tone correction, and lighting-preserving color correction.
- Avoid automatic whitening, global skin recoloring, forcing one skin color, removing makeup unintentionally, flattening shadows/highlights, making face/body the same flat tone, and running correction on tab open.

## Beard, Mustache, And Long Facial Hair

Beard metrics define beard, mustache, long beard, sideburn, jaw beard, neck beard, virtual beard landmarks, beard masks, and correction safety. This is not automatic beard removal or automatic beautification.

Core rules:

- Beard is facial hair, not a skin defect.
- Beard is identity-sensitive and affects age, gender impression, style, jaw shape, mouth boundary, chin shape, and neck separation.
- Do not erase beard automatically.
- Do not smooth beard as skin.
- Do not use beard edge as true jawline without confidence.
- Visible correction runs only when the user activates beard-related controls.
- Preserve beard by default and protect it from skin smoothing, blemish cleanup, lip correction, jawline correction, wrinkle correction, and under-jaw shadow correction.

### Beard Decision Order

Use this order before any beard-related correction or before another module touches a beard-overlapping region:

1. Treat beard as hair, not a skin defect.
2. Split facial hair into separate regions: `mustache`, `chinBeard`, `jawBeard`, `cheekBeard`, `neckBeard`, `sideburn`, and `longBeard`.
3. Do not expect beard landmarks from the face landmark model. Build `beardMask` first, then estimate virtual points such as `beardTopEstimated`, `beardBottomEstimated`, `mustacheTopEstimated`, and `longBeardTipEstimated`.
4. If beard covers the jawline, lower jawline analysis confidence. Do not use the beard outline as the real jawline.
5. If mustache overlaps the lips, protect it during lip correction. Do not misclassify mustache as lip shadow or lip boundary.
6. Treat long beard like hair, not skin. If long beard overlaps neck, clothing, or background, create separate protection masks for each overlap.
7. Do not remove stubble or shaving shadow automatically. Reduce them only when the user moves a beard or shaving-shadow softening slider.
8. Default behavior is protection, not removal. Beard removal is a separate explicit mode that requires a direct user command.

### Beard Landmark Availability

Common face landmark models do not provide beard landmarks.

Do not expect direct landmarks such as:

- `beardTop`
- `beardBottom`
- `mustacheEdge`
- `chinBeardEdge`
- `longBeardTip`

All beard landmarks are virtual estimated points derived from:

- `beardMask`
- Hair-like texture detection.
- Color separation.
- Face landmarks.
- Skin, lip, jawline, neck, clothing, and background masks.

Base anchors used for estimation:

- `noseBaseCenter`, `subnasale`, `philtrumCenter`
- `upperLipTopCenter`, `mouthLeftCorner`, `mouthRightCorner`, `mouthCenter`
- `lowerLipBottomCenter`, `chinPoint`
- `leftJawAngle`, `rightJawAngle`
- `leftJawBodyMid`, `rightJawBodyMid`
- `leftChinTransition`, `rightChinTransition`
- `neckLeft`, `neckRight`, `submentalCenter`
- `leftEarCenterEstimated`, `rightEarCenterEstimated`
- `faceCenterX`, `faceW`, `faceH`

### Beard Types

- `mustache`: hair above upper lip, between nose base and upper lip, including philtrum-side hair and upper-lip shadow.
- `goatee`: hair around chin and lower lip, sometimes including a small patch under lower lip.
- `chinBeard`: hair on chin surface; can hide chin texture and chin point.
- `jawBeard`: hair along jawline; can hide true jaw contour.
- `neckBeard`: hair below jaw and on upper neck; can hide neck wrinkles, under-jaw shadow, and double-chin fold.
- `sideburn`: side-face hair in front of ear, connecting scalp hair to beard.
- `cheekBeard`: lower-cheek hair; can hide cheek texture, jaw transition, and acne.
- `fullBeard`: connected mustache, cheek, chin, jaw, and neck beard region.
- `longBeard`: beard hair extending downward beyond chin and jawline; treat closer to head hair than skin.
- `stubble`: very short beard dots or short hair after shaving.
- `shavingShadow`: blue/gray/dark cast from hair roots under skin.

### Beard Masks

Required masks:

- `beardMask`
- `beardCoreMask`
- `beardSoftEdgeMask`
- `beardStrandMask`
- `mustacheMask`
- `upperLipBeardMask`
- `lowerLipBeardPatchMask`
- `chinBeardMask`
- `jawBeardMask`
- `cheekBeardMask`
- `neckBeardMask`
- `sideburnMask`
- `longBeardMask`
- `beardShadowMask`
- `stubbleDotMask`
- `beardStrayHairMask`
- `beardOcclusionMask`

Protection masks:

- `lipProtectionMask`
- `teethProtectionMask`
- `innerMouthProtectionMask`
- `nostrilProtectionMask`
- `noseProtectionMask`
- `skinTextureProtectionMask`
- `jawlineProtectionMask`
- `underJawShadowProtectionMask`
- `neckWrinkleProtectionMask`
- `clothingProtectionMask`
- `backgroundProtectionMask`
- `beardProtectionMask`
- `mustacheProtectionMask`
- `sideburnProtectionMask`
- `longBeardProtectionMask`

Protection rules:

- Skin smoothing must not blur beard.
- Blemish removal must not remove beard dots.
- Lip correction must not erase mustache touching lip.
- Jawline correction must not use beard edge as real jawline without confidence.
- Under-jaw shadow correction must not brighten beard as skin.

### Virtual Beard Landmarks

Estimate only after a reliable beard mask exists:

- `beardTopEstimated`
- `beardBottomEstimated`
- `beardLeftEstimated`
- `beardRightEstimated`
- `beardCenterEstimated`
- `mustacheTopEstimated`
- `mustacheBottomEstimated`
- `mustacheLeftEstimated`
- `mustacheRightEstimated`
- `mustacheCenterEstimated`
- `chinBeardTopEstimated`
- `chinBeardBottomEstimated`
- `jawBeardLeftEdgeEstimated`
- `jawBeardRightEdgeEstimated`
- `neckBeardBottomEstimated`
- `longBeardTipEstimated`
- `longBeardLeftEdgeEstimated`
- `longBeardRightEdgeEstimated`
- `sideburnTopEstimated`
- `sideburnBottomEstimated`

These points are for mask creation, confidence reduction, protected-region routing, and user-controlled tools. They are not direct face anatomy landmarks.

### Beard Structure Zones

- Zone A: Mustache, above upper lip; high protection because it touches nostrils, philtrum, and lip boundary.
- Zone B: Lower-lip patch; can be confused with lip shadow or chin wrinkle.
- Zone C: Chin beard; hides chin texture, chin crease, and chin shape.
- Zone D: Jaw beard; follows jawline and may hide true jaw contour.
- Zone E: Cheek beard; may hide acne, texture, and cheek shadow.
- Zone F: Neck beard; overlaps neck wrinkles and under-jaw shadow.
- Zone G: Sideburn; connects scalp hair and beard, protect unless user edits it.
- Zone H: Long beard body; treat as hair mass, not skin.
- Zone I: Long beard tips; preserve style unless user asks cleanup.
- Zone J: Beard soft edge; do not make a hard cutout.

### Beard Search ROIs

These are search ROIs, not correction targets. Actual target masks require beard-mask confidence.

`MustacheSearchROI`:

- `x = mouthLeftCorner.x - 0.10 * faceW` to `mouthRightCorner.x + 0.10 * faceW`
- `y = noseBaseCenter.y - 0.02 * faceH` to `upperLipTopCenter.y + 0.04 * faceH`

`ChinBeardSearchROI`:

- `x = mouthLeftCorner.x - 0.08 * faceW` to `mouthRightCorner.x + 0.08 * faceW`
- `y = lowerLipBottomCenter.y` to `chinPoint.y + 0.05 * faceH`

`JawBeardSearchROI`:

- `x = leftJawAngle.x` to `rightJawAngle.x`
- `y = mouthCenter.y` to `chinPoint.y + 0.08 * faceH`

`NeckBeardSearchROI`:

- `x = leftJawAngle.x - 0.05 * faceW` to `rightJawAngle.x + 0.05 * faceW`
- `y = chinPoint.y` to `chinPoint.y + 0.25 * faceH`

`SideburnSearchROI`:

- Left: `x = faceLeft - 0.05 * faceW` to `faceLeft + 0.15 * faceW`, `y = eyeCenterY - 0.05 * faceH` to `jawAngle.y + 0.05 * faceH`
- Right: `x = faceRight - 0.15 * faceW` to `faceRight + 0.05 * faceW`, `y = eyeCenterY - 0.05 * faceH` to `jawAngle.y + 0.05 * faceH`

`LongBeardSearchROI`:

- `x = leftJawAngle.x - 0.15 * faceW` to `rightJawAngle.x + 0.15 * faceW`
- `y = chinPoint.y - 0.03 * faceH` to lower visible beard/clothing boundary

### Beard Detection Features

Useful features:

- `BeardHairTextureScore`
- `BeardDensityScore`
- `BeardDarknessScore`
- `BeardHueScore`
- `BeardBlueGrayShadowScore`
- `BeardDirectionField`
- `BeardDirectionCoherence`
- `BeardEdgeSoftness`
- `BeardStrandVisibility`
- `BeardSkinContrast`
- `BeardPatchinessScore`
- `BeardConnectionScore`

### Long Beard Metrics

- `LongBeardLength = longBeardTipEstimated.y - chinPoint.y`
- `LongBeardLengthFaceRatio = LongBeardLength / faceH`
- `LongBeardWidth = longBeardRightEdgeEstimated.x - longBeardLeftEdgeEstimated.x`
- `LongBeardWidthFaceRatio = LongBeardWidth / faceW`
- `LongBeardDensityScore`
- `LongBeardTextureStrength`
- `LongBeardDirectionCoherence`
- `LongBeardTipIrregularity`
- `LongBeardBackgroundOverlapScore`
- `LongBeardClothingOverlapScore`
- `LongBeardNeckOverlapScore`

Interpretation:

- Long beard is hair mass, not skin.
- It may hide jawline, neck wrinkles, double-chin folds, and under-jaw shadow.
- If long beard overlaps the lower face/neck, reduce jawline, neck wrinkle, double-chin, and under-jaw shadow confidence.
- Long beard tips are style; do not auto-trim.
- Only background-isolated stray beard hairs may be cleaned like flyaways, and only with beard tool active.

### Beard Classification

Classify facial hair regions as:

- `intentionalBeard`: dense connected beard shape; preserve.
- `intentionalMustache`: visible mustache above upper lip; preserve.
- `sideburn`: side-face hair connected to scalp or beard; preserve.
- `stubble`: short repeated follicle dots; preserve by default.
- `shavingShadow`: blue/gray/dark beard-root cast; soften only by user control.
- `longBeard`: beard extending below chin/jaw/neck; preserve as hairstyle/identity.
- `strayBeardHair`: isolated beard hair outside main beard; cleanup candidate only if user requests.
- `razorIrritation`: red irritated shaving area; local correction allowed.
- `ingrownHair`: local inflamed trapped hair; local correction allowed.
- `beardArtifact`: old photo damage or compression mistaken as beard; restore only if confirmed.

Separate beard from:

- Skin blemishes.
- Pores.
- Pigmentation.
- Under-jaw shadow.
- Jawline contour.
- Lip boundary.
- Neck wrinkles.
- Photo artifacts.

When uncertain, preserve or soften only.

### Beard Module Interactions

Skin smoothing:

- Beard area is protected.
- Do not blur beard texture.
- Beard cleanup belongs to beard module, not smoothing.

Blemish removal:

- Do not remove beard dots.
- Local acne or ingrown hair can be corrected while preserving surrounding beard.

Lip correction:

- Protect mustache.
- Do not smooth upper lip into mustache.
- Do not erase mustache shadow automatically.

Jawline correction:

- If `jawBeardMask` overlaps jawline, lower jawline confidence.
- Do not use beard outline as real bone contour.
- Disable jaw geometry unless true skin/jaw contour is visible and user requested a geometry tool.

Under-jaw shadow and neck wrinkle:

- If `underJawStubbleMask` or `neckBeardMask` overlaps, classify before brightening or smoothing.
- Protect beard texture.
- Do not treat neck beard as neck wrinkle or under-jaw shadow.

Hair/flyaway:

- Sideburn and long beard strands are facial hair.
- Isolated beard strands may use flyaway-like logic only when the beard tool is active.

### Beard Correction Modes

- Mode A: Protect only. Default, no visible change.
- Mode B: Shaving shadow soften. Reduce blue/gray cast while preserving stubble texture.
- Mode C: Stubble soften. Reduce short-dot contrast while preserving natural beard pattern.
- Mode D: Beard color/tone balance. Even patchy beard color while preserving hair texture.
- Mode E: Beard stray hair cleanup. Remove isolated distracting beard strands while preserving shape.
- Mode F: Razor irritation cleanup. Reduce redness, ingrown hair, and tiny cuts.
- Mode G: Long beard cleanup. Clean distracting flyaway beard hairs without style change.
- Mode H: Beard removal. Not default; requires explicit user command and identity-change warning.

### Beard Sliders And Safe Limits

`beardProtection`:

- Always on by default.

`beardShadowSofteningSlider`:

- `0.0`: no change.
- `0.1 ~ 0.3`: slight blue/gray cast reduction.
- `0.3 ~ 0.6`: moderate shadow softening.
- `0.6 ~ 0.8`: strong clean-shaven impression.
- `0.8 ~ 1.0`: very strong; identity-change risk.

`stubbleSofteningSlider`:

- `0.0`: no change.
- `0.1 ~ 0.3`: reduce only harsh dots.
- `0.3 ~ 0.6`: soften visible stubble.
- `0.6 ~ 0.8`: strong stubble reduction.
- `0.8 ~ 1.0`: near clean-shave effect; caution.

`longBeardCleanupSlider`:

- `0.0`: no change.
- `0.1 ~ 0.3`: remove isolated background stray beard hairs.
- `0.3 ~ 0.6`: clean visible loose strands.
- `0.6 ~ 0.8`: stronger cleanup, preserve shape.
- `0.8 ~ 1.0`: strong cleanup, over-removal check required.

Safe limits:

- Intentional beard: default `0%` removal.
- Mustache: default `0%` removal.
- Sideburn: default `0%` removal.
- Shaving shadow: natural `15% ~ 35%`, cleanup `35% ~ 55%`, strong `55% ~ 70%`.
- Stubble dots: natural `0% ~ 20%`, cleanup `20% ~ 45%`, strong `45% ~ 70%`.
- Long beard background isolated strands: `30% ~ 100%` depending on slider/confidence.
- Connected beard edge and beard body: preserve.
- Razor redness: `30% ~ 70%` reduction.
- Ingrown hair: local cleanup allowed, preserve nearby beard texture.
- Beard geometry/style: no change by default.

### Beard Confidence And Output

Suggested confidence:

- `BeardMaskConfidence = hairTextureConfidence * beardRegionConfidence * colorSeparationConfidence * skinMaskConfidence * lipProtectionConfidence * jawProtectionConfidence * lightingConfidence * resolutionConfidence * occlusionInverseConfidence`

Reduce confidence for:

- Low resolution or motion blur.
- Dark beard on dark clothing/background.
- White/gray beard on bright background.
- Heavy shadow or compression.
- Old photo damage.
- Beard overlap with lips, jawline, or neck wrinkles.
- Weak skin/beard color separation.

Decision:

- High confidence: protect and allow selected beard correction.
- Medium confidence: protect strongly, allow soft tone correction only.
- Low confidence: preserve beard; do not remove or reshape.

Output should include:

- `beardMaskUsed`
- `mustacheMaskUsed`
- `chinBeardMaskUsed`
- `jawBeardMaskUsed`
- `neckBeardMaskUsed`
- `sideburnMaskUsed`
- `longBeardMaskUsed`
- `beardProtectionMaskUsed`
- `beardLandmarksEstimated`
- `beardConfidenceMap`
- `correctionStrengthMap`
- `overCorrectionWarning`

Optional debug:

- Beard region classification map.
- Beard vs skin/blemish map.
- Long beard edge map.
- Protected lip/jaw/neck overlap map.

Final beard structure rule:

- Do not define beard as defect.
- Define where beard, mustache, stubble, sideburn, and long beard exist, whether the hair is intentional style, shaving shadow, stubble, stray beard hair, razor irritation, or artifact, and whether the user actually requested beard correction.
- Preferred usage: beard protection, mustache protection, long beard mask handling, stubble/shaving shadow classification, beard-aware skin smoothing, jawline confidence adjustment, lip boundary protection, and manual beard cleanup.
- Avoid automatic beard removal, removing mustache as lip shadow, removing beard dots as blemish, using beard edge as jawline, smoothing beard as skin, changing beard style, damaging long beard shape, and running correction on tab open.

## Beard Stubble And Shaving Shadow

Beard and shaving metrics define beard stubble, shaving shadow, razor irritation, ingrown hair, beard texture, and lower-face shaving cleanup. Beard/stubble is not a blemish by default.

Core rules:

- Beard/stubble is protected by default.
- Shaving shadow is not skin dirt by default.
- Beard texture must be separated from acne, blackheads, pores, pigmentation, wrinkles, and under-jaw shadow.
- Do not remove beard/stubble automatically.
- Do not blur beard area as normal skin.
- Visible correction runs only when the user adjusts beard/shaving cleanup control.
- Preserve identity, age, gender impression, skin texture, jawline, and natural beard pattern.

Do not:

- Run automatically when a tab opens.
- Remove all beard shadow.
- Make lower face plastic.
- Blur jawline.
- Erase intentional mustache, beard, or sideburn.
- Treat beard dots as acne or blackheads.
- Treat shaving shadow as pigmentation.
- Brighten beard area until gray or flat.
- Damage lips, mouth corners, jawline, neck, or under-jaw shadow.

Allowed only by user control:

- Reduce harsh blue/gray shaving shadow.
- Soften uneven stubble darkness.
- Reduce razor redness.
- Reduce ingrown-hair bumps.
- Clean accidental shaving irritation.
- Even out patchy lower-face tone.
- Protect beard texture during skin smoothing.

Target regions:

- `beardRegionMask`: combined beard growth region.
- `mustacheRegionMask`: upper lip, philtrum side, and under nose.
- `chinBeardRegionMask`: chin and below lower lip.
- `jawBeardRegionMask`: jawline beard/stubble region.
- `lowerCheekBeardRegionMask`: lower cheek beard growth area.
- `sideburnMask`: sideburn and side face hair.
- `neckStubbleRegionMask`: upper neck and below jaw.
- `underJawStubbleRegionMask`: under chin and jaw-neck transition.
- `beardCoreMask`: visible beard/stubble pixels.
- `beardShadowMask`: blue/gray/dark shaving shadow area.
- `stubbleDotMask`: repeated dark hair follicle dots.
- `razorRednessMask`: red irritated shaving area.
- `ingrownHairMask`: inflamed bump or trapped-hair dot.
- `shavingCutMask`: small razor nick/cut.

Protection masks:

- `lipProtectionMask`
- `mouthCornerProtectionMask`
- `nostrilProtectionMask`
- `jawlineProtectionMask`
- `underJawShadowProtectionMask`
- `neckWrinkleProtectionMask`
- `hairProtectionMask`
- `sideburnProtectionMask`
- `skinTextureProtectionMask`
- `moleIdentityProtectionMask`
- `acneProtectionMask` if handled by acne module.

Beard mask outranks skin smoothing. Skin smoothing must not blur beard/stubble by default. Blemish module must not remove beard dots as spots. Under-jaw shadow module must not brighten beard shadow as normal skin.

Definitions:

- `beardStubble`: short visible hair after shaving, often many small dark dots or short directional marks.
- `shavingShadow`: blue, gray, green-gray, or dark cast under skin from hair roots or shaved beard.
- `beardTexture`: repeated follicle, dot, or short-hair pattern; may be natural and identity-defining.
- `razorIrritation`: red, patchy, irritated skin after shaving, sometimes with bumps or cuts.
- `ingrownHair`: inflamed bump, dark trapped hair, or red follicle bump.
- `razorCut`: tiny red line or nick.
- `mustacheShadow`: blue/gray/dark cast above upper lip, easily confused with pigmentation or lip shadow.
- `neckStubble`: beard dots or shadow on neck, easily confused with neck pigmentation or shadow.

Structure zones:

- Upper lip / mustache: close to lips and nostrils; protect lip boundary.
- Chin: stubble, acne, texture, and chin dimples overlap.
- Jawline: stubble can blend with jaw shadow; protect jawline shape.
- Lower cheek: patchy beard growth and shaving shadow are common.
- Neck: ingrown hairs, razor bumps, and neck stubble are common; distinguish from neck wrinkles.
- Sideburn: intentional hair transition; do not erase as blemish.
- Under-jaw: beard shadow and under-jaw shadow overlap; classify first.

Primary metrics:

- `BeardDensityScore`
- `StubbleDotDensity`
- `StubbleDotSizeAvg`
- `StubbleDirectionScore`
- `BeardShadowStrength`
- `BeardBlueGrayScore`
- `BeardPatchinessScore`
- `BeardEdgeSoftness`
- `RazorRednessScore`
- `RazorBumpScore`
- `IngrownHairScore`
- `ShavingCutScore`
- `BeardSkinTextureConflictScore`

Classification:

- `naturalStubble`: repeated dark dots in beard growth region; preserve by default.
- `shavingShadow`: broad blue/gray/dark cast; soften only if user requests.
- `intentionalBeard`: visible beard, mustache, or sideburn shape; protect.
- `razorIrritation`: red patch or sensitivity after shaving; color correction allowed.
- `ingrownHair`: local bump with trapped hair/dark dot/redness; local cleanup allowed.
- `razorCut`: small red cut or nick; local restoration allowed.
- `acne`: inflamed pimple not following beard pattern; send to acne module.
- `blackhead`: pore-related dot; send to blemish/pore module.
- `mole`: stable dark mark; protect unless requested.
- `pigmentation`: broad brown patch; send to pigmentation module.
- `underJawShadow`: structural shadow; send to under-jaw shadow module.
- `wrinkle`: line or fold; send to wrinkle module.
- `photoDamage`: scratch, dust, or artifact; send to restoration module.

Detection flow:

1. Build `beardRegionMask` from upper lip, chin, jawline, lower cheek, neck, under-jaw, and sideburn zones.
2. Detect repeated small dark components: stubble dots, short directional hairs, follicle marks.
3. Detect broad blue-gray, green-gray, dark gray, or low-saturation shadow-like color cast.
4. Detect inflammation: red patches, razor bumps, ingrown hairs, and cuts.
5. Exclude protected features: lips, nostrils, mouth corners, jawline edge, neck wrinkles, and intentional sideburn hair.
6. Classify before correction as preserve, soften, color-correct, local cleanup, or ignore.

Shaving shadow:

- `ShavingShadowMask`
- `ShavingShadowStrength`
- `ShavingShadowHueScore`
- `ShavingShadowAreaRatio`
- `ShavingShadowPatchinessScore`
- Shaving shadow is hair-root or beard shadow, not skin blemish.
- Natural correction softens blue-gray cast; it does not erase all texture.
- Safe limits: natural `15% ~ 35%`, cleanup `35% ~ 55%`, strong clean-shave `55% ~ 70%` only by explicit control.

Stubble dots:

- `StubbleDotMask`
- `StubbleDotPatternScore`
- `StubbleDotRemovalRiskScore`
- Stubble dots are not acne, blackheads, or blemishes.
- Repeated pattern in beard region should be protected.
- Safe limits: natural `0% ~ 20%` dot contrast reduction, cleanup `20% ~ 45%`, strong mode `45% ~ 70%` with texture restoration.

Razor redness:

- `RazorRednessMask`
- `RazorRednessIntensity`
- `RazorIrritationPatchScore`
- Reduce redness separately from beard shadow.
- Preserve skin and beard texture.
- Safe limits: `30% ~ 70%`, severe irritation up to `80%` with high confidence.

Ingrown hair:

- `IngrownHairMask`
- `IngrownHairDarkCoreScore`
- `IngrownHairRednessScore`
- `IngrownHairRaisednessScore`
- Local cleanup allowed while preserving surrounding stubble.
- Redness reduction `40% ~ 80%`; dark core cleanup local; bump softening medium local.

Razor cut:

- `ShavingCutMask`
- `ShavingCutLineScore`
- `ShavingCutFreshnessScore`
- Small nicks are temporary defects and can be locally restored.
- Large scar-like marks go to scar module.

Mustache / upper lip shadow:

- `MustacheShadowMask`
- `MustacheShadowStrength`
- `LipBoundaryRiskScore`
- `NostrilRiskScore`
- Protect lip border, nostrils, and philtrum structure.
- Safe limits: `15% ~ 45%`, strong mode up to `60%` with high confidence.

Jaw and neck stubble:

- `JawStubbleMask`
- `NeckStubbleMask`
- `UnderJawStubbleMask`
- `JawlineEdgeRiskScore`
- `NeckWrinkleRiskScore`
- Preserve jaw edge, neck wrinkles, and under-jaw structure.

Beard versus skin texture:

- Beard texture is repeated dark dots or short hair marks in beard growth regions.
- Skin pores are smaller pore-like texture distributed in skin region.
- Beard dots are not pores, blemishes, or blackheads.
- Do not smooth beard area like cheek skin.

Beard versus blemish:

- Acne in beard region can be corrected by acne module, but surrounding beard texture remains.
- Mole in beard region is protected unless requested.
- Pigmentation is broad brown patch, not blue shaving shadow.
- If uncertain, preserve or soften only.

Slider behavior:

- `0.0`: no change.
- `0.1 ~ 0.3`: very light shaving shadow softening; preserve stubble dots.
- `0.3 ~ 0.6`: moderate shaving shadow reduction, mild stubble contrast reduction, razor redness cleanup.
- `0.6 ~ 0.8`: strong cleanup; reduce visible stubble/shadow more while preserving texture and jawline.
- `0.8 ~ 1.0`: clean-shaven look mode; strict protection and texture restoration required, with identity-change warning.

The slider controls beard/shaving cleanup only. It must not remove intentional beard/mustache unless the user selected beard removal mode. It must not affect lips, nostrils, jaw edge, or neck wrinkles.

Confidence:

- `BeardCorrectionConfidence = beardRegionConfidence * skinMaskConfidence * stubblePatternConfidence * colorCastConfidence * protectionMaskConfidence * lightingConfidence * resolutionConfidence * occlusionInverseConfidence`
- High confidence: allow color cast reduction and local irritation cleanup.
- Medium confidence: soften only.
- Low confidence: preserve beard/stubble and do not remove dots or texture.

Over-correction failure:

- Lower face becomes plastic.
- All stubble dots disappear unintentionally.
- Jawline edge becomes blurry.
- Mustache area becomes flat/gray.
- Lips or nostrils are affected.
- Beard/mustache identity is removed without request.
- Face/neck texture mismatch appears.
- Under-jaw shadow is erased with beard shadow.

If failure occurs, restore texture, reduce correction strength, restore some stubble pattern, strengthen protection masks, and reduce mask area.

Output:

- `beardRegionMaskUsed`
- `shavingShadowMaskUsed`
- `stubbleDotMaskUsed`
- `razorRednessMaskUsed`
- `ingrownHairMaskUsed`
- `protectedMaskUsed`
- `correctionStrengthMap`
- `overCorrectionWarning`
- `textureRestoreMap` if used

Debug:

- Beard vs blemish classification map.
- Stubble dot candidates.
- Blue/gray shadow map.
- Protected lip/jaw/neck overlap map.

Final beard rule:

- Do not define beard/stubble as skin defect.
- Define whether the mark is stubble, shaving shadow, intentional beard, razor irritation, ingrown hair, acne, mole, pigmentation, wrinkle, under-jaw shadow, or artifact.
- Preferred usage: manual shaving shadow cleanup, razor redness reduction, ingrown hair local cleanup, stubble protection, beard-aware skin smoothing, beard-vs-blemish separation, and over-correction prevention.
- Avoid automatic beard removal, treating stubble as acne, treating shaving shadow as pigmentation, smoothing beard as normal skin, removing intentional mustache/beard, blurring jawline, erasing all lower-face texture, or affecting lips/nostrils.

## Hair

Hair metrics define scalp hair, hairline, bangs, side hair, temple hair, flyaway strands, volume, shadow, color, and mask boundary behavior. They are for hair mask refinement, soft edge matting, halo removal, flyaway reduction, shine control, tone balancing, and protecting face features. They are not beauty or hairstyle standards.

Core rules:

- Hair is not a solid shape.
- Hair has soft edges, semi-transparent strands, holes, highlights, shadows, and directional texture.
- Hair masks must be treated differently from face masks.
- Separate hair from skin/background first, then clean boundary and tone.
- Geometry or volume change comes last.
- Preserve identity, age, hairstyle, hair volume, hair direction, gray hair, and natural texture.
- Hair correction must not damage face, skin, eyebrow, forehead, ear, neck, clothing, or background.

### Hair Preconditions

Before hair analysis:

- Detect face landmarks and the head/hair region.
- Detect forehead, eyebrows, eyes, ears, neck, background, and clothing.
- Estimate pose, crop, lighting direction, resolution, and blur.
- Classify hair state: black, brown, gray, dyed, sparse, wet, curly, tied, covered, backlit, dark-on-dark background, bright-on-bright background, or occluding face.

Reduce confidence for cropped hair, dark hair on dark background, gray hair on bright background, low resolution, motion blur, compression, backlight halo, wet hair, curly hair with holes, transparent fine hair, hats, earrings, hands, or other occlusions.

### Hair Regions

Target regions and masks:

- `hairMask`
- `hairCoreMask`
- `hairSoftEdgeMask`
- `hairStrandMask`
- `flyawayHairMask`
- `bangsMask`
- `hairlineMask`
- `templeHairMask`
- `sideburnMask`
- `earOcclusionHairMask`
- `foreheadOcclusionHairMask`
- `eyebrowOcclusionHairMask`
- `faceOcclusionHairMask`
- `neckOcclusionHairMask`
- `backgroundMask`
- `skinProtectionMask`
- `foreheadProtectionMask`
- `eyebrowProtectionMask`
- `eyeProtectionMask`
- `earProtectionMask`
- `neckProtectionMask`

Hair zones:

- Core hair: dense main hair mass; reliable for color/tone cleanup.
- Hairline: forehead/hair boundary; identity-sensitive.
- Bangs/fringe: may occlude forehead, eyebrows, eyes, and skin.
- Temple hair: important for age, gender impression, and face shape.
- Side hair: affects head width and hairstyle.
- Crown/top hair: top volume and crop detection.
- Flyaway/loose strands: preserve or gently reduce, not blindly erase.
- Ear-overlap hair: affects ear and side-face detection.
- Neck/shoulder-overlap hair: must not bleed into clothing/background.
- Shadow region: hair shadow on skin/background; shadow is not hair.

Do not treat all hair as one binary mask. Core hair, soft edge, and strands need different processing.

### Hair Dimensions

Soft guide metrics:

- `HairWidthFaceRatio = hairW / faceW`: `1.05 ~ 1.60`
- `HairHeightFaceRatio = hairH / faceH`: `0.35 ~ 0.85`
- `HairTopExpansionRatio = (faceTop - hairTop) / faceH`: `0.05 ~ 0.28`
- `HairSideExpansionLeft = (faceLeft - hairLeft) / faceW`
- `HairSideExpansionRight = (hairRight - faceRight) / faceW`
- `HairSideExpansionBalanceScore = abs(HairSideExpansionLeft - HairSideExpansionRight)`

Hair width and height vary strongly by hairstyle. Use these mainly for mask sanity check and crop detection. Do not reduce hair volume automatically.

### Hairline

Hairline metrics:

- `hairlineYAtCenter`
- `hairlineYLeftTemple`
- `hairlineYRightTemple`
- `ForeheadHeight = hairlineYAtCenter - browCenterY`
- `ForeheadHeightFaceRatio = ForeheadHeight / faceH`: `0.13 ~ 0.24`
- `HairlineToFaceTopRatio = (hairlineYAtCenter - faceTop) / faceH`
- `HairlineSymmetryScore = abs(hairlineYLeftTemple - hairlineYRightTemple) / faceH`
- `TempleRecessionBalanceScore = abs(TempleRecessionLeft - TempleRecessionRight) / faceH`

High forehead, low forehead, and temple recession can be natural, age-related, hairstyle-related, or pose-related. Do not fill or move hairline automatically.

### Bangs And Face Occlusion

Useful overlap metrics:

- `BangsCoverageFaceRatio = area(bangsMask over forehead region) / area(forehead region)`
- `BangsEyebrowOverlapRatio = area(bangsMask overlapping eyebrow region) / area(eyebrow region)`
- `BangsEyeOverlapRatio = area(bangsMask overlapping eye region) / area(eye region)`
- `HairForeheadOverlapRatio`
- `HairEyebrowOverlapRatio`
- `HairEyeOverlapRatio`
- `HairCheekOverlapRatio`
- `HairJawOverlapRatio`
- `HairMouthOverlapRatio`

If hair overlaps a feature, reduce that feature's confidence. Skin smoothing must not blur hair strands, and hair cleanup must not erase eyebrows, eyelashes, or face boundaries.

### Hair Mask Confidence

Suggested confidence:

- `HairMaskConfidence = HairCoreConfidence * 0.35 + HairBoundaryConfidence * 0.25 + HairlineConfidence * 0.20 + StrandConfidence * 0.10 + FlyawayConfidence * 0.10`

Full engine confidence:

- `HairMetricConfidence = facePoseConfidence * hairSegmentationConfidence * edgeMattingConfidence * lightingConfidence * resolutionConfidence * occlusionInverseConfidence * backgroundSeparationConfidence`

Decision:

- High confidence: edge cleanup, color cleanup, and flyaway cleanup allowed.
- Medium confidence: core hair tone cleanup allowed; edge changes must be soft.
- Low confidence: no geometry correction; avoid aggressive background replacement and hard masking.

### Hair Boundary

Boundary metrics:

- `HairBoundarySmoothnessScore`
- `HairBoundaryNoiseScore`
- `HairBoundaryFeatherWidth`
- `HairEdgeTransparencyScore`
- `HairBackgroundBleedScore`
- `HairSkinBleedScore`
- `HairHaloScore`
- `EdgeMattingConfidence`

Hair boundary should not be perfectly hard. Jagged segmentation needs soft cleanup, not shape warping. Use feathered alpha, preserve fine strands when confidence is high, and suppress background color contamination near edges.

### Flyaway Hair

Flyaway hair removal is a manual filter for distracting stray strands. It is not automatic hairstyle correction.

Program rules:

- Run only when the user adjusts the flyaway removal slider or selects the flyaway cleanup tool.
- Do not run automatically when a tab opens.
- Do not remove all hair strands.
- Do not change hairstyle, hairline, hair volume, or silhouette.
- Do not erase natural baby hair around the hairline.
- Do not erase eyelashes, eyebrows, beard, mustache, or sideburns.
- Do not smear skin texture, background, or protected facial details.
- Do not create hard cutout hair edges.
- Default state is no visible correction and no automatic hair cleanup.

Target candidates:

- Isolated stray hairs outside the main hair mass.
- Flyaway hairs crossing clean background.
- Distracting strands crossing forehead, cheek, eye area, mouth area, neck, or clothing.
- Loose single strands separated from hairstyle.
- High-contrast thin hairs against studio background.

Do not target by default:

- Hairline baby hairs.
- Natural fringe or bangs.
- Intentional side hair.
- Eyelashes, eyebrows, beard, mustache, and sideburns.
- Neck hair if part of hairstyle.
- Strands naturally connected to main hair shape.
- Curls or textured hair pattern.
- Wispy hair that defines hairstyle.

Required masks:

- `hairMask`
- `hairCoreMask`
- `hairSoftEdgeMask`
- `hairStrandMask`
- `flyawayHairMask`
- `hairlineMask`
- `bangsMask`
- `babyHairMask`
- `sideHairMask`
- `sideburnMask`
- `faceSkinMask`
- `foreheadSkinMask`
- `cheekSkinMask`
- `eyeProtectionMask`
- `eyelashProtectionMask`
- `eyebrowProtectionMask`
- `lipProtectionMask`
- `earProtectionMask`
- `jawlineProtectionMask`
- `neckProtectionMask`
- `clothingProtectionMask`
- `backgroundMask`
- Optional: `glassesMask`, `jewelryMask`, `beardMask`, `mustacheMask`

Structure zones:

- Hair core: dense main hair mass; never remove as flyaway.
- Hair soft edge: semi-transparent outer boundary; clean carefully, do not hard-cut.
- Hairline baby hair: short fine hair near forehead/hairline; preserve by default.
- Bangs/fringe: intentional front hair; preserve unless explicitly edited.
- Isolated flyaway: thin strand separated from main hair; removal candidate.
- Face-crossing stray hair: remove only if distracting, high confidence, and restoration is safe.
- Background flyaway: outside hair mass on clean background; safest to remove.
- Hair crossing protected features: eye, brow, lash, lip, ear, beard; high caution.

Flyaway metrics:

- `FlyawayHairCount`
- `FlyawayHairLengthAvg`
- `FlyawayHairDirectionVariance`
- `FlyawayDistanceFromCore`
- `FlyawayContrast`
- `FlyawayThinnessScore`
- `FlyawayIsolationScore`
- `FlyawayConnectionScore`
- `FlyawayDirectionScore`
- `FlyawayCurvatureScore`
- `FlyawayDistractingScore`
- `FlyawayProtectedOverlapScore`
- `FlyawayRemovalSafetyScore`
- `FlyawayKeepScore`
- `FlyawayRemoveCandidateScore`

Classification:

- `connectedNaturalHair`: connected to main hairstyle, follows hair direction; preserve.
- `babyHair`: near hairline, short/fine/natural; preserve by default.
- `bangsOrFringe`: part of front hairstyle; preserve.
- `isolatedFlyaway`: separated, thin, high contrast, distracting; removal candidate.
- `faceCrossingFlyaway`: crosses skin or facial feature; remove only with high confidence and safe restoration.
- `backgroundFlyaway`: outside hair mass on clean background; safest removal candidate.
- `eyelashOrEyebrow`: protect, never remove as flyaway.
- `beardOrMustache`: protect.
- `wrinkleOrCrack`: send to wrinkle or lip module.
- `backgroundTextureOrArtifact`: remove only if restoration/artifact module confirms.

Detection flow:

1. Detect `hairCoreMask` and `hairSoftEdgeMask`.
2. Detect thin strand-like components around hair boundary and face.
3. For each component, calculate thickness, length, contrast, connection to hair core, direction compared with hair flow, overlap with protection masks, and restoration safety.
4. Classify the component.
5. Build `flyawayHairMask` only from `isolatedFlyaway`, `backgroundFlyaway`, and distracting `faceCrossingFlyaway` with high confidence.
6. Exclude hairline baby hair, eyelashes, eyebrows, beard, sideburns, intentional bangs, and low-confidence strands.

Slider behavior:

- `0.0`: no removal and no visible change.
- `0.1 ~ 0.3`: remove only high-confidence isolated background flyaways; preserve baby hairs and face-crossing strands unless extremely distracting.
- `0.3 ~ 0.6`: remove high-confidence flyaways on background and some safe skin areas; preserve hairline and protected features.
- `0.6 ~ 0.8`: stronger cleanup; remove more distracting strands crossing skin while protecting hairline, lashes, brows, lips, and beard.
- `0.8 ~ 1.0`: maximum cleanup; preserve natural hairline and hairstyle, trigger over-removal checks, and do not remove connected hair mass.

Slider strength controls candidate threshold and opacity reduction. It must not expand into protected regions automatically or erase all fine hair.

Removal methods:

- Local inpaint along strand mask.
- Background-aware restoration.
- Skin texture-aware restoration.
- Strand opacity reduction.
- Local color replacement.
- Soft mask feathering.
- Direction-aware cleanup.

Avoid global blur, block erasing hair edges, hard clone marks, skin/background smearing, sharp silhouette cuts, and removing all semi-transparent edge hair.

For background flyaways, use nearby background color/texture and preserve gradients. For skin-crossing flyaways, use nearby clean skin texture and preserve pores. For hairline flyaways, prefer opacity reduction over removal.

Protection rules:

- Never remove eyelashes, eyebrows, eyeliner, beard, mustache, intentional sideburn, lip cracks, wrinkles, clothing threads, jewelry details, or glasses frame as flyaway hair.
- Hair crossing eyes, brows, lips, ears, jawline, or textured skin is high caution.
- If a strand overlaps a protection mask, reduce confidence and prefer partial opacity reduction or no action.

Hairline rule:

- Hairline is identity-sensitive.
- Do not clean hairline into a straight artificial edge.
- Do not remove all baby hairs.
- Do not lower or raise hairline.
- Do not create a painted hairline or hard edge.
- `babyHairMask` is protected by default. Strong hairline cleanup requires explicit user intent.

Confidence:

- `FlyawayConfidence = strandDetectionConfidence * hairMaskConfidence * connectionClassificationConfidence * protectionMaskInverseConfidence * restorationSafetyConfidence * lightingConfidence * resolutionConfidence`

Reduce confidence for low resolution, blur, dark hair on dark background, light hair on light background, eye/brow overlap, heavy eye makeup, background texture similar to hair, nearby wrinkles/cracks, beard/sideburn proximity, old photo scratches, and compression artifacts.

Decision:

- High confidence: allow local removal or opacity reduction.
- Medium confidence: allow softening only.
- Low confidence: do not remove; preserve strand.

Over-removal check:

- `OverRemovalCandidate` if hairline becomes too clean, soft edge disappears, hairstyle silhouette changes, baby hairs are removed, flat skin/background patches appear, repeated inpaint texture appears, hair volume looks reduced, or face/hair boundary becomes hard cutout.
- If detected, restore removed strands partially, reduce effective slider strength, protect hairline more strongly, increase edge feather, and reduce mask area.

Safe limits:

- Background flyaway removal: `50% ~ 100%` when confidence is high.
- Skin-crossing flyaway removal: conservative `20% ~ 80%`, depending on confidence and slider.
- Hairline baby hair: default `0%` removal; max `10% ~ 25%` softening unless explicit hairline cleanup.
- Connected hair edge: do not remove; only soft boundary cleanup.
- Face feature overlap: default no removal.
- Hair volume change, hairline movement, and global smoothing: not allowed.

Output:

- `flyawayHairMaskUsed`
- `protectedMaskUsed`
- `removedCandidateMap`
- `softenedCandidateMap`
- `skippedLowConfidenceMap`
- `overRemovalWarning`
- `restorationConfidenceMap`

Debug views:

- Candidate strands.
- Classified flyaway types.
- Protection overlaps.
- Before/after removal mask.

Final flyaway rule:

- Do not define all loose hair as a defect.
- Define whether the strand is isolated flyaway, baby hair, bangs, connected hair, eyelash, eyebrow, beard, wrinkle, artifact, or background texture.
- Preferred usage: manual flyaway cleanup slider, background stray hair removal, distracting face-crossing hair softening, mask refinement, hairline preservation, and over-removal prevention.
- Avoid automatic hair cleanup, removing all baby hairs, hard hair silhouette cuts, hairstyle changes, eyelash/eyebrow erasure, skin/background smearing, hairline movement, and hair volume reduction.
- If confidence is low, do not remove.

### Hair Texture And Direction

Texture metrics:

- `HairDirectionField`
- `HairDirectionCoherence`
- `HairTextureFrequency`
- `HairTextureStrength`
- `HairClumpSize`
- `HairTextureNoiseScore`
- `HairSharpness`

Hair texture should follow direction. Random smoothing destroys hair. Smooth only noise that does not follow hair direction, preserve directional strand detail, and avoid waxy or painted hair.

### Hair Color, Shine, Gray Hair, And Scalp

Color/tone metrics:

- `HairColorMean`, `HairColorMedian`, `HairColorVariance`
- `HairSaturation`, `HairBrightness`
- `HairHighlightMean`, `HairShadowMean`
- `HairRootDarkness`, `HairTipBrightness`
- `HairGrayRatio`, `HairGrayDistribution`, `HairGrayContrast`
- `HairDyePatchinessScore`
- `HairColorBalanceLeftRight`
- `HairColorBleedToSkinScore`

Shine metrics:

- `HighlightMask`
- `HighlightStrength`
- `HighlightAreaRatio`
- `HighlightDirectionConsistency`
- `SpecularHighlightScore`
- `GreasyHairCandidateScore`

Scalp/sparse hair metrics:

- `ScalpVisibleMask`
- `ScalpVisibilityRatio`
- `ScalpSkinColorSimilarity`
- `SparseHairCandidateScore`
- `HairDensityMap`
- `HairDensityBalance`

Gray hair, scalp visibility, parting, and sparse hair are age and identity information. Never remove or fill automatically. If requested, use subtle texture-aware cleanup and preserve parting, root softness, and hair direction.

### Hair Volume And Parting

Volume metrics:

- `HairVolumeTopRatio = (faceTop - hairTop) / faceH`
- `HairVolumeSideRatio = ((faceLeft - hairLeft) + (hairRight - faceRight)) / faceW`
- `LeftHairVolume`, `RightHairVolume`
- `HairVolumeBalanceScore`
- `HairSilhouetteAreaRatio`
- `HairVolumeAsymmetryScore`

Parting/flow metrics:

- `HairPartLineMask`
- `HairPartPositionX`
- `HairPartOffsetRatio`
- `HairFlowLeftDirection`
- `HairFlowRightDirection`
- `HairFlowSymmetryScore`

Parting is personal hairstyle. Do not center it automatically. Avoid blurring across the part line.

### Hair Shadows

Hair shadow metrics:

- `HairShadowOnForeheadScore`
- `HairShadowOnFaceScore`
- `HairShadowOnNeckScore`
- `HairShadowDirectionConsistency`

Hair shadow is not hair. Skin retouch should not treat hair shadow as blemish. Soften harsh shadows while preserving natural lighting.

### Hair Retouch Categories

Use safe order:

1. Mask cleanup: fix leaking hair mask, refine edge feather, separate hair from skin/background.
2. Boundary cleanup: reduce jagged segmentation and halos while preserving strands.
3. Flyaway control: remove only distracting isolated strands.
4. Color/tone correction: balance patchy color and contamination while preserving highlights/shadows.
5. Shine correction: reduce harsh specular highlights while preserving directional shine.
6. Density correction: only if requested or subtle.
7. Geometry/volume correction: last resort, very conservative.

### Hair Safe Limits

- Hairline move: avoid by default; if necessary, `0.3% ~ 1.0%` of `faceH`.
- Outer boundary move: `0.5% ~ 2.0%` of `faceW`, confidence-dependent.
- Hair volume change: `2% ~ 5%` of hair silhouette width/area.
- Flyaway removal: remove only isolated distracting strands; do not remove all fine hair.
- Hair color/darkening: subtle; preserve root/tip and highlight variation.
- Hair smoothing: direction-aware only; do not blur entire hair mass.
- Shine reduction: reduce harsh highlights only; preserve natural shine.
- Gray hair reduction: never automatic.
- Scalp fill: never automatic; if requested, use texture-aware subtle density fill.
- Halo removal: edge-only, soft alpha, preserve strands.

Large hair geometry changes alter identity and hairstyle. Hairline changes alter age and identity. Hair must not become a solid painted mass or hard cutout.

Final hair rule:

- Do not define ideal hair.
- Define where dense hair, soft edge, and fine strands exist.
- Decide whether an edge is real hair, shadow, background, or mask error.
- Prefer hair mask refinement, soft-edge matting, halo removal, tone cleanup, shine control, and protection of face features.
- Avoid automatic hairline filling, automatic volume change, erasing all flyaways, flat solid color, hard cutout edges, gray hair removal without request, and accidental hairstyle change.

## Ears

Ear metrics define ear geometry, visibility, symmetry, occlusion, color, shadow, and ear-face boundary behavior. They are for natural portrait retouch guidance, artifact cleanup, and over-correction prevention. They are not beauty standards.

Core rules:

- Ears are identity-sensitive, but lower priority than eyes, nose, mouth, and jawline.
- Ear correction should mostly handle visibility, color, shadow, deformation, and occlusion.
- Do not force both ears to be identical.
- Do not enlarge, shrink, reposition, or reconstruct ears aggressively.
- Geometry correction comes last.
- Preserve identity, age, natural asymmetry, ear shape, hairstyle, earrings, and inner ear structure.
- Common face landmark models do not provide detailed ear anatomy landmarks.
- Treat ears as `ROI + segmentation + estimated virtual points`, not as direct face-landmark anatomy.
- Do not search for non-existing helix, concha, tragus, or earlobe landmarks in a face landmark model.

### Ear Preconditions

Before ear analysis:

- Detect face landmarks, head pose, hair mask, glasses mask, jawline, cheek boundary, and background.
- Check whether each ear is visible, partially visible, hidden, cropped, blurred, or covered.
- Reduce confidence for yaw, profile pose, hair coverage, glasses arms, earrings, sideburns, beard, shadow, crop, low resolution, blur, old photo damage, or generated-image distortion.

In frontal portraits, ears are often partially hidden by hair. One ear appearing larger is often pose-driven, not anatomical. If one ear is hidden by hair, do not invent a full ear.

### Ear Landmark Availability

Do not assume detailed ear landmarks exist.

Model availability:

- MediaPipe Face Mesh / Face Landmarker provides dense face surface landmarks, but not detailed ear anatomy points such as helix, concha, tragus, or earlobe.
- dlib 68 landmarks provide jaw contour, eyebrows, eyes, nose, and mouth, but not ear landmarks.
- MediaPipe Pose may provide rough `left_ear` / `right_ear` keypoints, but these are approximate ear locations only, not ear shape or inner-ear structure.

Available or estimated inputs:

- Optional `leftEarCenterApprox` and `rightEarCenterApprox` from a pose model.
- If pose ear keypoints are unavailable, estimate ear search ROI from the face box and eye/nose anchors.

Approximate search ROIs:

- `leftEarSearchROI.x = faceLeft - 0.12 * faceW` to `faceLeft + 0.12 * faceW`
- `leftEarSearchROI.y = eyeCenterY - 0.10 * faceH` to `noseBaseCenter.y + 0.12 * faceH`
- `rightEarSearchROI.x = faceRight - 0.12 * faceW` to `faceRight + 0.12 * faceW`
- `rightEarSearchROI.y = eyeCenterY - 0.10 * faceH` to `noseBaseCenter.y + 0.12 * faceH`

Inside each ROI, estimate ear region using:

- Skin-like color.
- Vertical oval-ish contour.
- Edge structure.
- Connection to side face.
- Separation from hair.
- Separation from background.
- Not jawline.
- Not glasses arm.
- Not earring.

Only after an estimated ear mask exists, derive virtual points:

- `leftEarTopEstimated`: topmost point of `leftEarMaskEstimated`.
- `leftEarBottomEstimated`: bottommost point.
- `leftEarOuterEstimated`: outermost point away from face center.
- `leftEarInnerAttachEstimated`: innermost point connected to side face.
- `leftEarCenterEstimated`: centroid or bounding-box center.
- `leftEarlobeEstimated`: lower rounded visible region if confidence is high.
- Equivalent right-ear estimated points.

Do not require detailed inner-ear landmarks by default:

- Helix.
- Antihelix.
- Concha.
- Tragus.
- Antitragus.
- Detailed earlobe contour.
- Inner ear ridge.
- Inner ear hollow.

These may become optional estimated subregions only when the ear is clearly visible, resolution is high, confidence is high, occlusion is low, and inner-ear shadow/edge structure is clear. Otherwise skip inner-ear geometry and allow only safe color, shadow, and boundary cleanup.

### Ear Regions

Target estimated points and masks:

- `leftEarTopEstimated`, `leftEarBottomEstimated`, `leftEarOuterEstimated`, `leftEarInnerAttachEstimated`, `leftEarCenterEstimated`
- `rightEarTopEstimated`, `rightEarBottomEstimated`, `rightEarOuterEstimated`, `rightEarInnerAttachEstimated`, `rightEarCenterEstimated`
- `leftEarlobeEstimated`, `rightEarlobeEstimated` only when confidence is high.
- Optional estimated helix/antihelix/concha/tragus subregions only when visible and reliable.
- `leftEarMaskEstimated`, `rightEarMaskEstimated`
- `leftEarCoreMask`, `rightEarCoreMask`
- `leftEarRimMask`, `rightEarRimMask`
- `leftEarInnerShadowMask`, `rightEarInnerShadowMask`
- `leftEarlobeMask`, `rightEarlobeMask`
- `hairOcclusionMask`, `glassesOcclusionMask`, `earringMask`, `sideburnMask`
- `skinProtectionMask`, `jawProtectionMask`, `backgroundMask`

Ear zones:

- Outer silhouette: visible outer boundary.
- Helix/outer rim: important natural curved rim.
- Antihelix/inner ridge: mostly tone and shadow, not geometry.
- Concha/inner bowl: central hollow, strongly affected by shadow.
- Tragus/front flap: often hidden by sideburn or hair.
- Earlobe: varies strongly by age and person.
- Ear-face attachment: prevents pasted/fake look.
- Occlusion: hair, glasses, earrings, shadow, or background overlap.

Do not treat ears as flat ovals. Most ear retouch should be color/shadow cleanup, not reshaping.

### Ear Size And Position

Soft guide metrics:

- `EarHeightFaceRatio = avgEarH / faceH`: `0.22 ~ 0.34`
- `EarWidthFaceRatio = avgEarW / faceW`: `0.055 ~ 0.105`
- `EarAspectRatio = avgEarH / avgEarW`: `2.2 ~ 3.5`
- `EarlobeHeightEarRatio = avgEarlobeH / avgEarH`: `0.12 ~ 0.25`
- `EarCenterYRatio = ((leftEarCenter.y + rightEarCenter.y) / 2) / faceH`: `0.42 ~ 0.58`

Ear top often aligns around brow/eye level. Ear bottom often aligns around nose base to upper lip. Pitch and camera angle change this strongly, so do not move ears vertically until pose is understood.

### Ear Left/Right Balance

- `EarHeightBalanceScore = abs(leftEarH - rightEarH) / avgEarH`
  - `0.00 ~ 0.08`: natural.
  - `0.08 ~ 0.15`: weak asymmetry or pose.
  - `0.15+`: yaw, occlusion, or detection issue first.
- `EarWidthBalanceScore = abs(leftEarW - rightEarW) / avgEarW`
  - `0.00 ~ 0.12`: natural.
  - `0.12 ~ 0.22`: weak asymmetry or pose.
  - `0.22+`: yaw, hair occlusion, or detection issue first.
- `EarCenterYBalanceScore = abs(leftEarCenter.y - rightEarCenter.y) / faceH`
  - `0.00 ~ 0.020`: natural.
  - `0.020 ~ 0.040`: weak tilt or pose.
  - `0.040+`: face roll, pitch, or detection issue first.
- `EarVisibilityBalanceScore = abs(area(leftEarMask) - area(rightEarMask)) / average area`

Ear balance is unreliable when yaw is present. Do not mirror one ear onto the other automatically.

### Ear Visibility And Occlusion

Visibility metrics:

- `LeftEarVisibilityRatio = area(leftEarVisibleMask) / estimatedLeftEarFullArea`
- `RightEarVisibilityRatio = area(rightEarVisibleMask) / estimatedRightEarFullArea`
- `HairEarOverlapLeft`, `HairEarOverlapRight`
- `GlassesEarOverlapLeft`, `GlassesEarOverlapRight`
- `EarringOverlapRatio`
- `SideburnEarOverlapRatio`

Decision:

- Visibility above `0.75`: geometry metrics may be usable.
- Visibility `0.40 ~ 0.75`: position/color/shadow cleanup only; geometry very small.
- Visibility below `0.40`: no ear shape correction; avoid hidden-ear reconstruction.

If hair covers the ear, preserve hair unless the user asks. Glasses arms and earrings are real objects and must be protected.

### Ear-Face Attachment

Attachment metrics:

- `EarFaceAttachmentLine`
- `EarFaceColorDifference`
- `EarFaceEdgeContrast`
- `EarFaceShadowStrength`
- `EarAttachmentBlendScore`

Ears should not look pasted onto the face. Preserve natural attachment shadow, but soften harsh or dirty-looking shadow. Match ear tone gently to nearby side-face skin, not exactly to the whole face.

### Ear Color, Tone, And Inner Detail

Useful metrics:

- `EarColorMean`
- `FaceSideSkinColorMean`
- `EarFaceColorDelta`
- `EarRednessScore`
- `EarShadowScore`
- `EarHighlightScore`
- `EarTextureScore`
- `HelixContourScore`
- `AntihelixVisibilityScore`
- `ConchaShadowStrength`
- `TragusVisibilityScore`
- `InnerEarDetailScore`

Ears are often naturally redder than face skin. Inner ear shadow is normal. Do not flatten ear color, make ears gray/plastic, or erase inner structure.

### Earlobe

Earlobe metrics:

- `EarlobeHeightEarRatio = earlobeH / earH`: `0.12 ~ 0.25`
- `EarlobeWidthEarWidthRatio = earlobeW / earW`: `0.35 ~ 0.70`
- `EarlobeAttachmentType`: attached, partially attached, or free.
- `EarlobeBalanceScore`
- `EarlobeWrinkleScore`

Earlobe shape is individual and age-related. Clean color/shadow only. Preserve attachment type and age-appropriate texture.

### Ear Interactions

Hair interaction:

- `HairBehindEarCandidate`
- `HairOverEarCandidate`
- `EarHairBoundaryConfidence`
- `EarHairColorBleedScore`
- `EarSkinBleedIntoHairScore`

Glasses interaction:

- `GlassesArmOverEarMask`
- `GlassesEarOverlapRatio`
- `GlassesShadowOnEarScore`

Jawline interaction:

- `EarBottomToJawRelation`
- `EarSideFaceGap`
- `EarJawShadowContinuity`

Retouch must protect hair strands, glasses edges, earrings, jawline shadows, and side-face shadows. Do not create a floating ear.

### Ear Mask Confidence

Suggested confidence:

- `EarMaskConfidence = poseEarKeypointConfidence * earROISkinConfidence * segmentationConfidence * hairOcclusionInverseConfidence * glassesOcclusionInverseConfidence * earringOcclusionInverseConfidence * backgroundSeparationConfidence * lightingConfidence * resolutionConfidence`

Decision:

- High confidence: visibility check, color/tone cleanup, shadow cleanup, and very small boundary refinement allowed.
- Medium confidence: color/tone cleanup only; avoid geometry correction.
- Low confidence: skip ear shape analysis, skip geometry correction, do not reconstruct hidden ear, and do not infer detailed inner-ear structure.

### Ear Retouch Priority

1. Detect visible ear region.
2. Detect hair, glasses, earring, sideburn, background occlusion.
3. Estimate pose/yaw.
4. Classify ear state: visible, partially hidden, hidden by hair, glasses-covered, earring-covered, cropped, shadow-heavy, color-mismatched, distorted/artifact.
5. Separate problem type: color mismatch, redness, harsh shadow, hair overlap, glasses overlap, jagged mask edge, artifact/damage, or true anatomy.
6. Apply safe cleanup first: tone matching, redness softening, inner shadow softening, attachment blend cleanup, hair/ear boundary refinement, glasses protection.
7. Apply geometry only if the ear is clearly visible, pose is near-frontal, confidence is high, deformation is artifact/detection issue, and correction stays within limits.

### Ear Safe Limits

- Ear vertical move: avoid by default; if necessary, `0.3% ~ 1.0%` of `faceH`.
- Ear horizontal move: avoid by default; if necessary, `0.3% ~ 1.0%` of `faceW`.
- Ear height change: `2% ~ 5%` of original `earH`.
- Ear width change: `2% ~ 5%` of original `earW`.
- Earlobe shape change: very small only; prefer tone cleanup.
- Ear rim contour correction: small local artifact correction only.
- Inner ear shadow cleanup: subtle; preserve structure.
- Ear redness reduction: subtle; preserve natural warmth.
- Ear-face blend: soft tone/edge blending only; do not flatten attachment shadow.
- Hidden-ear reconstruction: not allowed by default.

`10%+` ear geometry change is identity-changing. For ID photo, memorial portrait, and restoration, use smaller limits.

Final ear rule:

- Do not define an ideal ear.
- Define whether the ear is visible, hidden, occluded, cropped, pose-driven, color mismatched, shadow-heavy, artifact damaged, or safe to clean.
- Preferred usage: visibility detection, hair/glasses/ear occlusion handling, ear color/redness balancing, inner shadow cleanup, ear-face boundary blending, artifact restoration, and over-correction prevention.
- Avoid forcing both ears to match, automatic resizing/repositioning, hidden-ear reconstruction, earring removal without request, natural fold removal, flattened inner structure, and sharp hair cutouts.
- All ear points are virtual estimated points from pose ear keypoints if available, face-side ROI, ear segmentation mask, skin/hair/background separation, and edge/texture analysis.
- If ear visibility or confidence is low: no ear geometry, no hidden-ear reconstruction, no inner-ear detail generation, only safe tone/shadow cleanup.

## Nose

K Retouch Pro should use nose metrics to judge center drift, width, tilt, exposed nostrils, over-retouch risk, and lighting/pose issues.

Nose metrics are not beauty targets. Shape edits are the last step. Check face rotation, eye line, and lighting first.

Nose surface-flow guide:

- The bridge should usually read as a central bright plane with side planes falling away toward the cheeks.
- A useful reading is not a drawn center line but a tent-like or angled plane transition from bridge to sidewall.
- The tip should read as a rounded bulb surface, not a flat cap.
- Short directional strokes or evidence checks should favor bridge-to-cheek diagonal falloff and rounded tip flow rather than two hard dark outline lines.
- Nostril depth belongs to the inner cavity; do not let nostril darkness replace the bridge, tip, or wing surface reading.

### Nose Anchors

Dense landmark target anchors:

- `eyeInnerL`: left inner eye corner.
- `eyeInnerR`: right inner eye corner.
- `eyeGap`: inner-eye distance.
- `noseRoot`: start of nose bridge below glabella.
- `noseBridgeTop`: upper nose bridge.
- `noseBridgeMid`: middle nose bridge.
- `noseTip`: nose tip.
- `noseBaseCenter`: center under nose wings.
- `noseLeftWing`: outer left nose wing.
- `noseRightWing`: outer right nose wing.
- `nostrilLeft`: left nostril center.
- `nostrilRight`: right nostril center.
- `subnasale`: center point where nose and philtrum meet.

`noseBaseCenter` and `subnasale` can be treated as nearly the same guide point in the first implementation.

### Nose Center

Use nose center after checking face angle:

- `noseCenterX = (noseLeftWing.x + noseRightWing.x) / 2`
- `noseCenterOffset = abs(noseCenterX - faceCenterX) / faceW`

Guide:

- `0.00 ~ 0.02`: centered.
- `0.02 ~ 0.04`: weak drift.
- `0.04 ~ 0.06`: correction candidate.
- `0.06+`: suspect face angle, pose, or strong asymmetry first.

Eye-center reference can be more stable than face outline:

- `eyeCenterX = (eyeL.x + eyeR.x) / 2`
- `noseEyeCenterOffset = abs(noseCenterX - eyeCenterX) / faceW`

Guide:

- `0.00 ~ 0.02`: natural.
- `0.02 ~ 0.04`: weak drift.
- `0.04+`: pose or nose-axis asymmetry candidate.

Center:

- `abs(noseCenterX - faceCenterX) / faceW`: `< 0.03 ~ 0.06`

Vertical position:

- `noseTipY / faceH`: `0.58 ~ 0.66`
- `noseBaseY / faceH`: `0.63 ~ 0.70`

Width:

- `noseW / eyeGap`: `0.9 ~ 1.25`
- `noseW / faceW`: `0.18 ~ 0.25`
- `noseW / mouthW`: `0.45 ~ 0.70`

Natural edit limit:

- Nose width edits should usually stay within `±5% ~ ±8%` from the original.

### Nose Length

Use the distance from `noseRoot` to `noseBaseCenter`.

- `noseLength = noseBaseCenter.y - noseRoot.y`
- `noseLength / faceH`: `0.24 ~ 0.32`
- `noseLength / eyeDist`: `0.75 ~ 1.05`

Guide:

- `<= 0.22`: possible short nose or raised-head pose.
- `0.24 ~ 0.32`: natural range.
- `>= 0.34`: possible long nose or lowered-head pose.

### Nose Width And Wings

Nose width:

- `noseW = noseRightWing.x - noseLeftWing.x`
- `noseW / faceW`: `0.18 ~ 0.25`
- `noseW / eyeGap`: `0.90 ~ 1.25`
- `noseW / mouthW`: `0.45 ~ 0.70`

Guide:

- `< 0.17`: very narrow nose.
- `0.18 ~ 0.25`: natural range.
- `0.26 ~ 0.29`: wide-nose candidate.
- `0.30+`: strong wide-nose or lens/pose effect.

Wing balance:

- `leftWingW = noseTip.x - noseLeftWing.x`
- `rightWingW = noseRightWing.x - noseTip.x`
- `wingBalance = abs(leftWingW - rightWingW) / noseW`

Guide:

- `0.00 ~ 0.06`: natural.
- `0.06 ~ 0.12`: weak asymmetry.
- `0.12 ~ 0.18`: correction candidate.
- `0.18+`: suspect pose or strong asymmetry.

Natural edit limits:

- Nose center move: `1% ~ 2%` of `faceW`.
- Nose width reduction: `3% ~ 7%` from original.
- Nose wing reduction: `3% ~ 8%` from original.

### Nose Bridge

Bridge width:

- `bridgeW / noseW`: `0.28 ~ 0.45`
- `bridgeW / faceW`: `0.06 ~ 0.11`

Bridge drift:

- `bridgeCenterX`: center line from bridge highlight/shadow.
- `bridgeOffset = abs(bridgeCenterX - faceCenterX) / faceW`

Guide:

- `0.00 ~ 0.02`: natural.
- `0.02 ~ 0.04`: weak curve.
- `0.04+`: nose curve or face-angle issue.

Bridge width and bridge center are hard to measure from 5-point landmarks. Prefer dense landmarks plus luminance/shadow analysis.

### Nose Tip

Tip size:

- `tipW / noseW`: `0.45 ~ 0.70`
- `tipH / noseLength`: `0.16 ~ 0.26`

Large tip candidates:

- `tipW / noseW > 0.70`
- `tipH / noseLength > 0.28`

Natural edit limits:

- Tip reduction: `3% ~ 6%` from original.
- Tip vertical move: `0.5% ~ 1.5%` of `faceH`.

Tip edits strongly change identity. Prefer highlight/oil/side-shadow cleanup before geometric tip changes.

### Nostrils

Nostril spacing:

- `nostrilDist = nostrilRight.x - nostrilLeft.x`
- `nostrilDist / noseW`: `0.45 ~ 0.70`

Nostril exposure:

- `nostrilY = (nostrilLeft.y + nostrilRight.y) / 2`
- `nostrilExposeRatio = (nostrilY - noseTip.y) / (noseBaseCenter.y - noseTip.y)`

Guide:

- `0.45 ~ 0.65`: natural front view.
- `<= 0.35`: nostrils less visible or lowered-head pose.
- `>= 0.70`: nostrils strongly visible, raised-head pose, or upturned nose impression.

If nostrils are strongly visible, treat it as a pose problem before treating it as a nose-shape problem.

### Nose Base Line

Nose base line:

- `baseAngle = angle(noseLeftBase, noseRightBase)`

Guide:

- `< 2 deg`: nearly horizontal.
- `2 ~ 5 deg`: weak tilt.
- `5+ deg`: face rotation, expression, or nose asymmetry candidate.

Use this for face/roll checking before changing nose shape.

### Nose To Mouth

Mouth center relation:

- `mouthCenterX = (mouthLeft.x + mouthRight.x) / 2`
- `noseMouthOffset = abs(noseBaseCenter.x - mouthCenterX) / faceW`

Guide:

- `0.00 ~ 0.02`: natural.
- `0.02 ~ 0.04`: weak asymmetry.
- `0.04 ~ 0.06`: correction candidate.
- `0.06+`: expression, pose, or asymmetry issue.

Trust order should generally be eyes first, nose second, mouth third. Mouth moves a lot with expression.

### Nose And Philtrum

- `philtrumH = lipTop.y - noseBaseCenter.y`
- `philtrumH / noseBaseToChin`: `0.22 ~ 0.32`
- `noseLength / philtrumH`: `1.8 ~ 2.8`

If nose and philtrum are both long, the whole lower-middle face may read long. If nose is normal but philtrum is long, treat it as lower-face proportion, not nose.

### Nose Axis Curve

Do not judge a bent nose from one point.

- `noseAxisTop = noseRoot.x`
- `noseAxisMid = noseBridgeMid.x`
- `noseAxisTip = noseTip.x`
- `noseAxisBase = noseBaseCenter.x`
- `bridgeCurve = max(abs(top-mid), abs(mid-tip), abs(tip-base)) / faceW`

Guide:

- `0.00 ~ 0.015`: natural.
- `0.015 ~ 0.030`: weak curve.
- `0.030 ~ 0.050`: correction candidate.
- `0.050+`: suspect face angle or real nose curve.

Correction order:

1. Face rotation.
2. Eye-line leveling.
3. Nose centerline judgment.
4. Very weak bridge/tip adjustment only if the user chooses it.

### Nose Light And 3D Impression

Nose height in a photo is mostly luminance contrast:

- `bridgeHighlightStrength = bridge center brightness - side average brightness`
- `tipHighlightStrength = nose tip brightness - surrounding average brightness`
- `sideShadowStrength = side shadow difference`

Interpretation:

- Strong highlight plus strong shadow: nose reads high.
- Weak highlight plus weak shadow: nose reads flat.
- One-sided shadow: lighting direction or face rotation.

Prefer tonal cleanup over shape changes:

- Reduce excessive bridge highlight.
- Soften excessive wing shadow.
- Remove tip shine.
- Reduce darkness around nostrils without changing shape.

### Program Scores

First useful values:

- `NoseCenterScore = abs(noseCenterX - faceCenterX) / faceW`
- `NoseEyeCenterScore = abs(noseCenterX - eyeCenterX) / faceW`
- `NoseWidthFaceRatio = noseW / faceW`
- `NoseWidthEyeRatio = noseW / eyeGap`
- `NoseLengthRatio = noseLength / faceH`
- `NoseWingBalanceScore = abs(leftWingW - rightWingW) / noseW`
- `NoseMouthCenterScore = abs(noseBaseCenter.x - mouthCenterX) / faceW`
- `NoseBridgeCurveScore = maxAxisDeviation / faceW`
- `NostrilExposeRatio = (nostrilY - noseTip.y) / (noseBaseCenter.y - noseTip.y)`

Score guide:

- `0.00 ~ 0.03`: natural.
- `0.03 ~ 0.06`: weak correction candidate.
- `0.06 ~ 0.10`: clear correction candidate.
- `0.10+`: suspect angle, expression, or detection error first.

One-line rule:

Nose shape changes come last. First remove face angle and lighting problems, then weakly correct only unusually strong values within about `3% ~ 7%` from the original.

## Mouth And Lips

Mouth and lip geometry should guide natural retouching and prevent over-correction. It must not become a fixed beauty standard.

The mouth is highly expression-dependent. If the mouth is open, teeth are visible, the subject is smiling, talking, pressing lips, wearing heavy lipstick, has beard/mustache occlusion, or the face has strong yaw/pitch, lower all mouth metric confidence.

### Mouth And Lip Anchors

Dense landmark target anchors:

- `mouthLeftCorner`, `mouthRightCorner`
- `upperLipTopCenter`, `upperLipCupidLeft`, `upperLipCupidRight`
- `upperLipCenter`, `upperLipLeftPeak`, `upperLipRightPeak`
- `lowerLipBottomCenter`, `lowerLipCenter`
- `mouthInnerTop`, `mouthInnerBottom`, `mouthInnerLeft`, `mouthInnerRight`
- `upperLipOuterContourPoints[]`, `lowerLipOuterContourPoints[]`
- `upperLipInnerContourPoints[]`, `lowerLipInnerContourPoints[]`
- `teethTopVisibleLine`, `teethBottomVisibleLine`
- `teethLeftVisible`, `teethRightVisible`

Primary derived values:

- `mouthCenter.x = (mouthLeftCorner.x + mouthRightCorner.x) / 2`
- `mouthCenter.y = (mouthLeftCorner.y + mouthRightCorner.y) / 2`
- `mouthW = distance(mouthLeftCorner, mouthRightCorner)`
- `lipH = lowerLipBottomCenter.y - upperLipTopCenter.y`
- `innerMouthH = mouthInnerBottom.y - mouthInnerTop.y`
- `innerMouthW = mouthInnerRight.x - mouthInnerLeft.x`
- `upperLipH = mouthInnerTop.y - upperLipTopCenter.y`
- `lowerLipH = lowerLipBottomCenter.y - mouthInnerBottom.y`
- `visibleLipH = upperLipH + lowerLipH`
- `philtrumH = upperLipTopCenter.y - noseBaseCenter.y`
- `noseBaseToChin = chinPoint.y - noseBaseCenter.y`
- `lowerLipToChin = chinPoint.y - lowerLipBottomCenter.y`
- `mouthToChin = chinPoint.y - mouthCenter.y`

### Mouth Position

- `MouthCenterYRatio = mouthCenter.y / faceH`: `0.72 ~ 0.80`
- `MouthCenterXScore = abs(mouthCenter.x - faceCenterX) / faceW`
  - `0.00 ~ 0.03`: natural.
  - `0.03 ~ 0.05`: weak offset.
  - `0.05+`: check expression, yaw, or detection first.
- `MouthNoseCenterScore = abs(mouthCenter.x - noseBaseCenter.x) / faceW`
  - `0.00 ~ 0.03`: natural.
  - `0.03 ~ 0.05`: weak asymmetry.
  - `0.05+`: expression, yaw, mouth pull, or detection issue.
- `MouthEyeCenterScore = abs(mouthCenter.x - eyeCenterX) / faceW`
  - `0.00 ~ 0.04`: natural.
  - `0.04+`: check yaw, crop, or expression first.

Do not move mouth center unless eye, nose, and chin alignment all support it.

### Mouth Width

- `MouthWidthFaceRatio = mouthW / faceW`: `0.32 ~ 0.42`
- `MouthWidthEyeDistRatio = mouthW / eyeDist`: `0.75 ~ 1.05`
- `MouthWidthNoseRatio = mouthW / noseW`: `1.45 ~ 2.20`
- `MouthWidthLowerFaceRatio = mouthW / noseBaseToChin`: `0.85 ~ 1.35`

Small mouth candidate:

- `mouthW / faceW < 0.30` and `mouthW / eyeDist < 0.72`

Wide mouth candidate:

- `mouthW / faceW > 0.44` or `mouthW / eyeDist > 1.10`

Correction limit:

- Mouth width change: `3% ~ 7%` of original `mouthW`.
- `10%+` changes can strongly alter identity and expression.

### Mouth Height And Openness

- `LipHeightMouthWidthRatio = lipH / mouthW`: `0.22 ~ 0.38`
- `LipHeightFaceRatio = lipH / faceH`: `0.045 ~ 0.075`
- `InnerMouthOpenRatio = innerMouthH / mouthW`
  - `0.00 ~ 0.08`: closed/neutral.
  - `0.08 ~ 0.16`: slightly open.
  - `0.16+`: open mouth, speaking, or smile.
- `InnerMouthWidthRatio = innerMouthW / mouthW`
  - `0.00 ~ 0.45`: closed/neutral.
  - `0.45+`: smile/open expression.

If inner mouth is large, reduce all lip-shape correction confidence. Do not force closed-mouth ratios onto open-mouth photos.

### Upper And Lower Lip

- `UpperLowerLipRatio = upperLipH / lowerLipH`: `0.55 ~ 0.85`
- `LowerUpperLipRatio = lowerLipH / upperLipH`: `1.20 ~ 1.80`
- `UpperLipMouthWidthRatio = upperLipH / mouthW`: `0.08 ~ 0.16`
- `LowerLipMouthWidthRatio = lowerLipH / mouthW`: `0.13 ~ 0.24`

Interpretation:

- Lower lip is usually thicker than upper lip.
- Very thick upper lip: `upperLipH / lowerLipH > 1.00`
- Very thin upper lip: `upperLipH / lowerLipH < 0.45`
- Very thick lower lip: `lowerLipH / upperLipH > 2.00`

Correction limit:

- Upper/lower lip height changes: `3% ~ 8%` of original height.
- Avoid making upper and lower lips equal unless explicitly requested.

### Philtrum And Lower Face

- `PhiltrumLowerFaceRatio = philtrumH / noseBaseToChin`: `0.22 ~ 0.32`
- `PhiltrumLipRatio = philtrumH / lipH`: `1.00 ~ 1.80`
- `NoseToMouthCenterRatio = (mouthCenter.y - noseBaseCenter.y) / noseBaseToChin`: `0.35 ~ 0.48`
- `LipToChinRatio = lowerLipToChin / noseBaseToChin`: `0.38 ~ 0.55`
- `LowerFaceSegmentRatio = philtrumH : lipH : lowerLipToChin`: approximately `1 : 1 : 2.2`

Long philtrum candidate:

- `philtrumH / noseBaseToChin > 0.34`

Short philtrum candidate:

- `philtrumH / noseBaseToChin < 0.20`

Philtrum geometry change is risky. Prefer subtle tone/shadow correction around the philtrum. Mouth vertical movement should usually stay within `0.5% ~ 1.5%` of `faceH`.

### Philtrum Anchor, Distance, And Ratio

Philtrum analysis defines anchors, position, distance ratios, spacing rules, and confidence checks for the region between the nose base and upper lip. It is for measurement, protection, mask placement, and safe local retouch guidance. It is not automatic reshaping.

Core rules:

- Do not change nose-lip distance automatically.
- Do not move upper lip, nose base, cupid bow, or mouth position.
- Philtrum anchors are measurement guides, not correction commands.
- Nose base, upper lip boundary, nostrils, cupid bow, and mustache are protected.

Primary anchors:

- `noseBaseCenter` / `subnasale`: top philtrum anchor, centered under the nose between nostril base points.
- `upperLipTopCenter`: bottom philtrum anchor, highest center point of upper lip boundary near cupid bow center.
- `philtrumCenter`: midpoint between `noseBaseCenter` and `upperLipTopCenter`.
- `philtrumColumnCenterLine`: vertical line from `noseBaseCenter` to `upperLipTopCenter`.
- `leftPhiltrumRidgeEstimated`, `rightPhiltrumRidgeEstimated`: raised ridges estimated from highlight/shadow, not usually direct landmarks.
- `leftPhiltrumGrooveEstimated`, `rightPhiltrumGrooveEstimated`: shallow groove/shadow points inside the philtrum.
- `upperLipCupidLeft`, `upperLipCupidRight`: lower boundary references.
- `noseLeftBase`, `noseRightBase`: lower nose/alar base anchors.

`PhiltrumSearchROI`:

- `x_start = min(noseLeftBase.x, upperLipCupidLeft.x) - 0.035 * faceW`
- `x_end = max(noseRightBase.x, upperLipCupidRight.x) + 0.035 * faceW`
- `y_start = noseBaseCenter.y - 0.010 * faceH`
- `y_end = upperLipTopCenter.y + 0.025 * faceH`

The ROI is only a search area. The final `philtrumMask` must exclude lips, nostrils, mustache hair, beard/shaving shadow, strong nose shadow, and makeup bleed.

Position guide in normalized face coordinates:

- `noseBaseCenter.ny`: `0.63 ~ 0.70`
- `upperLipTopCenter.ny`: `0.69 ~ 0.75`
- `philtrumCenter.ny`: `0.66 ~ 0.72`
- `mouthCenter.ny`: `0.72 ~ 0.80`
- `chinPoint.ny`: `0.96 ~ 1.00`
- `noseBaseCenter.nx`, `upperLipTopCenter.nx`, `philtrumCenter.nx`: `0.48 ~ 0.52`
- `mouthCenter.nx`: `0.47 ~ 0.53`

These are soft guides only. Face yaw, expression, camera angle, and natural asymmetry can shift values. Do not force these values by reshaping.

Core distances:

- `PhiltrumHeight = distance(noseBaseCenter, upperLipTopCenter)`
- `NoseToMouthDistance = distance(noseBaseCenter, mouthCenter)`
- `NoseToChinDistance = distance(noseBaseCenter, chinPoint)`
- `UpperLipHeight = distance(upperLipTopCenter, upperLipLowerBoundaryCenter)`
- `MouthToChinDistance = distance(mouthCenter, chinPoint)`
- `LowerFaceHeight = distance(noseBaseCenter, chinPoint)`
- `MouthWidth = distance(mouthLeftCorner, mouthRightCorner)`
- `NoseWidth = distance(noseLeftWing, noseRightWing)`
- `CupidBowWidth = distance(upperLipCupidLeft, upperLipCupidRight)`
- `PhiltrumRidgeWidth = distance(leftPhiltrumRidgeEstimated, rightPhiltrumRidgeEstimated)`

Main ratio guide:

- `PhiltrumHeightFaceRatio = PhiltrumHeight / faceH`: common `0.045 ~ 0.085`, soft extended `0.040 ~ 0.100`
- `PhiltrumLowerFaceRatio = PhiltrumHeight / LowerFaceHeight`: common `0.18 ~ 0.30`, soft extended `0.15 ~ 0.35`
- `PhiltrumNoseToMouthRatio = PhiltrumHeight / NoseToMouthDistance`: common `0.45 ~ 0.70`
- `PhiltrumMouthToChinRatio = PhiltrumHeight / MouthToChinDistance`: common `0.18 ~ 0.35`
- `PhiltrumUpperLipRatio = PhiltrumHeight / UpperLipHeight`: common `1.00 ~ 2.20`, varies strongly by lip thickness
- `PhiltrumMouthWidthRatio = PhiltrumHeight / MouthWidth`: common `0.12 ~ 0.25`
- `PhiltrumNoseWidthRatio = PhiltrumHeight / NoseWidth`: common `0.35 ~ 0.70`
- `PhiltrumRidgeWidthMouthRatio = PhiltrumRidgeWidth / MouthWidth`: common `0.16 ~ 0.35`
- `PhiltrumRidgeWidthNoseRatio = PhiltrumRidgeWidth / NoseWidth`: common `0.30 ~ 0.60`
- `CupidBowPhiltrumWidthRatio = CupidBowWidth / PhiltrumRidgeWidth`: common `0.80 ~ 1.40`

Lower-face section ratios:

- `PhiltrumSectionRatio = PhiltrumHeight / LowerFaceHeight`
- `UpperMouthSectionRatio = distance(upperLipTopCenter, mouthCenter) / LowerFaceHeight`
- `ChinSectionRatio = distance(mouthCenter, chinPoint) / LowerFaceHeight`

Interpretation:

- A long philtrum impression can come from thin upper lip, weak cupid bow, strong mustache shadow, downward mouth expression, camera angle, nose shadow, or makeup/foundation mismatch.
- A short philtrum impression can come from thick upper lip, lifted upper lip expression, smile, open mouth, lip color bleeding upward, or shadow hiding the lower philtrum boundary.
- Do not classify long/short philtrum from `PhiltrumHeight` alone.

Alignment metrics:

- `PhiltrumFaceCenterOffset = abs(philtrumCenter.x - faceCenterX) / faceW`: normal `0.00 ~ 0.025`, caution `0.025 ~ 0.045`, low confidence `0.045+`
- `PhiltrumNoseAlignmentOffset = abs(philtrumCenter.x - noseBaseCenter.x) / faceW`: normal `0.00 ~ 0.020`, caution `0.020 ~ 0.040`
- `PhiltrumLipAlignmentOffset = abs(philtrumCenter.x - upperLipTopCenter.x) / faceW`: normal `0.00 ~ 0.020`, caution `0.020 ~ 0.040`
- `PhiltrumMouthAlignmentOffset = abs(philtrumCenter.x - mouthCenter.x) / faceW`: normal `0.00 ~ 0.030`, caution `0.030 ~ 0.050`

Center line rule:

- `noseBaseCenter`, `philtrumCenter`, `upperLipTopCenter`, and `mouthCenter` should roughly align.
- Do not auto-correct misalignment.
- Use offsets only for confidence, mask placement, and warning.

Ridge and groove spacing:

- `PhiltrumGrooveSpacing = distance(leftPhiltrumGrooveEstimated, rightPhiltrumGrooveEstimated)`
- `LeftRidgeToCenterDistance = abs(leftPhiltrumRidgeEstimated.x - philtrumCenter.x)`
- `RightRidgeToCenterDistance = abs(rightPhiltrumRidgeEstimated.x - philtrumCenter.x)`
- `RidgeSymmetryScore = abs(LeftRidgeToCenterDistance - RightRidgeToCenterDistance) / PhiltrumRidgeWidth`
- `PhiltrumRidgeWidthFaceRatio = PhiltrumRidgeWidth / faceW`: common `0.045 ~ 0.090`
- `PhiltrumRidgeWidthNoseWidthRatio = PhiltrumRidgeWidth / NoseWidth`: common `0.30 ~ 0.60`

Ridge/groove points are estimated from light and shadow. Heavy mustache, shaving shadow, makeup, low resolution, or old photo damage lowers confidence.

Shape features:

- `straightColumn`
- `slightlyVShape`
- `widePhiltrum`
- `narrowPhiltrum`
- `flatPhiltrum`
- `deepPhiltrum`

Feature scores:

- `PhiltrumLengthScore`
- `PhiltrumWidthScore`
- `PhiltrumDepthScore`
- `PhiltrumFlatnessScore`
- `PhiltrumShadowHarshnessScore`
- `PhiltrumTextureScore`
- `PhiltrumHairOverlapScore`
- `PhiltrumLipBoundaryRiskScore`
- `PhiltrumNostrilRiskScore`

Reduce confidence if:

- `mustacheMask` overlaps philtrum.
- `shavingShadowMask` overlaps philtrum.
- Upper lip mask confidence is low.
- Nose base landmark is unstable.
- Nostril shadow is strong.
- Mouth is open.
- Smile or speech expression is present.
- Face yaw or pitch is high.
- Resolution is low or motion blur exists.
- Heavy makeup/foundation or old photo damage exists.

Suggested confidence:

- `PhiltrumAnchorConfidence = noseBaseCenterConfidence * upperLipTopCenterConfidence * lipBoundaryConfidence * nostrilProtectionConfidence * mustacheInverseConfidence * poseConfidence * resolutionConfidence * lightingConfidence`

Decision:

- High confidence: clear nose base, clear upper lip boundary, no heavy mustache, neutral expression, frontal pose.
- Medium confidence: mild shadow, makeup, or mustache; use broad ROI and conservative correction.
- Low confidence: heavy mustache, unclear lip boundary, open mouth, strong expression, low resolution; do not measure fine ratios strongly, protect only.

Use philtrum ratios for:

- Philtrum ROI placement.
- Upper lip boundary protection.
- Mustache/shaving shadow separation.
- Local tone/shadow correction limits.
- Confidence scoring.
- Debug measurement.

Do not use philtrum ratios for:

- Automatic nose-lip distance correction.
- Automatic lip lifting.
- Automatic upper lip reshaping.
- Automatic philtrum shortening.
- Automatic face proportion beautification.

Output should include:

- `philtrumAnchors`: `noseBaseCenter`, `upperLipTopCenter`, `philtrumCenter`, `philtrumColumnCenterLine`, `leftPhiltrumRidgeEstimated`, `rightPhiltrumRidgeEstimated`, `leftPhiltrumGrooveEstimated`, `rightPhiltrumGrooveEstimated`
- `philtrumRatios`: `PhiltrumHeightFaceRatio`, `PhiltrumLowerFaceRatio`, `PhiltrumNoseToMouthRatio`, `PhiltrumMouthToChinRatio`, `PhiltrumUpperLipRatio`, `PhiltrumMouthWidthRatio`, `PhiltrumNoseWidthRatio`, `PhiltrumRidgeWidthMouthRatio`, `PhiltrumRidgeWidthNoseRatio`
- `alignment`: `PhiltrumFaceCenterOffset`, `PhiltrumNoseAlignmentOffset`, `PhiltrumLipAlignmentOffset`, `PhiltrumMouthAlignmentOffset`
- `confidence`: `PhiltrumAnchorConfidence`, `PhiltrumMaskConfidence`, `PhiltrumRidgeGrooveConfidence`
- `warnings`: `mustacheOverlap`, `lipBoundaryUnclear`, `nostrilShadowStrong`, `poseDistortion`, `expressionDistortion`, `lowResolution`

Final philtrum anchor rule:

- Anchor and ratio values are used to find and protect the philtrum area.
- They must not trigger automatic reshaping.
- Nose base, upper lip boundary, nostrils, cupid bow, and mustache are protected.

### Mouth Corners And Expression

- `MouthCornerLevelScore = abs(mouthLeftCorner.y - mouthRightCorner.y) / faceH`
  - `0.00 ~ 0.015`: natural.
  - `0.015 ~ 0.030`: weak asymmetry.
  - `0.030+`: expression, head tilt, or detection issue first.
- `MouthCornerSlopeDeg = atan2(mouthRightCorner.y - mouthLeftCorner.y, mouthRightCorner.x - mouthLeftCorner.x) * 180 / PI`
- `SmileCurveRatio = ((mouthLeftCorner.y + mouthRightCorner.y) / 2 - mouthInnerTop.y) / mouthW`
- `SmileUpturnRatio = ((upperLipTopCenter.y + lowerLipBottomCenter.y) / 2 - (mouthLeftCorner.y + mouthRightCorner.y) / 2) / mouthW`

Corners higher than the center indicate smile/upturn. Corners lower than the center can indicate relaxed aging mouth or downturned expression. Do not neutralize expression unless requested.

Correction limit:

- Corner vertical move: `0.5% ~ 1.5%` of `faceH`.
- Corner horizontal move: `1% ~ 3%` of `mouthW`.

### Lip Contour

- `CupidBowWidthRatio = UpperLipCupidBowWidth / mouthW`: `0.18 ~ 0.32`
- `CupidBowDepthRatio = abs(CupidBowDepth) / upperLipH`: `0.15 ~ 0.45`
- `UpperLipPeakBalanceScore = abs(upperLipCupidLeft.y - upperLipCupidRight.y) / upperLipH`
  - `0.00 ~ 0.15`: natural.
  - `0.15+`: asymmetry, expression, or detection issue.
- `UpperLipContourSymmetryScore`: mirrored upper contour difference divided by `mouthW`.
- `LowerLipContourSymmetryScore`: mirrored lower contour difference divided by `mouthW`.

Cupid bow is highly individual. Prefer edge cleanup and color boundary cleanup over shape deformation.

### Left/Right Lip Balance

- `leftMouthHalfW = mouthCenter.x - mouthLeftCorner.x`
- `rightMouthHalfW = mouthRightCorner.x - mouthCenter.x`
- `MouthWidthBalanceScore = abs(leftMouthHalfW - rightMouthHalfW) / mouthW`
  - `0.00 ~ 0.06`: natural.
  - `0.06 ~ 0.12`: weak asymmetry.
  - `0.12+`: expression, yaw, or correction candidate.
- `UpperLipAreaBalanceScore`: left/right upper lip area difference divided by average.
- `LowerLipAreaBalanceScore`: left/right lower lip area difference divided by average.
  - `0.00 ~ 0.12`: natural.
  - `0.12 ~ 0.20`: weak asymmetry.
  - `0.20+`: expression, lighting, or detection issue.

Lip area is affected by lighting, lipstick, teeth, and smile. Use area balance only when lip mask confidence is high.

### Teeth And Inner Mouth

- `TeethHeightMouthRatio = TeethVisibleH / lipH`
- `TeethWidthMouthRatio = TeethVisibleW / mouthW`
- `TeethExposureScore = TeethVisibleH / innerMouthH`

If teeth are visible:

- Prioritize color/tone cleanup.
- Avoid strong lip contour reshaping.
- Avoid changing mouth openness.
- Avoid moving corners unless asymmetry is extreme.
- Preserve natural smile.

### Mouth Color And Tone

Useful tone metrics:

- `LipColorMean`
- `UpperLipColorMean`
- `LowerLipColorMean`
- `SurroundingSkinColorMean`
- `LipSkinContrast`
- `UpperLowerLipColorDifference`
- `LipSaturationRatio`
- `LipEdgeContrast`
- `DarkCornerScore`
- `PhiltrumShadowScore`
- `LowerLipHighlightScore`

Natural mouth retouch should often adjust tone more than shape. Clean mouth-corner darkness gently, avoid flat oversaturated lips, and preserve skin texture around the mouth.

### Mouth Masks

Recommended masks:

- Outer lip mask.
- Upper lip mask.
- Lower lip mask.
- Inner mouth mask.
- Teeth mask.
- Left/right mouth corner masks.
- Philtrum mask.
- Near-mouth nasolabial fold mask.
- Surrounding skin protection mask.

Mask usage:

- Shape correction uses outer lip contour only with high confidence.
- Color correction uses lip masks.
- Teeth correction uses teeth mask only.
- Mouth corner cleanup uses local corner masks.
- Philtrum correction must not bleed into upper lip.
- Surrounding skin mask protects skin texture and must not be over-smoothed.

### Mouth Confidence Rules

Reduce confidence if:

- Mouth is open.
- Teeth are visible.
- Strong smile, talking expression, or pressed lips.
- One corner is hidden by pose.
- Beard or mustache covers lip boundary.
- Strong lipstick distorts natural boundary.
- Blur or shadow hides lip contour.
- Face yaw/pitch is large.
- Landmark confidence is low.

Suggested confidence:

- `MouthMetricConfidence = frontalPoseConfidence * mouthLandmarkConfidence * lipMaskConfidence * expressionNeutralConfidence * visibilityConfidence`

### Mouth Retouch Priority

1. Check face roll and pose.
2. Verify mouth and lip landmark confidence.
3. Detect expression state: neutral, smile, open, teeth visible, speaking.
4. Analyze mouth center alignment with eyes and nose.
5. Analyze mouth width and lip height.
6. Analyze upper/lower lip balance.
7. Analyze mouth-corner asymmetry.
8. Analyze philtrum and lower-face relation.
9. Apply local tone cleanup first.
10. Apply minimal geometry only if multiple metrics agree.

Mouth is more expression-sensitive than nose or eyes. Do not force symmetry or change expression accidentally.

### Mouth Safe Correction Limits

- Mouth center move: `0.5% ~ 1.5%` of `faceW` or `faceH`.
- Mouth width change: `3% ~ 7%` of original `mouthW`.
- Lip height change: `3% ~ 8%` of original `lipH`.
- Upper/lower lip height change: `3% ~ 8%` of original height.
- Mouth corner vertical move: `0.5% ~ 1.5%` of `faceH`.
- Mouth corner horizontal move: `1% ~ 3%` of `mouthW`.
- Cupid bow shape change: very small only.
- Philtrum geometry change: avoid if possible; prefer tone/shadow correction.
- Teeth whitening: subtle only; preserve natural tooth texture and shadow.

Geometry changes above `10%` are likely identity-changing. For ID photo, memorial portrait, and restoration, use even smaller limits.

Mouth vertical position:

- `mouthCenterY / faceH`: `0.72 ~ 0.80`
- `(mouthCenterY - noseBaseY) / (chinY - noseBaseY)`: `0.35 ~ 0.48`

Mouth width:

- `mouthW / eyeDist`: `0.75 ~ 1.05`
- `mouthW / faceW`: `0.32 ~ 0.42`

Natural edit limit:

- Mouth width edits should usually stay within `±5% ~ ±10%` from the original.

Lip height:

- `lipH / mouthW`: `0.22 ~ 0.38`
- `lipH / faceH`: `0.045 ~ 0.075`

Upper/lower lip ratio:

- `upperLipH / lowerLipH`: `0.55 ~ 0.85`
- `lowerLipH / upperLipH`: `1.2 ~ 1.8`

Avoid making the upper lip too large or the lower lip too thin unless the user explicitly chooses it.

### Lip Dryness, Chapped Lips, And Lip Wrinkles

Lip dryness metrics define chapped lips, cracks, peeling skin, dry patches, vertical lip lines, lip color unevenness, and lip-boundary damage. This module restores local lip surface quality; it does not change lip size or shape.

Lip landmarks alone are not enough to measure lip surface problems. Landmark-only data can measure mouth position, mouth width, lip height, upper/lower lip ratio, mouth-corner level, mouth openness, and outer lip geometry. It cannot reliably measure chapped lips, cracks, peeling skin, lip wrinkles, lip texture, dry patches, color patchiness, lipstick cracking, or lip highlight/shadow texture.

These require a real `lipSurfaceMask`, not just contour landmarks.

Core rules:

- Lip texture is natural.
- Lip wrinkles are not all defects.
- Chapped cracks and peeling skin can be softened or restored.
- Do not make lips look like flat plastic.
- Preserve natural lip volume, color variation, fine vertical texture, mouth expression, and natural highlights.
- Do not over-smooth the vermilion border.
- Protect teeth, inner mouth, skin around mouth, mustache/beard, philtrum, nasolabial folds, and mouth corners.
- Lip boundary evidence should use color, tone, and texture-direction change together rather than color alone.
- When lip color is weak or close to skin tone, compare the local tissue flow of lip, philtrum, perioral skin, and lower-lip-to-chin skin.

Definitions:

- `lipTexture`: natural fine surface texture, small vertical lines, soft color variation, and micro highlights.
- `lipWrinkle`: natural vertical or curved line on lip surface.
- `deepLipCrease`: deeper line from dryness, age, expression, or compression.
- `chappedLip`: dry, cracked, rough, peeling, or flaky lip surface.
- `lipCrack`: sharp dark or bright broken line from dryness or split skin.
- `lipPeeling`: lifted flaky skin, often with bright edge or rough patch.
- `lipDryPatch`: matte, rough, pale, grayish, or desaturated dry area.
- `lipCornerCrack`: small crack or dark split near mouth corner.
- `lipColorPatchiness`: uneven lip color.
- `lipBoundaryDamage`: unclear or broken vermilion border from dryness, makeup, blur, or retouch error.

Required masks:

- `outerLipMask`
- `upperLipMask`
- `lowerLipMask`
- `lipSurfaceMask`
- `innerMouthMask`
- `teethMask`
- `mouthCornerLeftMask`, `mouthCornerRightMask`
- `vermilionBorderMask`
- `lipTextureMask`
- `lipWrinkleMask`
- `deepLipCreaseMask`
- `lipCrackMask`
- `lipPeelingMask`
- `lipDryPatchMask`
- `lipColorPatchinessMask`
- `lipHighlightMask`
- `lipShadowMask`
- `lipstickMask`
- `lipTintMask`

Temporary rule if `lipSurfaceMask` is missing:

- Skip lip dryness measurement.
- Skip lip crack measurement.
- Skip lip wrinkle measurement.
- Skip lip peeling measurement.
- Do not apply lip surface correction.
- Only allow geometry and position checks from landmarks.
- Do not guess lip texture from landmarks.

Fallback:

- Use landmarks only to create a rough lip ROI.
- Build `lipSurfaceMask` from lip color segmentation, edge detection, inner-mouth exclusion, teeth exclusion, surrounding-skin exclusion, upper/lower lip separation, and a soft feathered boundary.

Absolute rule:

- No lip surface correction without `lipSurfaceMask` confidence.

Lip guide-centered directional texture evidence:

- Lip directional/phase texture detection is not a standalone whole-lip analyzer.
- First create or estimate a lip 3D guide, guide centerline, or local guide search mask from mouth/lip anchors.
- The guide is a search start and local curve reference only; it is not the final correction mask.
- Build `LipGuideSearchBand` around the guide centerline, then expand it into two long surface planes: one upper-lip plane and one lower-lip plane.
- The two planes may be deliberately wider than the centerline band so the analyzer can reach the lip ends; inner-mouth protection remains the hard exclusion, while vermilion-side support can stay soft.
- Inside this guide band, detect directional lip texture, natural lip lines, dry cracks, rough dry patches, gloss breaks, and border evidence.
- If confidence is high, create candidate masks such as `lipLineCandidateMask`, `lipCrackCandidateMask`, `lipDryPatchCandidateMask`, and `lipTextureEvidenceMask`.
- If confidence is low, keep protect-only behavior and do not guess from landmarks.
- Visible correction still requires an explicit lip tool or slider.

Texture-direction guide around the mouth:

- Lip surface usually shows fine vertical or slightly curved micro-line flow.
- Upper-lip skin above the vermilion border transitions away from lip-line texture and into softer skin grain.
- The philtrum usually reads as a center groove with two ridges and a more vertical downward flow from nose base toward upper lip.
- Skin around the mouth often follows the orbicular mouth-muscle flow, wrapping around the lips rather than matching lip texture.
- Skin below the lower lip usually shifts into a broader chin-surface flow, often softer and more downward than the lip texture above.
- If lip color evidence is weak, use these texture-direction transitions as boundary evidence before widening correction.
- If both color and texture-direction evidence are weak, lower confidence and protect wider instead of forcing a lip edge.

Protection masks:

- `skinAroundMouthProtectionMask`
- `teethProtectionMask`
- `innerMouthProtectionMask`
- `mustacheProtectionMask`
- `beardProtectionMask`
- `mouthCornerProtectionMask`
- `philtrumProtectionMask`
- `nasolabialFoldProtectionMask`

Lip dryness zones:

- Upper lip body: usually thinner, contains fine vertical lines, often darker or less highlighted.
- Lower lip body: usually fuller, stronger highlights and visible texture, common dryness/peeling area.
- Lip center: most visible texture and cracks.
- Lip corners: prone to dark cracks, saliva shadows, and dryness; easily confused with mouth-corner wrinkles.
- Vermilion border: lip-skin boundary; soft but clear.
- Inner lip edge: boundary with inner mouth/teeth; must not bleed into teeth or mouth cavity.
- Dry flake regions: lifted rough skin; local cleanup allowed.
- Lip makeup region: lipstick/tint/gloss can hide true dryness and must be separated.

Primary metrics:

- `LipTextureStrength`
- `LipVerticalLineDensity`
- `LipWrinkleDepthScore`
- `DeepLipCreaseScore`
- `LipCrackSharpnessScore`
- `LipCrackDepthScore`
- `LipCrackLength`
- `LipPeelingAreaRatio`
- `LipDryPatchAreaRatio`
- `LipDrynessScore`
- `LipColorPatchinessScore`
- `LipHighlightBalanceScore`
- `LipTextureLossScore`
- `PlasticLipScore`

Lip line classification:

- `naturalFineLipLine`: thin repeated vertical/curved lines; mostly preserve.
- `deepLipCrease`: stronger, wider line; soften contrast only.
- `dryCrack`: sharp broken irregular line; repair more aggressively.
- `peelingEdge`: raised flake edge; local cleanup allowed.
- `makeupCrack`: lipstick/tint discontinuity; correct makeup/tone, not geometry.
- `shadowLine`: lighting or mouth-shape shadow; soften only if dirty/harsh.
- `photoArtifactLine`: compression/scanner/scratch; restore if confirmed artifact.

Rules:

- Preserve natural fine lip lines.
- Soften deep creases.
- Repair dry cracks and peeling edges locally.
- Do not flatten all lip texture.

Chapped lip metrics:

- `ChappedLipMask`
- `ChappedLipSeverityScore`
- `ChappedLipColorScore`
- `ChappedLipRoughnessScore`
- `ChappedLipBoundaryDamageScore`

Chapped-lip correction:

- Reduce sharp crack contrast.
- Remove or soften peeling flakes.
- Restore local lip color.
- Restore soft lip texture.
- Preserve natural vertical lip lines.
- Preserve lip volume and highlight.
- Keep lip boundary natural.

Safe range:

- Crack contrast reduction: `40% ~ 80%`.
- Peeling flake cleanup: allowed locally.
- Dry patch color restoration: `30% ~ 70%`.
- Natural lip line reduction: `0% ~ 25%`.
- No global lip blur.

Lip cracks:

- `LipCrackMask`
- `CrackDirection`
- `CrackWidth`
- `CrackLength`
- `CrackContrast`
- `CrackEdgeSharpness`
- `CrackRepairSafetyScore`

Deep cracks inside the lip body can be locally restored. Mouth-corner cracks must be separated from expression crease and saliva shadow.

Lip peeling:

- `LipPeelingMask`
- `PeelingEdgeScore`
- `PeelingAreaRatio`
- `PeelingTextureMismatchScore`
- `PeelingHighlightScore`

Peeling is usually a cleanup target, but texture must be restored after cleanup.

Lip color unevenness:

- `LipColorMean`
- `UpperLipColorMean`
- `LowerLipColorMean`
- `LipColorPatchinessScore`
- `LipDryDesaturationScore`
- `LipRednessBalanceScore`
- `LipDarkPatchScore`

Lips are naturally uneven in color. Upper and lower lip color differences are normal. Correct only distracting patchiness and dry pale areas, while preserving highlight and shadow.

Lip highlight and moisture:

- `LipHighlightMask`
- `LipHighlightStrength`
- `LipMoistureVisualScore`
- `OverGlossyLipScore`
- `FlatDryLipScore`

Natural lips need subtle highlight. Do not create glossy artificial lips by default.

Vermilion edge:

- `VermilionBorderSharpness`
- `LipBorderDamageScore`
- `LipSkinBleedScore`
- `LipstickBleedScore`

The lip border should be soft but clear. Avoid hard outlines and protect surrounding skin texture.

Mouth corner cracks:

- `MouthCornerCrackMask`
- `MouthCornerDrynessScore`
- `SalivaShadowScore`
- `MouthCornerWrinkleConfusionScore`

Correct dark cracks locally, but do not move mouth corners or remove natural expression creases.

Lip makeup interaction:

- `LipstickMask`
- `LipTintMask`
- `LipGlossMask`
- `MakeupCrackScore`
- `LipstickTextureMask`
- `LipMakeupBleedScore`

Lipstick cracking and actual chapped lips are different. Preserve intentional makeup unless cleanup mode or user request says otherwise.

Confidence:

- `LipDrynessMetricConfidence = lipMaskConfidence * lipTextureConfidence * lightingConfidence * resolutionConfidence * mouthExpressionNeutralConfidence * makeupSeparationConfidence * occlusionInverseConfidence`

Decision:

- High confidence: crack, peeling, and dry patch cleanup allowed.
- Medium confidence: color and roughness softening only.
- Low confidence: do not reshape or strongly inpaint; preserve natural lip texture.

Retouch priority:

1. Detect `outerLipMask`, `upperLipMask`, and `lowerLipMask`.
2. Protect teeth, inner mouth, skin around mouth, mustache, and beard.
3. Classify surface details: natural line, deep crease, dry crack, peeling flake, dry patch, makeup crack, shadow line, or artifact.
4. Calculate dryness, crack depth, peeling area, color patchiness, texture-loss risk, and boundary damage.
5. Remove/soften flakes, reduce crack contrast, restore dry patch color, soften deep creases, restore local lip texture.
6. Preserve natural fine lines, highlight, volume, and expression.
7. Check for no plastic lips, no flat single color, no hard outline, no lost natural vertical lines, and no bleeding into teeth/skin.

Safe limits:

- Natural lip line reduction: `0% ~ 25%`.
- Deep lip crease softening: `10% ~ 35%`.
- Dry crack contrast reduction: `40% ~ 80%`.
- Peeling flake cleanup: local only.
- Dry patch color restoration: `30% ~ 70%`.
- Lip texture smoothing: low to medium, preserving fine lines.
- Lip color unification: low.
- Lip highlight change: subtle.
- Lip border sharpening: very low.
- Lip geometry change: off by default.

Final lip dryness rule:

- Do not define perfect smooth lips.
- Define natural lip texture, natural lip wrinkle, dryness crack, peeling skin, dry color patch, makeup crack, shadow, or artifact.
- Preferred usage: chapped-lip cleanup, cracked-lip repair, peeling cleanup, dry patch color restoration, lip texture preservation, natural lip wrinkle softening, lip-boundary protection, and plastic-lip prevention.
- Avoid removing all lip lines, global lip blur, changing lip shape, default gloss creation, hard outlines, flat single-color lips, smearing into teeth/skin, and treating lipstick texture as skin damage.

## Face Vertical Distance, Spacing, And Angle Metrics

Face vertical distance metrics define key spacing and angles between chin, lips, eyes, eyebrows, nose, and mouth. They are for analysis, mask placement, confidence, protection, debug values, and manual-tool safety limits. They are not automatic beautification.

Core rules:

- Correct head roll using the eye line before measuring vertical distances.
- Reduce confidence if yaw, pitch, expression, occlusion, blur, or low resolution is high.
- Do not move chin, lips, eyes, eyebrows, nose, or mouth automatically based on these values.
- Use these values only for measurement, protection, confidence checks, and manual-tool guidance.

Primary anchors:

- `chinPoint`
- `lowerLipBottomCenter`
- `lowerLipCenter`
- `mouthCenter`
- `upperLipTopCenter`
- `noseBaseCenter` / `subnasale`
- `leftEyeCenter`, `rightEyeCenter`, `eyeCenter`
- `leftEyeLowerMid`, `rightEyeLowerMid`
- `leftEyeUpperMid`, `rightEyeUpperMid`
- `leftBrowCenter`, `rightBrowCenter`, `browCenter`
- `leftBrowLowerMid`, `rightBrowLowerMid`
- `leftBrowArch`, `rightBrowArch`
- `faceTop`, `faceBottom`, `faceH`, `faceW`, `faceCenterX`

All ratios use normalized coordinates after roll correction:

- `nx = (point.x - faceLeft) / faceW`
- `ny = (point.y - faceTop) / faceH`

### Chin, Lip, And Lower-Face Distances

Chin-to-lip metrics:

- `ChinToLowerLipDistance = distance(chinPoint, lowerLipBottomCenter)`
- `ChinToMouthCenterDistance = distance(chinPoint, mouthCenter)`
- `ChinToUpperLipDistance = distance(chinPoint, upperLipTopCenter)`
- `LowerLipToChinRatio = ChinToLowerLipDistance / faceH`: common `0.15 ~ 0.24`, soft `0.13 ~ 0.28`
- `MouthCenterToChinRatio = ChinToMouthCenterDistance / faceH`: common `0.20 ~ 0.30`, soft `0.18 ~ 0.34`
- `UpperLipToChinRatio = ChinToUpperLipDistance / faceH`: common `0.24 ~ 0.36`, soft `0.22 ~ 0.40`
- `LowerFaceBalanceRatio = ChinToMouthCenterDistance / distance(noseBaseCenter, chinPoint)`: common `0.45 ~ 0.62`

Interpretation:

- Larger values can indicate longer chin, lower mouth position, thin lips, or camera angle.
- Smaller values can indicate short chin, thick lower lip, lifted chin pose, or smile.
- Use for chin/mouth region mask placement and confidence, not automatic correction.

Lower-face section metrics:

- `LowerFaceHeight = distance(noseBaseCenter, chinPoint)`
- `LowerFaceHeightRatio = LowerFaceHeight / faceH`: common `0.30 ~ 0.38`, soft `0.28 ~ 0.42`
- `PhiltrumSection = distance(noseBaseCenter, upperLipTopCenter)`
- `LipMouthSection = distance(upperLipTopCenter, mouthCenter)`
- `ChinSection = distance(mouthCenter, chinPoint)`
- `PhiltrumSectionRatio = PhiltrumSection / LowerFaceHeight`: common `0.18 ~ 0.30`
- `LipMouthSectionRatio = LipMouthSection / LowerFaceHeight`: common `0.15 ~ 0.28`
- `ChinSectionRatio = ChinSection / LowerFaceHeight`: common `0.45 ~ 0.62`

Lower-face proportions vary strongly by age, gender, expression, lens, and pose. Use only for analysis and local retouch limits.

### Eye, Lip, And Brow Distances

Eye-to-lip metrics:

- `EyeCenterToMouthCenterDistance = distance(eyeCenter, mouthCenter)`
- `EyeCenterToUpperLipDistance = distance(eyeCenter, upperLipTopCenter)`
- `EyeCenterToLowerLipDistance = distance(eyeCenter, lowerLipBottomCenter)`
- `EyeToMouthRatio = EyeCenterToMouthCenterDistance / faceH`: common `0.24 ~ 0.36`, soft `0.22 ~ 0.40`
- `EyeToUpperLipRatio = EyeCenterToUpperLipDistance / faceH`: common `0.20 ~ 0.32`, soft `0.18 ~ 0.36`
- `EyeToLowerLipRatio = EyeCenterToLowerLipDistance / faceH`: common `0.28 ~ 0.40`, soft `0.25 ~ 0.44`
- `EyeToLipMidlineAngle`: angle between eye center line and mouth center line.

Larger distance can indicate long midface, lower mouth, camera pitch, or expression. Smaller distance can indicate short midface, raised mouth/smile, or camera angle. Use for midface mapping, not reshaping.

Eye-to-eyebrow metrics:

- `LeftEyeBrowDistance = distance(leftEyeUpperMid, leftBrowLowerMid)`
- `RightEyeBrowDistance = distance(rightEyeUpperMid, rightBrowLowerMid)`
- `AvgEyeBrowDistance = average(LeftEyeBrowDistance, RightEyeBrowDistance)`
- `EyeBrowDistanceFaceRatio = AvgEyeBrowDistance / faceH`: common `0.025 ~ 0.065`, soft `0.020 ~ 0.080`
- `EyeBrowDistanceEyeHeightRatio = AvgEyeBrowDistance / avgEyeHeight`: common `0.80 ~ 1.80`, soft `0.60 ~ 2.20`
- `EyeBrowDistanceBalanceScore = abs(LeftEyeBrowDistance - RightEyeBrowDistance) / faceH`: normal `0.00 ~ 0.015`, caution `0.015 ~ 0.030`, high `0.030+`

Brow-eye distance changes with expression. Raised eyebrows increase distance; frowning lowers it. Makeup, brow thickness, hair, and blur can affect measurement. Do not lift brows automatically.

### Angle Metrics

Eye line:

- `EyeLineAngle = angle(leftEyeCenter -> rightEyeCenter)`
- After roll correction, it should be near `0 deg`.
- Before correction: `0 ~ 3 deg` small tilt, `3 ~ 8 deg` noticeable tilt, `8+ deg` strong roll and lower confidence.
- Use as the primary roll reference, but do not rotate the final image unless the user requests.

Mouth line:

- `MouthLineAngle = angle(mouthLeftCorner -> mouthRightCorner)`
- After roll correction, neutral frontal face is near `0 deg`.
- `0 ~ 3 deg` small, `3 ~ 7 deg` noticeable, `7+ deg` expression/pose/asymmetry likely.
- `MouthEyeAngleDifference = abs(MouthLineAngle - EyeLineAngle)`
- Do not auto-level the mouth.

Eyebrows:

- `BrowSlopeAngleLeft = angle(leftBrowHead -> leftBrowTail)`
- `BrowSlopeAngleRight = angle(rightBrowHead -> rightBrowTail)`
- Soft common range: `-15 deg ~ +20 deg`, but style, expression, and makeup vary strongly.
- `BrowArchAngleLeft = angle(leftBrowHead -> leftBrowArch -> leftBrowTail)`
- `BrowArchAngleRight = angle(rightBrowHead -> rightBrowArch -> rightBrowTail)`
- `BrowEyeAngleDifference = abs(BrowSlopeAngle - EyeLineAngle)`: normal soft `0 ~ 15 deg`, caution `15+ deg`
- Do not make brows symmetrical or lift brow tail automatically.

Chin and lower face:

- `ChinCenterLineAngle = angle(noseBaseCenter -> chinPoint)` relative to vertical face axis.
- Guide: `0 ~ 3 deg` normal small offset, `3 ~ 6 deg` mild asymmetry/pose, `6+ deg` reduce confidence.
- `ChinOffsetRatio = abs(chinPoint.x - faceCenterX) / faceW`: normal `0.00 ~ 0.035`, caution `0.035 ~ 0.060`, high `0.060+`
- `LowerFaceCenterLine = line from noseBaseCenter to chinPoint`
- `MouthCenterOffsetFromLowerFaceLine = perpendicular distance from mouthCenter to LowerFaceCenterLine / faceW`: normal `0.00 ~ 0.025`, caution `0.025 ~ 0.050`
- `PhiltrumMouthChinAngle = angle(noseBaseCenter -> mouthCenter, mouthCenter -> chinPoint)`
- Use for lower-face mask confidence and correction safety, not automatic correction.

Nose-to-lip and midface:

- `NoseBaseToUpperLipAngle = angle(noseBaseCenter -> upperLipTopCenter)` relative to vertical face axis.
- Guide: `0 ~ 3 deg` normal, `3 ~ 6 deg` mild offset, `6+ deg` pose/expression/mustache/lip mask issue.
- `EyeNoseMouthCenterAngle = angle from eyeCenter to noseBaseCenter to mouthCenter`
- `CenterAxisAngle = angle(eyeCenter -> mouthCenter)` relative to vertical face axis.
- Guide: `0 ~ 3 deg` stable, `3 ~ 6 deg` mild pose/asymmetry, `6+ deg` reduce confidence.

### Distance And Angle Confidence

Suggested confidence:

- `AngleConfidence = landmarkConfidence * frontalPoseConfidence * expressionInverseConfidence * occlusionInverseConfidence * resolutionConfidence * rollCorrectionConfidence`
- `DistanceConfidence = anchorConfidence * poseConfidence * expressionConfidence * maskBoundaryConfidence * resolutionConfidence`

Reduce confidence if:

- Face yaw or pitch is high.
- Head roll is not corrected.
- Mouth is open, smiling, speaking, or pressed.
- Eyebrows are raised or frowning.
- Hair covers eyebrow.
- Beard or mustache covers lips.
- Chin is hidden by beard or long beard.
- Lip boundary is unclear.
- Eye is partly closed.
- Camera pitch, wide-angle distortion, low resolution, blur, or old photo damage exists.

### Vertical Distance Output

Output should include:

- `VerticalDistances`: `ChinToLowerLipDistance`, `ChinToMouthCenterDistance`, `ChinToUpperLipDistance`, `EyeCenterToMouthCenterDistance`, `EyeCenterToUpperLipDistance`, `EyeCenterToLowerLipDistance`, `AvgEyeBrowDistance`, `LeftEyeBrowDistance`, `RightEyeBrowDistance`
- `Ratios`: `LowerLipToChinRatio`, `MouthCenterToChinRatio`, `UpperLipToChinRatio`, `EyeToMouthRatio`, `EyeToUpperLipRatio`, `EyeToLowerLipRatio`, `EyeBrowDistanceFaceRatio`, `EyeBrowDistanceEyeHeightRatio`, `LowerFaceBalanceRatio`
- `Angles`: `EyeLineAngle`, `MouthLineAngle`, `BrowSlopeAngleLeft`, `BrowSlopeAngleRight`, `BrowArchAngleLeft`, `BrowArchAngleRight`, `ChinCenterLineAngle`, `NoseBaseToUpperLipAngle`, `CenterAxisAngle`, `PhiltrumMouthChinAngle`
- `Confidence`: `DistanceConfidence`, `AngleConfidence`, `EyeBrowDistanceConfidence`, `ChinLipDistanceConfidence`, `EyeLipDistanceConfidence`
- `Warnings`: `poseDistortion`, `expressionDistortion`, `lipBoundaryUnclear`, `chinHiddenByBeard`, `browHiddenByHair`, `eyePartlyClosed`, `mustacheOverlap`, `lowResolution`

Final vertical distance rule:

- Measure, classify, and protect.
- Use distances and angles for mask placement, local region estimation, confidence checks, protection masks, manual slider safety limits, and before/after debug values.
- Do not use them for automatic chin movement, lip movement, brow lift, eye/lip distance correction, or face proportion beautification.

## Philtrum And Chin

Philtrum:

- `philtrumH / noseBaseToChin`: `0.22 ~ 0.32`
- `philtrumH / lipH`: `1.0 ~ 1.8`

Lower face guide:

- `philtrumH : lipH : chinPartH`: approximately `1 : 1 : 2.2`

Reducing the philtrum too much can look younger, but it quickly shows as retouching.

## Face Component Position, Distance, And Ratio Guide

Face component position guides define approximate normalized positions and spacing ranges for major face components. They are used for landmark validation, mask placement, confidence scoring, local region boundaries, and retouch safety. They are not ideal proportions and must not trigger automatic reshaping.

Coordinate system:

- `faceLeft = 0.0`, `faceRight = 1.0`, `faceTop = 0.0`, `faceBottom = 1.0`
- `nx = (point.x - faceLeft) / faceW`
- `ny = (point.y - faceTop) / faceH`

Component mask placement rule:

- Component masks must snap to the best available component landmark or snapped anchor.
- Do not place eye, brow, nose, mouth, lip, nostril, philtrum, chin, jaw, cheekbone, ear, hairline, or neck masks from face-box percentages alone when a reliable landmark or mask-derived anchor exists.
- Ratio-table positions are fallback sanity checks and confidence guides, not primary mask anchors.
- If a component anchor is missing or unreliable, use the guide table only to define a search ROI, then build the actual mask from segmentation, edge, color, texture, and protection masks.
- If component mask overlap with its landmark is low, lower confidence or skip visible correction.

### Component Position Table

Global center line:

- `faceCenterX`: `nx = 0.50`
- Vertical center line should pass approximately through forehead center, nose root, nose tip, nose base, philtrum, mouth center, and chin point.
- Soft alignment: `noseCenterX 0.48 ~ 0.52`, `mouthCenterX 0.47 ~ 0.53`, `chinPointX 0.47 ~ 0.53`.
- Do not force symmetry; use only for confidence and mask stability.

Hairline and forehead:

- `hairlineCenter`: `nx 0.45 ~ 0.55`, `ny 0.10 ~ 0.22`; bangs, baldness, crop, and lighting lower confidence.
- `foreheadCenter`: `nx 0.45 ~ 0.55`, `ny 0.22 ~ 0.34`; shine, wrinkles, bangs, and hair shadow affect detection.
- Do not move hairline automatically.

Eyebrows:

- Anchors: `leftBrowCenter`, `rightBrowCenter`, `browCenter`
- Position: `browCenter.ny 0.32 ~ 0.40`, `leftBrowCenter.nx 0.28 ~ 0.40`, `rightBrowCenter.nx 0.60 ~ 0.72`
- Distance: brow-to-eye `0.025 ~ 0.065` of `faceH`, or `0.80 ~ 1.80` of eye height.
- Expression and makeup affect brow position. Do not auto-lift or level brows.

Eyes:

- Anchors: `leftEyeCenter`, `rightEyeCenter`, `eyeCenter`
- Position: `eyeCenter.ny 0.42 ~ 0.48`, `leftEyeCenter.nx 0.30 ~ 0.38`, `rightEyeCenter.nx 0.62 ~ 0.70`
- Distance: `eyeDist / faceW 0.38 ~ 0.46`, `eyeGap / avgEyeW 0.90 ~ 1.20`, `avgEyeW / faceW 0.18 ~ 0.23`
- Eyes are the primary roll reference and must be protected from skin retouch.

Ear:

- Anchors: `leftEarCenterEstimated`, `rightEarCenterEstimated`
- Position: left `nx -0.05 ~ 0.12`, right `nx 0.88 ~ 1.05`, `earCenter.ny 0.42 ~ 0.58`
- Common face landmark models do not provide detailed ear landmarks. Estimate from ROI and segmentation, and lower confidence when hidden by hair.

Nose:

- Nose root: `nx 0.48 ~ 0.52`, `ny 0.38 ~ 0.48`
- Nose bridge: `nx 0.48 ~ 0.52`, `ny 0.44 ~ 0.58`, `bridgeW / noseW 0.28 ~ 0.45`
- Nose tip: `nx 0.48 ~ 0.52`, `ny 0.58 ~ 0.66`, center offset `0.00 ~ 0.030` of `faceW`
- Nose base: `nx 0.48 ~ 0.52`, `ny 0.63 ~ 0.70`
- Nose wings: `leftWing.nx 0.38 ~ 0.46`, `rightWing.nx 0.54 ~ 0.62`, `ny 0.62 ~ 0.70`
- Distance: `noseLength / faceH 0.24 ~ 0.32`, `noseW / faceW 0.18 ~ 0.25`, `noseW / mouthW 0.45 ~ 0.70`, `noseW / eyeGap 0.90 ~ 1.25`
- Nostril shadows are protected dark regions and must not be brightened as blemish.

Philtrum:

- Anchors: `noseBaseCenter`, `upperLipTopCenter`, `philtrumCenter`
- Position: `philtrumCenter.nx 0.48 ~ 0.52`, `philtrumCenter.ny 0.66 ~ 0.72`
- Distance: `philtrumHeight / faceH 0.045 ~ 0.085`, `philtrumHeight / lowerFaceHeight 0.18 ~ 0.30`, `philtrumRidgeWidth / faceW 0.045 ~ 0.090`
- Mustache, shaving shadow, and upper-lip boundary uncertainty lower confidence. Do not change nose-lip distance.

Mouth and lips:

- `mouthCenter`: `nx 0.47 ~ 0.53`, `ny 0.72 ~ 0.80`
- `mouthW / faceW 0.32 ~ 0.42`, `mouthW / eyeDist 0.75 ~ 1.05`, `mouthW / noseW 1.45 ~ 2.20`
- `upperLipTopCenter.ny 0.69 ~ 0.75`, `lowerLipBottomCenter.ny 0.76 ~ 0.84`
- `lipH / mouthW 0.22 ~ 0.38`, `lipH / faceH 0.045 ~ 0.075`, `upperLipH / lowerLipH 0.55 ~ 0.85`
- Expression strongly affects mouth position. Lip surface problems require `lipSurfaceMask`; landmarks alone cannot measure cracks or dryness.

Chin and jaw:

- `chinPoint`: `nx 0.47 ~ 0.53`, `ny 0.96 ~ 1.00`
- `chinW / faceW 0.18 ~ 0.32`, `mouthCenter to chin / faceH 0.20 ~ 0.30`, `lowerLipBottom to chin / faceH 0.15 ~ 0.24`
- `leftJawAngle.nx 0.15 ~ 0.30`, `rightJawAngle.nx 0.70 ~ 0.85`, `jawAngle.ny 0.70 ~ 0.82`
- `jawAngleW / faceW 0.68 ~ 0.86`, `jawAngleW / cheekW 0.72 ~ 0.92`
- Beard, hair, and shadow can fake jaw width. Do not auto-sharpen chin or auto-slim jaw.

Cheekbone:

- `leftZygomaPeak.nx 0.10 ~ 0.25`, `rightZygomaPeak.nx 0.75 ~ 0.90`, `ny 0.42 ~ 0.58`
- `zygomaPeakW / faceW 0.78 ~ 0.96`, `cheekW / faceW 0.88 ~ 1.00`, `cheekW / jawAngleW 1.10 ~ 1.35`
- Lighting and smile affect cheekbone reading. Do not auto-reduce cheekbones.

Neck:

- Anchors: `neckLeft`, `neckRight`, `neckCenter`
- Starts below `chinPoint`, `neckCenter.nx 0.45 ~ 0.55`, `ny 1.00+`
- Collar, hair, beard, and shadow affect the neck mask. Match tone softly with face.

### Component To Face Outline Distance

- Eye to side face: `0.18 ~ 0.28` of `faceW`; use for eye confidence and yaw detection.
- Brow outer to side face: `0.08 ~ 0.18` of `faceW`; use for brow mask confidence and hair occlusion checks.
- Nose wing to cheek line: `0.25 ~ 0.38` of `faceW`; use for nose width sanity and cheek mapping.
- Mouth corner to side face: `0.20 ~ 0.32` of `faceW`; use for mouth width confidence and expression check.
- Chin width to jaw angle width: `chinW / jawAngleW 0.25 ~ 0.45`; use for chin/jaw shape analysis, not automatic V-line.
- Cheekbone to jawline: `cheekboneW / jawAngleW 1.05 ~ 1.32`; use for cheek/jaw relation and face-shape confidence.
- Forehead to cheek: `foreheadW / cheekboneW 0.75 ~ 1.05`; use for face-shape analysis and hairline/temple confidence.
- Jaw to cheek: `jawAngleW / cheekboneW 0.72 ~ 1.00`; use for shape score, not automatic correction.

### Component Spacing And Angle Summary

Vertical spacing:

- Hairline to brow: `0.12 ~ 0.22` of `faceH`
- Brow to eye: `0.025 ~ 0.065` of `faceH`
- Eye to nose tip: `0.12 ~ 0.22` of `faceH`
- Eye to nose base: `0.17 ~ 0.27` of `faceH`
- Eye to mouth center: `0.24 ~ 0.36` of `faceH`
- Nose base to upper lip: `0.045 ~ 0.085` of `faceH`
- Mouth center to chin: `0.20 ~ 0.30` of `faceH`
- Lower lip to chin: `0.15 ~ 0.24` of `faceH`
- Nose base to chin: `0.30 ~ 0.38` of `faceH`

Width ratios:

- `eyeDist / faceW 0.38 ~ 0.46`
- `avgEyeW / faceW 0.18 ~ 0.23`
- `eyeGap / avgEyeW 0.90 ~ 1.20`
- `noseW / faceW 0.18 ~ 0.25`
- `noseW / eyeGap 0.90 ~ 1.25`
- `mouthW / faceW 0.32 ~ 0.42`
- `mouthW / eyeDist 0.75 ~ 1.05`
- `mouthW / noseW 1.45 ~ 2.20`
- `lipH / mouthW 0.22 ~ 0.38`
- `cheekboneW / faceW 0.78 ~ 0.96`
- `jawAngleW / faceW 0.68 ~ 0.86`
- `chinW / faceW 0.18 ~ 0.32`
- `chinW / jawAngleW 0.25 ~ 0.45`
- `foreheadW / cheekboneW 0.75 ~ 1.05`
- `cheekboneW / jawAngleW 1.05 ~ 1.32`

Angle guide:

- `EyeLineAngle`: `0 ~ 3 deg` small, `3 ~ 8 deg` noticeable, `8+ deg` strong roll.
- `MouthLineAngle`: `0 ~ 3 deg` small, `3 ~ 7 deg` noticeable, `7+ deg` expression/pose/asymmetry likely.
- `BrowSlopeAngle`: soft `-15 deg ~ +20 deg`, highly expression and makeup sensitive.
- `NoseCenterLineAngle`: near vertical after roll correction.
- `ChinCenterLineAngle`: `0 ~ 3 deg` stable, `3 ~ 6 deg` mild pose/asymmetry, `6+ deg` reduce confidence.
- `PhiltrumCenterLineAngle`: near vertical, but mustache and lip-boundary uncertainty can distort it.
- `JawLineAngle`: use for jaw taper and angularity, not automatic slimming.

Confidence rule:

- Reduce landmark ratio confidence for high yaw/pitch, uncorrected roll, strong smile/speech expression, partly closed eyes, hair-hidden brows/ears/contours, lips hidden by mustache, jaw/chin hidden by beard or long beard, strong side shadow, glasses shadow, low resolution, or old photo damage.

Final component position rule:

- Use percentage and ratio values for landmark sanity checks, mask placement, region boundary estimation, confidence scoring, module protection, and manual slider safe limits.
- Do not use them for automatic face reshaping, symmetry correction, V-line, nose/lip/eye repositioning, or ideal proportion forcing.
- Measure and protect; do not reshape automatically.

## Vertical Flow

Normalized front-face flow:

- Brow center: `0.34 ~ 0.40`
- Eye center: `0.42 ~ 0.48`
- Nose tip: `0.58 ~ 0.66`
- Nose base: `0.63 ~ 0.70`
- Mouth center: `0.72 ~ 0.80`
- Chin: `1.00`

These values should be checked only after roll correction and with pose confidence.

## First-Pass Scores

Use simple ratios first:

- `EyeBalanceScore = abs(leftEyeW - rightEyeW) / avgEyeW`
- `EyeHeightBalanceScore = abs(leftEyeY - rightEyeY) / faceH`
- `NoseCenterScore = abs(noseCenterX - faceCenterX) / faceW`
- `MouthCenterScore = abs(mouthCenterX - faceCenterX) / faceW`
- `MouthWidthRatio = mouthW / eyeDist`
- `LipHeightRatio = lipH / mouthW`
- `BrowEyeRatio = browEyeDist / eyeH`

Guide interpretation:

- `0.00 ~ 0.03`: almost natural.
- `0.03 ~ 0.06`: weak asymmetry.
- `0.06 ~ 0.10`: correction candidate.
- `0.10+`: strong asymmetry or expression/pose issue.

## Current Code Coverage

Currently measured in `Core/AnchorMesh/Measure/KAnchorMeasureEngine.cs`:

- `FaceHeightToWidthRatio`
- `EyeCenterYToFaceHeightRatio`
- `EyeDistanceToFaceWidthRatio`
- `EyeHeightBalanceScore`
- `EyeLevelScore`
- `EyeCenterOffsetToFaceWidth`
- `EyeLineAngleDeg`
- `EyeMetricGuideConfidence`
- `NoseTipYToFaceHeightRatio`
- `NoseBaseYToFaceHeightRatio` as a temporary estimate from nose-tip to mouth-center distance.
- `NoseCenterOffsetToFaceWidth`
- `NoseEyeCenterOffsetToFaceWidth`
- `EstimatedNoseLengthToFaceHeightRatio`
- `EstimatedNoseLengthToEyeDistanceRatio`
- `MouthCenterYToFaceHeightRatio`
- `MouthCenterOffsetToFaceWidth`
- `MouthNoseCenterOffsetToFaceWidth`
- `MouthEyeCenterOffsetToFaceWidth`
- `MouthToChinDistance` and `MouthToChinToLowerFaceRatio` as coarse lower-face vertical guides from current sparse anchors and face-mask bottom.
- `MouthWidthToEyeDistanceRatio`
- `MouthWidthToFaceWidthRatio`
- `MouthCornerLevelScore`
- `MouthCornerSlopeDeg`
- `MouthWidthBalanceScore`
- `MouthMetricGuideConfidence`
- Component mask placement currently uses snapped AnchorMesh feature points for eyes, brows, lips, nose, nostrils, and face outline where available; face-box percentages are fallback guides, not primary mask anchors.
- `PhiltrumToLowerFaceRatio` as a coarse proxy from the current sparse landmarks, not a spec-grade philtrum height measurement.
- `LowerFacePhiltrumLipChinGuideRatio` as a coarse lower-face guide.
- `CheekFaceWidthRatio`
- `JawFaceWidthRatio`
- `ChinFaceWidthRatio` from the current `widthAt90` mask slice.
- `JawWidthToCheekWidthRatio`
- `JawMidToCheekWidthRatio` from the current `widthAt80 / widthAt50` mask slices.
- `ChinWidthToCheekWidthRatio` from the current `widthAt90 / widthAt50` mask slices.
- `JawToChinTaperRatio` from the current `widthAt90 / HorizontalJawWidth` estimate.
- `LowerFaceToFaceHeightRatio`
- `MouthToChinToLowerFaceRatio`
- `WidthAt20`, `WidthAt35`, `WidthAt50`, `WidthAt65`, `WidthAt80`, `WidthAt90`
- `ForeheadWidthRatio`, `EyeLevelWidthRatio`, `CheekLevelWidthRatio`, `MouthLevelWidthRatio`, `JawLevelWidthRatio`, `ChinLevelWidthRatio`
- `ContourBalanceScore`
- `LowerContourBalanceScore`
- `ContourMetricGuideConfidence`

Not yet directly measured because dense landmarks are not connected:

- Real eye width and height.
- Real eye gap from inner eye corners.
- Real eye aspect ratio.
- Real iris diameter and iris-eye ratio.
- Pupil position.
- Upper/lower eyelash masks, lash root lines, lash tips, lash density, lash direction, mascara/eyeliner separation, lash shadow, smudge, blur, and over-sharpening metrics.
- Dark-circle and under-eye component masks for pigmentation, vascular blue/purple tone, tear-trough shadow, eye-bag shadow, lower-lash shadow, makeup/glasses shadow, under-eye texture, and correction-safety metrics.
- Brow width and brow-eye distance.
- Brow slope and brow-eye balance.
- Full face component position guide validation for hairline, forehead, brows, eyes, ears, nose root/bridge/base/wings, philtrum, lips, chin, jaw, cheekbone, neck, component-to-face-line distances, and landmark-to-mask snap confidence maps.
- Spec-grade vertical spacing and angle metrics for chin-to-lip, eye-to-lip, eye-to-brow, brow slope/arch, mouth line, chin center line, nose-to-lip angle, lower-face center line, and midface center-axis confidence.
- Real nose width.
- Real nose wing balance.
- Real bridge width and bridge curve.
- Real nostril distance and nostril exposure.
- Real nose-tip width and height.
- Upper/lower lip height.
- Real lip height, inner-mouth openness, and upper/lower lip ratios.
- Spec-grade philtrum anchors and ratios from `noseBaseCenter`, `upperLipTopCenter`, philtrum ridge/groove estimates, cupid bow points, nostril protection, mustache overlap, and lip-boundary confidence.
- Cupid bow width, depth, and contour balance.
- Teeth visibility metrics.
- Lip color/tone metrics.
- Lip surface masks and lip dryness, crack, peeling, dry patch, vermilion-border damage, plastic-lip, lip texture-loss, and lip makeup-crack metrics.
- Inner-mouth and lip contour ratios.
- Real temple, zygoma, jaw angle, jaw mid, chin side, and neck widths from dense contour.
- Face shape scores for oval, round, square, rectangle, heart, diamond, triangle, inverted triangle, long, and short face candidates, plus face shape confidence, occlusion flags, and manual contour tool safe-limit maps.
- Real jaw angle balance.
- Jaw curve smoothness and contour noise.
- Dense-contour multi-slice balance beyond the current mask-slice estimate.
- Real chin side width, chin height, chin center, chin-nose center, chin-mouth center, and chin projection metrics.
- Square-jaw taper profile, mandibular angle sharpness, jaw-body straightness, lower-face blockiness, masseter area, bone/masseter/fat/shadow/pose/beard/hair cause classification, under-jaw shadow, jaw-neck separation, under-jaw shadow hardness/patchiness/continuity, pitch/lighting-driven shadow, and beard/hair/clothing confusion metrics.
- Cheek/zygoma peak, cheek outer, cheek hollow, cheek highlight, cheek-to-jaw transition, cheek/mouth/nose relation, smile-cheek lift, makeup contour, hair/beard cheek overlap, and visual volume metrics.
- Hair segmentation, hair core/soft-edge/strand masks.
- Hairline, bangs, temple hair, side hair, sideburn, ear-overlap, forehead-overlap, eyebrow-overlap, eye-overlap, jaw-overlap, and neck-overlap metrics.
- Hair boundary noise, edge transparency, halo, background bleed, skin bleed, and matting confidence metrics.
- Flyaway hair count, length, direction, distance, thinness, isolation, connection, protected-overlap, keep/remove candidate scores, removed/softened/skipped maps, restoration confidence, and over-removal checks.
- Hair direction field, texture strength, clump size, shine, gray hair, scalp visibility, parting, and volume metrics.
- Forehead skin masks, hairline confidence, bangs coverage, forehead wrinkle/glabella masks, shine masks, shadow masks, texture strength, blemish masks, and forehead tone/color metrics.
- Global wrinkle/fold masks, crow's feet, under-eye lines, nasolabial folds, mouth-corner/marionette lines, cheek wrinkles, chin creases, neck wrinkles, double-chin folds, wrinkle classification, age/expression/shadow/artifact confidence, and wrinkle correction-strength metrics.
- Skin mark classification masks for moles, acne, acne redness, acne scars, blemishes, freckles, pigmentation, melasma, redness, blackheads, whiteheads, pores, scars, photo damage, identity protection, and local texture-preserving reconstruction metrics.
- Manual skin smoothing target masks, protection masks, region strength maps, texture-preservation scores, plastic-skin checks, over-smoothing maps, and slider-triggered execution guards.
- Skin smoothing fine/medium/large detail layers, adaptive radius scales, fine/medium/large thresholds, uniformity score, texture-protection threshold, and scale-aware plastic-skin checks.
- Skin texture, pore, fine grain, micro-contrast, texture loss, texture mismatch, over-smoothed skin, plastic skin, texture restoration, shine-texture interaction, and face/neck texture consistency metrics.
- Skin color balance, body tone matching, clean reference skin selection, color cast maps, patchiness maps, makeup/shadow/highlight separation, face-neck/body deltas, hand/arm tone harmony, and tone overcorrection checks.
- Beard, mustache, sideburn, cheek beard, chin beard, jaw beard, neck beard, long beard masks, virtual beard landmarks, beard direction/texture/density, beard occlusion, long-beard overlap, beard-vs-skin/jaw/lip/neck classification, and beard-aware correction maps.
- Beard/stubble region masks, shaving shadow, stubble-dot pattern, razor redness, ingrown hair, razor cut, mustache shadow, jaw/neck stubble, beard-vs-blemish classification, and beard-aware correction maps.
- Ear visible masks, ear ROI, virtual top/bottom/outer/inner-attach points, optional estimated helix/antihelix/concha/tragus/earlobe subregions, and ear-face attachment metrics.
- Ear visibility, hair/glasses/earring overlap, sideburn overlap, ear-face color delta, redness, inner shadow, earlobe, artifact, and ear/jaw relation metrics.

Future MediaPipe/ONNX dense landmarks should fill these values without changing the guide ranges.

## Rule

Absolute values should not drive edits.

Use the subject's original face as the baseline, detect only values that are unusually out of range, and apply only weak, user-controlled correction.
