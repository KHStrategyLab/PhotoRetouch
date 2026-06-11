# ANCHOR_MESH_GUIDE_REFERENCE.md

This document defines lightweight anchor, guide, ratio, and detector rules for K-AnchorMesh.

Read this document when working on:

- pupil / iris detection
- eye guide
- eye leveling
- eyebrow guide
- eyebrow detector
- brow ROI
- facial anchor geometry
- debug anchor overlay

## Pupil Definition For Anchor-Based Eye Detection

Define the pupil as the darkest central area inside the iris.

In PhotoRetouch, the pupil is not an edit target.
It is a reference point and protection anchor.

Purpose:

- Use the pupil center as an eye-center reference point.
- Use left and right pupil centers to estimate eye-line angle and face tilt.
- Use pupil/iris position to guide eye protection, eyebrow ROI, eyelid ROI, and orbital-brow search.
- Never apply skin smoothing, tone correction, wrinkle reduction, or blemish correction over the pupil, iris, catchlight, or visible eye detail.

Detection rule:

- Do not search for the pupil globally.
- First detect the Eye ROI.
- Inside the Eye ROI, find the iris candidate.
- Inside the iris candidate, find the darkest circular or elliptical center area as the pupil candidate.
- If iris or pupil confidence is weak, widen eye protection instead of trying to correct the area.

Lightweight ratio guide:

- Pupil center is usually near 45% to 55% of the Eye ROI width.
- Pupil center is usually near 45% to 60% of the Eye ROI height.
- Iris diameter is usually about 25% to 35% of visible eye width.
- Pupil diameter changes with lighting and is usually about 25% to 50% of iris diameter.
- Left-right pupil distance can be used as a face-scale reference.
- Pupil position may shift strongly depending on gaze direction, so do not force it to the exact center of the eye.

Pixel-size example:

- If visible eye width is about `100 px`, a reasonable pupil candidate may appear as a dark circle about `8 px` in diameter inside the iris.
- A small white catchlight around `4 px x 4 px` may overlap that dark circle and partially break its shape.
- Do not reject the pupil only because a small catchlight interrupts part of the circle.

Protection policy:

- Protect the full iris area, not only the pupil.
- Include catchlight and nearby eyelid-edge detail in the eye protection mask.
- Pupil and iris masks must always be part of HardProtect.
- If uncertain, protect wider and retouch weaker.

## Pupil Lower Baseline Definition

Define `PupilLowerBaseline` as the extended line connecting the lower boundary points of the left and right pupil or iris candidates.

This line is used as a secondary horizontal reference for eye-level alignment.

Terminology:

- `LeftPupilCenter`: center point of the left pupil candidate.
- `RightPupilCenter`: center point of the right pupil candidate.
- `LeftPupilLowerPoint`: bottom point of the left pupil or iris ellipse.
- `RightPupilLowerPoint`: bottom point of the right pupil or iris ellipse.
- `PupilLowerBaseline`: the infinite extended line passing through `LeftPupilLowerPoint` and `RightPupilLowerPoint`.

Point definition:

- Do not choose the lower point from raw dark pixels only.
- First detect the Eye ROI.
- Inside the Eye ROI, fit the iris or pupil as an ellipse.
- The lower point is the bottom tangent point of that ellipse in the local eye direction.
- If the pupil is too small, hidden, or unstable, use the iris ellipse lower point instead.

Horizontal angle:

```text
dx = RightPupilLowerPoint.X - LeftPupilLowerPoint.X
dy = RightPupilLowerPoint.Y - LeftPupilLowerPoint.Y
angle = atan2(dy, dx)
```

Use `angle` as a secondary face/eye horizontal tilt estimate.

Correction rule:

- Rotation correction should be `-angle`.
- Do not rotate the image automatically unless the user activates a leveling tool.
- If center-line angle and lower-baseline angle disagree strongly, prefer the pupil-center line and mark the lower baseline as low confidence.

Safety rules:

- Do not use this line as an edit mask.
- Do not let eyelid shadows, eyelashes, catchlights, or dark under-eye pixels become pupil lower points.
- If one eye is partially closed or the iris is hidden, lower the confidence.
- If uncertain, use the pupil-center line as the primary horizontal reference.

## Eye Guide Follows Pupil/Iris Center

When pupil or iris center is updated, the full eye guide must follow.

