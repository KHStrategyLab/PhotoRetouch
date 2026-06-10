# K Retouch Pro / PhotoRetouch - ORDER_35

This file is UTF-8.

## Stage

V1 final review / engine stabilization review

## Status

InProgress / First final review recorded

## Review Date

2026-06-09

## Build Verification

Command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Result:

- Warnings: 0
- Errors: 0

## Packaging Verification

Command used for light package verification:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build\Publish-V1.ps1 -SelfContained false -OutputRoot '.\publish\verify'
```

Result:

- Publish folder created.
- ZIP package created.
- Generated output is ignored by git.

## Current V1 Boundary

V1 remains a single-face portrait retouch engine.

In scope:

- SnapshotMask
- HardProtect / SoftProtect / RetouchAllow
- Stage 1-10
- MaskQualityGate
- SkinSmooth
- BlemishReduce
- WrinkleSoftReduce
- ToneEven first pass
- TextureRestore
- HardProtectFinalRestore
- Export core
- Preset core
- Batch core
- Cache/memory cleanup first pass
- Session persistence first pass
- x64 publish script

Out of scope / Hold:

- multi-face processing
- ShapeBalance / left-right symmetry geometry module
- generative AI retouch
- background replacement
- clothing retouch
- GPU optimization
- advanced parallel processing

## Strong Points

- Stage and Slider changes are separated from SnapshotMask regeneration.
- HardProtect final restore exists as the last pipeline step.
- SnapshotMask disk cache exists and excludes Stage from cache keys.
- Manual face adjustment changes are treated as SnapshotMask regeneration conditions.
- Export and Preset structures do not store photo-specific SnapshotMask data.
- Batch processing creates one SnapshotMask per image and does not share masks across photos.
- High-resolution policy separates preview sizing from export intent.

## Remaining V1 Risks

- Real portrait visual QA is still required.
- FaceParsing is still temporary/scaffolded, not a production segmentation model.
- Dedicated `ToneEvenFilter` with full candidate masks is still weaker than the written target design.
- Manual mask brush has core logic but not a full mouse brush UI.
- Face keypoint manual adjustment has core override storage but not individual keypoint handles.
- Export / Preset / Batch have core services but not full user-facing UI panels.
- Full installer, code signing, and version stamping are not done.
- Long-session memory stress testing is not done.

## V1 Next Practical Work

1. Run real portrait tests with the local portrait test manifest.
2. Review Stage 1 / 5 / 10 output visually.
3. Inspect HardProtect diff images, especially eyes, eyebrows, lips, nostrils, hair, beard, and glasses.
4. Decide which UI panel to finish first:
   - Export options
   - Presets
   - Batch
   - Manual mask brush
5. Do not add V2/Hold features until this V1 review loop is stable.

## Final Review Sentence

The V1 engine direction is now structurally established, but it is not a finished commercial release yet.

The next useful work is real image verification and UI completion, not adding new AI features.

