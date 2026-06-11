# CODEX_PROJECT_REFERENCE.md

This document contains the active project direction for PhotoRetouch.

## Project Identity

PhotoRetouch is a 64-bit Windows WPF portrait retouching application for daily photographer workflow.

The application should feel stable, calm, and predictable.

## Current Baseline

The current baseline is an early engine reset:

1. Load a photo
2. Detect the face area
3. Estimate landmarks
4. Build guide masks and guide overlays
5. Keep the existing panel UI and preview shell working

The current goal is not feature expansion. The current goal is to simplify the engine core until the guide pipeline is easy to inspect and maintain.

## Program Policy

PhotoRetouch is not an automatic AI face-correction program.

Detected data should help locate the face and prepare future manual tools. It should not automatically apply beautification, shape correction, or hidden retouch.

## Preview Policy

- Keep the preview shell alive.
- Keep background rendering behavior safe and predictable.
- Use display-sized preview sources for interactive work.
- Do not force original-resolution processing during routine preview.
- Keep save/export separate from the live preview surface.

## Working Policy

- Prefer scoped changes.
- Keep the current panel UI structure unless a clear break makes change necessary.
- The engine may be rewritten under the UI shell if that is faster than removing old behavior piece by piece.
- Build verification should still use `dotnet build .\\PhotoRetouch.sln -p:Platform=x64`.

## Active Engineering Direction

Main runtime focus:

- `MainWindow.xaml`
- `MainWindow.xaml.cs`
- `Tools/Masking/StandardMaskWarpEngine.cs`
- `Tools/Masking/SnapshotMaskBuilder.cs`
- `Tools/Masking/FaceSnapshotMaskSet.cs`
- `Tools/Masking/FaceAnalysisResult.cs`
- `Tools/Masking/DebugMaskExporter.cs`

The active engine direction is:

```text
detect
-> landmark
-> guide
-> preview
```

If old retouch logic conflicts with this reset, the simpler guide pipeline wins.