Use this dependency order:

```text
PupilCenter / IrisCenter
-> EyeCenter
-> EyeAxis
-> InnerEyeCorner / OuterEyeCorner
-> UpperEyelidGuide / LowerEyelidGuide
-> EyeROI
-> BrowROI
```

Do not keep the old eye guide fixed after the pupil has moved.
If pupil/iris detection confidence is high, recenter the eye guide around the new pupil/iris position.

The eye should follow the pupil, not the other way around.

Patch rule:

- Patch only the eye-guide follow logic when this task is requested.
- When pupil/iris center is updated with good confidence, recenter `EyeCenter`, `EyeAxis`, `UpperEyelidGuide`, `LowerEyelidGuide`, and `EyeROI` around the updated pupil/iris position.
- Do not move the pupil to match the old eye guide.
- Do not modify eyebrow mask or retouch filters in this patch.

## Eyebrow Guide Definition

The eyebrow guide is not the final EyebrowMask.

Define `EyebrowGuide` as a structural guide that predicts where the eyebrow hair mass should exist above each eye.

The guide must be based on eye geometry, pupil/iris position, face tilt, and the superior orbital arc.

Do not create the eyebrow guide from fixed face-box coordinates.
Do not draw a simple horizontal line above the eye.

Guide inputs:

- Eye ROI
- EyeCenter
- UpperEyelid line
- InnerEyeCorner
- OuterEyeCorner
- PupilCenter or IrisCenter
- EyeWidth
- EyeHeight
- FaceTiltAngle from pupil center baseline
- Optional validation angle from pupil/iris lower baseline

Guide structure:

- `BrowCenterlineGuide`
- `BrowUpperGuide`
- `BrowLowerGuide`
- `BrowEnvelopeGuide`

The eyebrow guide should be a curved orbital arc or thin envelope, not a single straight segment.

Coordinate rule:

Build the eyebrow guide in a local eye coordinate system rotated by the face/eye tilt angle.

Primary tilt:

- left pupil center to right pupil center

Validation tilt:

- left iris/pupil lower point to right iris/pupil lower point

If the two angles strongly disagree, lower guide confidence.

Guide shape rule:

Use a free curved guide.

Preferred structure:

- 15 upper boundary points
- 15 lower boundary points

or:

- 10 lower guide points
- 10 centerline points
- 10 upper guide points

The guide should follow the superior orbital ridge above the eye.

Do not use this guide as the final eyebrow mask.
Use it only to define the search zone and expected brow direction for later pixel fitting.

Failure policy:

If eye landmarks are weak, glasses interfere, or the pupil/iris is uncertain:

- keep the guide conservative
- widen the brow search zone slightly
- lower confidence
- do not force a synthetic eyebrow position

## Wider Eyebrow Guide Ratio

Use a generous eyebrow guide envelope.
The guide should cover the expected brow zone, not only the visible brow hair line.

Reference inputs:

- EyeWidth
- EyeHeight
- EyeCenter
- UpperEyelid
- InnerEyeCorner
- OuterEyeCorner
- PupilCenter or IrisCenter
- FaceTiltAngle

Wider guide position:

- Brow center is usually above the upper eyelid by about 0.50 to 1.05 * EyeHeight.
- Brow lower guide is usually above the upper eyelid by about 0.25 to 0.60 * EyeHeight.
- Brow upper guide is usually above the upper eyelid by about 0.85 to 1.45 * EyeHeight.

Wider guide width:

- Brow guide width should be about 1.15 to 1.45 * EyeWidth.
- Brow head may start slightly inside the inner eye corner or extend outside it by about 0.05 to 0.20 * EyeWidth.
- Brow tail may extend beyond the outer eye corner by about 0.15 to 0.35 * EyeWidth.

Brow peak guide:

- Brow peak is usually around 55% to 78% of brow width from the brow head.
- Brow peak may rise about 0.10 to 0.35 * EyeHeight above the brow centerline.
- Do not force a sharp peak if the brow appears flat, soft, sparse, or hidden.

Envelope rule:

- The eyebrow guide should be a wide curved envelope, not a thin line.
- Use the guide to define a safe search zone for brow pixels.
- Do not use this guide directly as the final EyebrowMask.
- Later pixel fitting should decide the real brow thickness, endpoints, gaps, and density.

Safety rule:

