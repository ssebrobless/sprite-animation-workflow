# Sprite Animation Workflow

Updated: 2026-03-20

```text
╔══ Repo Layout ═══════════════════════════════════════════════════════╗
║ SPRITE_PIPELINE_KIT/                                                ║
║   reusable workflow bundle, templates, bootstrap                    ║
║ tools/                                                              ║
║   launchers and reusable helper scripts                             ║
║ src/                                                                ║
║   Sprite Workflow App + core libraries                              ║
║ sample-projects/                                                    ║
║   linked project profiles, starting with Wevito                     ║
║ docs/                                                               ║
║   roadmap, product spec, backlog                                    ║
╚══════════════════════════════════════════════════════════════════════╝
```

This repository serves two purposes:

- a reusable sprite/animation workflow bundle
- a cross-platform desktop app for planning, reviewing, editing, staging, and exporting sprite work

## Sprite Workflow App

The app is a manual-first sprite workstation with visible AI assistance layered on top.

Current app capabilities:

- load a project profile and discover existing authored/runtime assets
- plan a blank project from scratch or adopt an existing sprite tree
- keep provider-agnostic AI adapters visible in-app instead of hard-coding one AI service into the domain model
- browse rows by species, age, gender, family, coverage, and review state
- review animations with frozen edit frames, live playback, onion skin, and compare tools
- paint directly in-app with layers, palettes, selection tools, lasso, shapes, transforms, blink compare, history, and reference layers
- save frame-level notes, statuses, issue tags, and approval state
- stage authored, runtime, and editor-canvas candidates before import
- build AI requests and keep visible AI task/activity history
- export trusted sets, validation reports, project kits, and blank validation sandboxes
- surface project readiness, discovery categories, trusted export blockers, and portability state in-app

## Start Here

Primary roadmap:

- [PERFECT_LOOP_EXECUTION_PLAN.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/PERFECT_LOOP_EXECUTION_PLAN.md)
- [PERFECT_LOOP_MILESTONE_BACKLOG.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/PERFECT_LOOP_MILESTONE_BACKLOG.md)
- [NEXT_WAVE_PLAN.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/NEXT_WAVE_PLAN.md)
- [PRODUCT_SPEC.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/PRODUCT_SPEC.md)
- [RELEASE_CHECKLIST.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/RELEASE_CHECKLIST.md)

Sample profiles:

- [wevito.project.json](C:/Users/fishe/Documents/projects/sprite-workflow-app/sample-projects/wevito.project.json)
- [blank-starter.project.json](C:/Users/fishe/Documents/projects/sprite-workflow-app/sample-projects/blank-starter.project.json)
- [imported-external.project.json](C:/Users/fishe/Documents/projects/sprite-workflow-app/sample-projects/imported-external.project.json)

## Run

Preferred Windows launch:

```text
tools\launch_sprite_workflow_app.vbs
```

No-console PowerShell launch:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\launch_sprite_workflow_app.ps1
```

Test the solution:

```powershell
dotnet test .\SpriteWorkflow.sln
```

## Solution

```text
src/
  SpriteWorkflow.App/
  SpriteWorkflow.Core/
  SpriteWorkflow.Infrastructure/
  SpriteWorkflow.ProjectModel/
  SpriteWorkflow.Tests/
```

## Reusable Workflow Bundle

If you want the reusable docs/bootstrap first, start with:

- `SPRITE_PIPELINE_KIT/README.md`
- `SPRITE_PIPELINE_KIT/PROCESS_PLAYBOOK.md`
- `SPRITE_PIPELINE_KIT/FUTURE_SPRITE_ANIMATION_GUIDELINE.md`
- `REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md`

## Bootstrap A New Project

```powershell
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\path\to\new-project" -ProjectName "New Project" -Preset generic
```

That scaffold creates the starter docs, manifests, motion-family definitions, and project structure needed to begin a new sprite pipeline.
