# PhotoRetouch Engine Design

## Goal

PhotoRetouch is a 64-bit portrait retouching tool for daily photo work. The UI should stay predictable and calm, while preview generation must feel responsive enough for repeated professional use.

The current priority is not maximum theoretical speed. The priority is stable behavior, clear preview quality, and a path toward faster engines without rewriting the whole application.

Customization is a product requirement. Do not remove or merge useful photographer controls simply to make the code smaller or the current engine faster. Optimize the preview and engine path instead.

## Current Engine

The current preview engine is a C# CPU engine.

- WPF handles the UI and image display.
- Pixel adjustments are calculated on a background thread.
- Input is blocked while a preview render is running.
- Tone correction sliders use throttled live preview while dragging; heavier future tools may still commit on mouse release.
- Curve preview has been experimented with, but heavy image recalculation during point movement should be avoided.
- Preview processing can use a reduced preview image size from settings.

Current implemented adjustment order:

1. Preview source sizing
2. Exposure
3. Contrast
4. Saturation
5. White balance
6. Curve LUT
7. Blur or sharpen

## Preview Policy

The preview is for judging the edit, not final export quality.

- Interactive effect preview should render against the image size actually needed by the current preview viewport.
- The visible preview cell size is the first sizing target for effect preview rendering.
- Low performance users can also set a maximum long side size.
- Current allowed settings preview long side range is 800px to 4000px.
- The settings preview limit is an upper bound; it should not force effect previews to render larger than the visible preview needs.
- This policy applies to all interactive tool categories, not only photo adjustment: curves, skin, face shape, eyes, nose, mouth, hair, clothing, and background.
- Multi-select split preview is for choosing and comparing photos. It should show original/lightweight views and avoid applying retouch effects to all selected images.
- Original comparison should continue to show the original source image.
- Future export should not be limited by preview size.

## Interaction Policy

Heavy work should not run continuously while the user is dragging unless it is throttled and working against the screen-sized preview source.

- Tone correction sliders: update UI while dragging and render throttled live preview.
- Future heavy sliders: update UI while dragging, render preview on release unless an optimized preview path exists.
- Curve point movement: move curve UI while dragging, render preview on release.
- Curve opacity or strength: render with throttled live preview.
- Mouse wheel should not adjust retouch sliders.
- Photo-list arrow navigation should be contextual. It should only apply after clicking a photo item in the left list, and should stop after clicking preview, tools, empty space, or other controls.
- Mouse helper gestures are separate from keyboard shortcuts and may include Space as well as Ctrl, Shift, and Alt.
- Individual tool controls should remain independently adjustable. Shared/global application should be added later as explicit copy or batch workflow.

## Customization And Performance Policy

The app should keep photographer-facing controls flexible even if the source code becomes larger.

- Keep per-tool and per-sub-control values independent during development.
- Design later setting-copy workflows for applying one photo's settings to selected photos.
- Design later batch apply/export workflows separately from interactive preview.
- Do not make multi-select automatically process all selected photos during preview.
- Prefer performance fixes in this order: screen-sized effect preview sources, throttled live preview, caching, engine separation, native CPU, optional GPU.
- Final output should use the original image and full adjustment model, not the reduced preview bitmap.

## Engine Roadmap

### Stage 1: Stabilize C# CPU Engine

Keep the current engine but improve structure.

- Separate UI controls from preview engine logic.
- Build one adjustment request object from the current UI state.
- Avoid repeated full-image recalculation.
- Keep reduced preview size support.
- Create screen-sized effect preview sources before running tool effects.
- Do not let individual tool engines independently decide to process full original images for interactive preview.
- Keep one render in progress at a time.

### Stage 2: Add Engine Interface

Introduce a common engine contract so the UI does not care which engine is used.

Suggested shape:

```csharp
public interface IPreviewEngine
{
    BitmapSource Render(BitmapSource source, PreviewAdjustment adjustment);
}
```

Suggested implementations:

- `CSharpPreviewEngine`
- `NativeCpuPreviewEngine` later
- `GpuPreviewEngine` later

Settings labels:

- Stable mode - C# CPU engine
- Fast mode - Native CPU engine
- Accelerated mode - GPU engine

### Stage 3: Native CPU Engine

Move pixel loops to a C++ x64 DLL.

Reasons:

- Keeps GPU dependency out of the default path.
- More predictable than GPU for mixed user hardware.
- Fits the 64-bit-only design.
- Allows future SIMD and multithreaded optimization.

The first native target should be tone and curve processing using LUTs.

### Stage 4: GPU Engine

GPU should be optional, not default.

Reasons:

- GPU drivers and devices vary a lot.
- Older or low-end GPUs may be unstable or slower.
- It is useful as a future acceleration option for capable machines.

## Native Engine Direction

The native engine should receive BGRA32 pixels and LUT or adjustment values, then return BGRA32 pixels.

Initial C++ function shape:

```cpp
extern "C" __declspec(dllexport)
void ApplyPreviewBgra32(
    const unsigned char* source,
    unsigned char* destination,
    int width,
    int height,
    int stride,
    const PreviewAdjustmentNative* adjustment);
```

Start with preview rendering only. Do not move final export or file IO into native code yet.

## Important Decisions

- Do not auto-sync the working folder during editing.
- Use manual refresh for newly added files.
- Do not lock source image files while the app is running.
- Keep preview and final output paths conceptually separate.
- Keep CPU as the safe default.
