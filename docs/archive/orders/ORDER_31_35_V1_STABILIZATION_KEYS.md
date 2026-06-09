# K Retouch Pro / PhotoRetouch - ORDER_31~35

# V1 Stabilization / Productization Keys

Status:
Queued / Planned

Purpose:
Define the work boundary after `ORDER_30_HIGH_RES_PERFORMANCE_OPTIMIZATION`.

Core decision:
`ORDER_01` through `ORDER_30` are the V1 core engine development flow.
After `ORDER_30`, do not keep adding new core filters or AI features. Move into stabilization, product polish, settings persistence, packaging, and final review.

Current highest-priority note:
Do not let attractive future features interrupt the current order. Stage and Slider changes are not SnapshotMask regeneration conditions. HardProtect outranks every filter. Hold / After V1 items stay recorded, but they are not part of the current V1 scope. Current source code is the source of truth when this document conflicts with implementation.

Shared mask backbone:
The completed per-photo `FaceSnapshotMaskSet` is the common reference for the V1 skin pipeline. SkinSmooth, BlemishReduce, WrinkleSoftReduce, ToneEven, TextureRestore, Debug Overlay, Preset application, and Batch processing must reuse the same SnapshotMask for that photo. A SnapshotMask is never shared across different photos.

## Remaining Orders After ORDER_30

1. `ORDER_31_CACHE_MEMORY_CLEANUP`
   - Cache cleanup / memory optimization.

2. `ORDER_32_UI_PRODUCT_POLISH`
   - UI product polish.

3. `ORDER_33_USER_SETTINGS_PERSISTENCE`
   - User settings persistence / last work state.

4. `ORDER_34_INSTALLER_PACKAGE`
   - Installer / distribution package.

5. `ORDER_35_V1_FINAL_REVIEW`
   - V1 final review / engine stabilization review.

## After V1 Hold Items

Status:
Hold / After V1

These items are not discarded, but they are outside the current V1 engine scope:

- Multi-face processing.
- Advanced generative AI retouching.
- Background replacement.
- Clothing retouch.
- Advanced manual editing tools.
- Multi-person batch.
- GPU optimization.
- Advanced parallel processing.

Rules:

- Do not implement Hold items during V1 unless the user explicitly changes the scope.
- Keep Hold items recorded for a V2 review.
- Do not delete these items from the roadmap.

## V1 Boundary

- `ORDER_01` to `ORDER_30`: V1 core engine development.
- `ORDER_31` to `ORDER_35`: V1 stabilization / productization / distribution readiness.
- Hold items: review after V1.

## ShapeBalance Current Source Rule

Left/right balance and symmetry correction must not be mixed directly into the V1 skin retouch engine.

Current source already has first-pass `ShapeBalance` code. Treat it as an explicit geometry module that can run before skin retouch when shape controls are active, not as a skin filter.

Principles:

- Reuse the existing SnapshotMask, FaceLandmark, and HardProtect structure.
- Treat ShapeBalance as Geometry Warp, not as a skin filter.
- Transform the image and masks with the same `TransformMap`.
- Use HardProtect to prevent excessive deformation of eyes, lips, nostrils, and hairline.
- Keep Skin Stage and ShapeBalance Stage separate.
- Start with weak/default-safe values.
- ShapeBalance can rebuild its own cached map/balanced bundle when shape controls change, but it must not recreate SnapshotMask unless the normal SnapshotMask regeneration conditions are met.

## Codex Working Rules

1. Do not change the established order sequence.
2. After `ORDER_30`, continue with `ORDER_31`.
3. Prefer stabilization over new feature expansion.
4. Multi-face processing is outside V1.
5. Hold items stay recorded as After V1.
6. Do not expand detailed future orders unless the user asks.

## Final Sentence

`ORDER_30` completes the core engine direction.
After that, the goal is not to make the engine more complex.
The goal is to make the single-face V1 retouch engine stable, usable, packageable, and ready for real photo-studio work.
