# Gender-Neutral Mask Policy

This file is UTF-8.

## Core Rule

K Retouch Pro does not use gender as a default engine condition.

The retouch pipeline is mask-based and feature-based, not gender-based.

## Engine Policy

Do not add `GenderDetection` to the base correction pipeline.

Correction masks must be decided from visible image evidence:

- Skin tone candidates
- Blemish candidates
- Wrinkle candidates
- Lip mask
- Hair mask
- Glasses mask
- Beard hair mask
- Beard shadow / shaving mark / blue cast candidates

Gender must not decide whether those masks exist or whether a correction is applied.

## Feature Rules

`BeardShadowMask` is not a male-only feature.

It applies only when the image contains detectable shaving marks, beard shadow, blue cast, or similar tone discoloration. Real beard, mustache, and sideburn hair remain protected details.

`LipMask` is not a female-only feature.

Lips are hard-protected for every detected face. Lip color must not be shifted toward skin tone by skin retouching.

Wrinkles, blemishes, skin tone, hair, glasses, beard shadow, and lip protection are all handled from detected masks and candidate regions.

## Future Use

If gender-like information is ever considered, it may only be used as optional preset recommendation metadata.

It must not be used to build correction masks, skip protection masks, or choose retouch strength in the base engine.

## Test Metadata

Test sets may keep optional gender labels for coverage tracking only.

Those labels are not retouch inputs and must not affect mask decisions.
