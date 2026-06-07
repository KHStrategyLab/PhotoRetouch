# AGENTS.md

## Project Identity

PhotoRetouch is a 64-bit Windows WPF portrait retouching application for daily photographer workflow. The user is a working photographer and is shaping the tool around practical ID/photo retouching needs, not around generic image editor conventions.

The application should feel stable, calm, and predictable. Avoid surprising UI movement, sudden list changes, or heavy live recalculation while the user is adjusting values.

## Repository And Build

- Main repository path: `C:\Users\beint\source\repos\PhotoRetouch`
- Solution: `PhotoRetouch.sln`
- Target platform: x64 only
- Build command:

```powershell
dotnet build .\PhotoRetouch.sln -p:Platform=x64
```

Before building, close the running app to avoid file lock errors:

```powershell
Get-Process PhotoRetouch -ErrorAction SilentlyContinue | Stop-Process -Force
```

Run after successful build:

```powershell
Start-Process -FilePath .\bin\x64\Debug\net8.0-windows\PhotoRetouch.exe -WindowStyle Hidden
```

## Important Documents

Read these before making larger design or engine changes:

- `docs\ENGINE_DESIGN.md`
- `docs\FEATURE_STATUS_AND_ROADMAP.md`

These documents describe the current engine state, roadmap, menu status, and next recommended steps.

## Current Product Direction

The app is being built in layers:

1. UI and workflow stability
2. Photo list and preview behavior
3. Preview adjustment engine
4. Curve editor
5. Engine separation
6. Native CPU engine
7. Optional GPU engine

Do not jump directly to GPU or native code without first keeping the current C# engine path stable and separable.

## Customization First Policy

PhotoRetouch should favor photographer-level control over early simplification.

- Keep each tool and each meaningful sub-control independently adjustable during development.
- Do not collapse multiple controls into one shared/global value just to make the code shorter.
- Batch apply, copy settings to selected photos, and global apply are later workflow conveniences, not the base model.
- Multi-select should remain a choosing/comparison workflow until explicit setting-copy or batch behavior is designed.
- If customization makes the code larger, prefer improving structure, preview sizing, caching, and engine separation over removing controls.
- Performance should be solved through screen-sized preview sources, render throttling, engine interfaces, native CPU, optional GPU, and caching.

## Preview Engine Policy

Current preview engine is C# CPU-based.

Key rules:

- Most sliders update preview only on mouse release.
- Mouse wheel must not adjust retouch sliders.
- Curve point dragging should move only the curve UI while dragging.
- Preview should be rendered when the point is released.
- Preview rendering should run in the background.
- Only one preview render should run at a time.
- While preview rendering is in progress, conflicting input should be blocked.
- Do not darken the whole UI during preview rendering.
- Preview max long side can be limited in settings from 800px to 4000px.
- All interactive tool effects must render against a screen-sized effect preview source, not the full original image.
- Use the current preview viewport/cell size as the first limit for effect preview rendering; the preview setting remains an upper bound.
- This screen-sized preview policy applies to every future tool category: photo adjustment, curves, skin, face shape, eyes, nose, mouth, hair, clothing, and background.
- Multi-select split preview is a choosing/comparison mode. Do not apply retouch effects to multiple selected photos during preview.
- Final export/save must stay conceptually separate and should render from the original source, not from the reduced screen preview.
- Original comparison should continue to use the original source image.

## Engine Roadmap

Do this before adding C++:

- Introduce a `PreviewAdjustment` model.
- Introduce an `IPreviewEngine` interface.
- Move current pixel code into a `CSharpPreviewEngine`.

Later:

- Add `NativeCpuPreviewEngine` using a C++ x64 DLL.
- Keep CPU C# as the safe fallback.
- GPU remains optional and should not be the default.

Settings already contains a `Performance` tab placeholder:

- Stable mode - CPU engine
- Accelerated mode - GPU engine, implementation pending

This may later be renamed or expanded when native CPU engine is added.

## UI Design Preferences

- Use neutral colors. Avoid red accents because they can confuse photo judgment.
- Avoid sudden layout shifts.
- Keep list selection borders 1px so items do not shake.
- Do not use heavy overlays unless absolutely necessary.
- Use compact, work-focused UI rather than marketing-style screens.
- Preserve the current right-monitor maximized startup behavior.
- Keep controls photographer-friendly in language.

## Photo List And Preview Rules

- Work folder is configured in settings.
- App preloads the work folder on startup.
- `작업 폴더 새로고침` manually loads newly added files.
- Do not auto-sync the work folder yet.
- Prevent duplicate photo paths.
- Multi-select is limited to 8 images.
- Split preview:
  - 2 images: 2 split
  - 4 images: 4 split
  - 5 images: 3+2
  - max 2 rows, max 4 columns
- Selecting a photo from the list should reset selected preview transforms to Fit in.
- Split preview zoom max should allow source 1:1, not beyond.
- Dragging zoomed images should not reveal blank space.

## Curve Editor Direction

Target behavior is closer to Photoshop-style curves, not a simple slider.

Current rules:

- Channels: All, R, G, B
- Max 7 anchors per channel
- Endpoints are fixed and cannot be deleted
- Add points by clicking the curve canvas/line
- Drag points to edit
- Delete selected point with Delete or Backspace
- Drag a point outside the curve box to delete
- Point movement should not render the image continuously
- Render preview on release

Still needed:

- Cleaner curve labels
- Better point hit testing
- Smooth interpolation
- Selected point input/output display
- Per-channel reset
- Shortcut settings entry for curve point deletion

## Settings Files

Settings are stored under:

```text
%APPDATA%\PhotoRetouch
```

Known files:

- `section-order.json`
- `shortcuts.json`
- `preview-settings.json`
- `working-folder.json`
- `performance-settings.json`

## Working With Code

- Prefer scoped changes.
- Do not refactor large parts unless it directly supports the current step.
- Use `apply_patch` for manual edits.
- Keep build verification with x64.
- If app is running, stop it before build.
- Do not revert unrelated user changes.

## Suggested Next Engineering Step

After stabilizing the current curve editor behavior, start engine separation:

1. Create `PreviewAdjustment`.
2. Create `IPreviewEngine`.
3. Move current `PhotoAdjustmentEngine` logic into `CSharpPreviewEngine`.
4. Keep `PhotoAdjustmentEngine` as a facade if useful.
5. Only after that, plan `PhotoRetouch.Native` for C++.
