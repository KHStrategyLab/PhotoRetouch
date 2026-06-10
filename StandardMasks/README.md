This folder can hold optional UTF-8 project standard mask resources.

Expected PNG names:

- standard_skin_mask.png
- standard_eye_protect_mask.png
- standard_eyebrow_protect_mask.png
- standard_nose_mask.png
- standard_soft_protect_mask.png

Lip and nostril PNG dummy masks are intentionally not used. Lip masks come from anchored local masks, and standalone nostril masks are removed; nose-hole regions may still be used as an internal exclusion area.

If a supported PNG is missing or fails to load, `StandardMaskLoader` creates an empty mask and records a debug warning.
