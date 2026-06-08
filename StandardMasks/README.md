This folder can hold optional UTF-8 project standard mask resources.

Expected PNG names:

- standard_skin_mask.png
- standard_eye_protect_mask.png
- standard_eyebrow_protect_mask.png
- standard_lip_protect_mask.png
- standard_nose_mask.png
- standard_nostril_mask.png
- standard_soft_protect_mask.png

If a PNG is missing or fails to load, `StandardMaskLoader` creates a temporary generated mask and records a debug warning.
