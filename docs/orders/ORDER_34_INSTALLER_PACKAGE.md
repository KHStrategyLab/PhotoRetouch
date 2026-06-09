# K Retouch Pro / PhotoRetouch - ORDER_34

This file is UTF-8.

## Stage

Installer / distribution package

## Status

InProgress / Publish package script implemented

## Goal

Prepare a safe V1 x64 desktop distribution path without yet committing generated binaries or installer output.

## Implemented

- Added `build/Publish-V1.ps1`.
- Publish target is fixed to:
  - `win-x64`
  - `Platform=x64`
- Script supports:
  - Release publish
  - self-contained publish by default
  - output directory cleanup
  - ZIP package generation
- Generated publish outputs are excluded from git.

## Command

```powershell
.\build\Publish-V1.ps1
```

Optional:

```powershell
.\build\Publish-V1.ps1 -SelfContained $false
```

## Output

Default output:

- `publish/v1/KRetouchPro-win-x64/`
- `publish/v1/KRetouchPro-win-x64.zip`

## Still Pending

- Real installer wizard.
- Code signing.
- Version stamping.
- Release notes.
- Clean install/uninstall flow.
- Desktop shortcut creation.
- Packaging QA on a second PC.

## Guardrails

- Do not commit generated publish output.
- Keep V1 x64-only.
- Do not add V2/Hold features during packaging.
- Do not package private test images.

## Verification

- `dotnet build .\PhotoRetouch.sln -p:Platform=x64`
- Result: build passed with 0 warnings and 0 errors.

