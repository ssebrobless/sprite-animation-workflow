# Sprite Animation Workflow

Updated: 2026-03-16

```text
╔══════════════ Bundle Layout ══════════════╗
║ SPRITE_PIPELINE_KIT/                      ║
║   reusable kit + bootstrap + templates   ║
║ tools/                                   ║
║   bootstrap implementation               ║
║ REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md     ║
║   long-form reference                    ║
╚═══════════════════════════════════════════╝
```

This repository is the standalone reusable sprite and animation workflow bundle extracted from the Wevito process.

## Start Here

Open these first:

- `SPRITE_PIPELINE_KIT/FUTURE_SPRITE_ANIMATION_GUIDELINE.md`
- `SPRITE_PIPELINE_KIT/README.md`
- `SPRITE_PIPELINE_KIT/PROCESS_PLAYBOOK.md`
- `SPRITE_PIPELINE_KIT/PRESET_GUIDE.md`
- `REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md`

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

## Notes

- This bundle keeps the working relative layout between `SPRITE_PIPELINE_KIT` and `tools`, so the bootstrap command works from here.
- It intentionally excludes Wevito-only game docs and species-specific production notes.
