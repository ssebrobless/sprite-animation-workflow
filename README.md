# Sprite Animation Workflow

Updated: 2026-03-18

```text
+---------------------------------------------------------------+
| Repository Layout                                             |
|                                                               |
| SPRITE_PIPELINE_KIT/                                          |
|   reusable workflow kit, bootstrap, templates                 |
| tools/                                                        |
|   reusable bootstrap implementation                           |
| REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md                          |
|   long-form workflow reference                                |
|                                                               |
| src/                                                          |
|   cross-platform Sprite Workflow App                          |
| sample-projects/                                              |
|   project profiles, starting with Wevito                      |
| docs/                                                         |
|   app product spec and v1 backlog                             |
+---------------------------------------------------------------+
```

This repository now contains both:

- the standalone reusable sprite and animation workflow bundle extracted from the Wevito process
- the new cross-platform `Sprite Workflow App` desktop application built to browse, review, repair, and request sprite and animation work across projects

## Start Here

If you want the reusable workflow bundle first, open:

- `SPRITE_PIPELINE_KIT/FUTURE_SPRITE_ANIMATION_GUIDELINE.md`
- `SPRITE_PIPELINE_KIT/README.md`
- `SPRITE_PIPELINE_KIT/PROCESS_PLAYBOOK.md`
- `SPRITE_PIPELINE_KIT/PRESET_GUIDE.md`
- `REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md`

If you want the desktop app first, open:

- `docs/PRODUCT_SPEC.md`
- `docs/V1_BACKLOG.md`
- `sample-projects/wevito.project.json`

## Sprite Workflow App

The app is a separate cross-platform desktop tool for sprite and animation generation workflows.
Wevito is the first configured project profile.

Current app features:

- project profile loading
- authored coverage indexing
- filterable asset browser
- authored vs runtime animation compare viewer
- row-level review notes and statuses
- repair queue
- request drafting, saving, and exportable AI handoff text

Solution layout:

```text
src/
  SpriteWorkflow.App/
  SpriteWorkflow.Core/
  SpriteWorkflow.Infrastructure/
  SpriteWorkflow.ProjectModel/
  SpriteWorkflow.Tests/
```

Run the app:

```powershell
dotnet run --project .\src\SpriteWorkflow.App\SpriteWorkflow.App.csproj
```

Run tests:

```powershell
dotnet test .\SpriteWorkflow.sln
```

## Bootstrap A New Project

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\path\to\new-project" -ProjectName "New Project" -Preset generic
```

That scaffold will create:

- `docs/SPRITE_SOURCE_OF_TRUTH.md`
- `docs/MOTION_AUTHORING_ROADMAP.md`
- `docs/SPRITE_PIPELINE_CHECKLIST.md`
- `docs/GEMINI_PROMPT_RULES.md`
- `docs/SPRITE_PIPELINE_START_HERE.md`
- `tools/incoming_sprite_manifest.json`
- `tools/motion_families.json`

## What Is Included

- the compiled future-facing guideline
- the concise repeatable playbook
- the preset guide
- the PowerShell bootstrap wrapper
- the Python bootstrap implementation
- the reusable templates
- the long-form reference playbook
- the new Avalonia-based desktop workflow app

## Notes

- The bundle keeps the working relative layout between `SPRITE_PIPELINE_KIT` and `tools`, so the bootstrap command works from here.
- The app lives alongside the reusable docs so the repository can serve both as a workflow reference and as the long-term production tool.