- If glasses, bangs, weak eyebrows, shadows, or wrinkles interfere, keep the guide wider but lower confidence.
- A wider guide is acceptable for search and protection planning.
- Do not convert the full wide guide into a visible correction mask.

## Eyebrow Hair Detector Definition

Define the eyebrow as a dark hair-like cluster above each eye.

Do not define the eyebrow as simply the darkest area.
Do not let dark forehead skin, forehead shadow, wrinkles, eyelashes, bangs, or glasses frames become eyebrow candidates.

The detector must use:

- local darkness contrast
- hair-like texture
- oriented edge density
- brow guide alignment
- brow-like shape ratio
- eye-based distance ratio
- face/eye tilt angle

Darkness rule:

Do not search for the absolute darkest pixels globally.

First build a local Brow ROI above each eye.
Inside the Brow ROI, sample nearby skin tone and brightness.
A brow candidate must be darker than nearby skin, not just dark in absolute value.

Use local contrast:

```text
candidateDarkness = localSkinLuma - pixelLuma
```

A pixel or cluster is not enough just because it is dark.
It must also have hair-like texture and brow-like geometry.

Brow ROI inputs:

- EyeCenter
- PupilCenter or IrisCenter
- UpperEyelid
- InnerEyeCorner
- OuterEyeCorner
- EyeWidth
- EyeHeight
- FaceTiltAngle

Build the Brow ROI in a local eye coordinate system rotated by the eye or face tilt angle.

Generous Brow ROI ratio:

- Brow ROI width: 1.15 to 1.45 * EyeWidth
- Brow lower boundary: 0.25 to 0.70 * EyeHeight above UpperEyelid
- Brow center: 0.50 to 1.15 * EyeHeight above UpperEyelid
- Brow upper boundary: 0.85 to 1.60 * EyeHeight above UpperEyelid
- Brow head: near InnerEyeCorner, allowed to extend 0.05 to 0.20 * EyeWidth
- Brow tail: allowed to extend 0.15 to 0.35 * EyeWidth beyond OuterEyeCorner

Eyebrow hair feature:

A valid eyebrow candidate should have:

- darker pixels than nearby skin
- short hair-like strokes
- high local edge density
- direction roughly following the brow guide
- elongated curved cluster shape
- upper and lower boundary tendency
- variable thickness
- density variation or small gaps

Reject these as eyebrow candidates:

- broad smooth dark forehead skin
- soft shadow gradients
- forehead wrinkles
- eyelid shadows
- eyelashes below the brow zone
- glasses frames
- bangs or random hair strands outside the orbital-brow ROI

Shape and ratio guide:

- Visible brow hair length: about 0.70 to 1.30 * EyeWidth
- Brow guide envelope width: about 1.15 to 1.45 * EyeWidth
- Brow thickness: about 0.12 to 0.45 * EyeHeight
- Brow peak position: about 55% to 78% of brow width from brow head
- Brow peak rise: about 0.10 to 0.35 * EyeHeight above brow centerline

Angle guide:

- Use pupil-center baseline as the primary face/eye tilt angle.
- Use pupil or iris lower baseline as validation.
- Brow global angle should usually stay within about ±25 degrees of the eye baseline.
- Local brow segments may vary within about ±35 degrees.
- Do not force a sharp arch if the real brow is flat, sparse, hidden, or broken.

Candidate scoring:

EyebrowCandidateScore should combine:

- local darkness contrast
- hair texture score
- oriented edge score
- connected component shape score
- brow guide alignment score
- eye-distance ratio score
- bilateral consistency score

Apply penalties for:

- forehead shadow
- smooth dark skin
- wrinkles
- glasses frame
- eyelash leakage
- bangs or random hair outside Brow ROI

Output:

The detector should output:

- BrowHairCandidate
- BrowConfidence
- BrowHeadPoint
- BrowTailPoint
- BrowPeakPoint
- BrowCenterline
- BrowUpperBoundary
- BrowLowerBoundary
- BrowEnvelopeGuide

Do not use the detector result directly as a visible correction mask.
Use it to refine eyebrow guide, protection zone, and later pixel fitting.

Failure policy:

If confidence is weak:

- keep the BrowGuide wider
- lower EyebrowConfidence
- protect wider
- do not force a synthetic eyebrow position
- do not use forehead dark skin as eyebrow
