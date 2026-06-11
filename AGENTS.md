# AGENTS.md

//* Do not edit, rewrite, delete, move, refactor, or re-encode any file without explicit user permission. //*

## Startup Handshake

At the beginning of every new Codex session, read this AGENTS.md file first.
Before analyzing, editing, patching, building, or summarizing anything, reply with this exact handshake:
"AGENTS.md loaded. I will not modify files without explicit permission, and I will preserve UTF-8 with BOM."

## Language Policy

* Keep project documents, technical notes, code comments, commit messages, and task summaries in English unless the user explicitly asks for Korean.
* Korean may be used only for direct conversation with the user.
* When summarizing work state, use short English bullet-style lines.

## Encoding Rules

- Preserve UTF-8 with BOM.
- Do not use Set-Content, Out-File, or script rewrites unless UTF-8 BOM is explicitly preserved.
- Verify important text files start with EF BB BF after writing.

## Work Timing and Summary Rules

* Do not create a status summary or compaction-style recap right before starting work or right before applying a patch.
* Only summarize during natural pause points.

Pause points are:

First, after file structure analysis is complete.
Second, right after the modification plan is explained.
Third, after one feature patch is complete.

When the user says "proceed", "patch it", "fix it", or "go ahead", do not summarize. Start working immediately.

## Snippet And Clipboard Encoding Rule

- Pasted snippets from chat, browser, clipboard, or terminal are text fragments, not files.
- Snippets do not carry a UTF-8 BOM.
- Do not report a pasted snippet as invalid only because it has no BOM.
- The UTF-8 with BOM requirement applies only to repository files saved on disk.
- When applying a pasted snippet to a repository file, preserve or write the target file as UTF-8 with BOM.
- After writing an important text file, verify the saved file starts with `EF BB BF`.

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
Start-Process -FilePath .\bin\x64\Debug\net10.0-windows\PhotoRetouch.exe -WindowStyle Hidden
```

## Korean Text And Encoding

The app has Korean UI text, so source encoding must stay predictable.

- The project uses `.editorconfig` with `charset = utf-8-bom` for source and config files.
- Keep Korean text files such as `.cs`, `.xaml`, `.json`, and `.md` as UTF-8 with BOM.
- If Korean looks broken in terminal output, first suspect the viewer or shell encoding before rewriting source files.
- In PowerShell, prefer explicit UTF-8 reads for inspection, for example `Get-Content -Encoding UTF8`.
- Avoid broad encoding conversions across the whole repository unless there is a verified file-level problem.

## Reference Documents

Read these only when the task needs them:

- `docs\CODEX_PROJECT_REFERENCE.md`
- `docs\PORTRAIT_MASK_ENGINE_REFERENCE.md`
- `docs\ANCHOR_MESH_GUIDE_REFERENCE.md`
- `docs\ENGINE_DESIGN.md`
- `docs\FEATURE_STATUS_AND_ROADMAP.md`
- `docs\FACE_RATIO_GUIDES.md`

Do not load or summarize all reference documents by default.
Use them only for larger design, mask engine, AnchorMesh, preview/save, or retouch pipeline work.

## Quick Model Use Policy

- Use GPT-5.4 with medium reasoning for normal PhotoRetouch coding and debugging.
- Use GPT-5.5 Pro with high reasoning for architecture review, full-flow analysis, and root-cause discussion only.
- Use GPT-5.3 with low or medium reasoning for short document, BOM, wording, and simple file checks.
