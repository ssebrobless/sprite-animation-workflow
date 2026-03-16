# Sprite Pipeline Kit

Updated: 2026-03-16

```text
╔════════════════════ Sprite Pipeline Kit ════════════════════╗
║ bootstrap wrapper  │ scaffold a new sprite project         ║
║ templates          │ reusable generic docs + manifests     ║
║ workflow playbook  │ repeat the process without re-explaining║
║ preset guide       │ pick a starting shape for the project ║
╚═════════════════════════════════════════════════════════════╝
```

This folder is the easiest place to find the reusable sprite and animation workflow we refined in Wevito and packaged for reuse here.

## Open These First

- [FUTURE_SPRITE_ANIMATION_GUIDELINE.md](./FUTURE_SPRITE_ANIMATION_GUIDELINE.md)
- [PROCESS_PLAYBOOK.md](./PROCESS_PLAYBOOK.md)
- [PRESET_GUIDE.md](./PRESET_GUIDE.md)
- [bootstrap_sprite_pipeline.ps1](./bootstrap_sprite_pipeline.ps1)
- [templates](./templates)

## Quick Start

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\path\to\new-project" -ProjectName "New Project" -Preset character
```

For a fully custom scaffold:

```powershell
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\path\to\new-project" -ProjectName "New Project" -Entities hero turret explosion -EntityLabelSingular asset -EntityLabelPlural assets -VariantAxes state faction palette
```

## Presets

- `generic`
- `character`
- `creature`
- `prop`
- `vfx`
- `ui_mascot`

## What The Bootstrap Creates

- `docs/SPRITE_SOURCE_OF_TRUTH.md`
- `docs/MOTION_AUTHORING_ROADMAP.md`
- `docs/SPRITE_PIPELINE_CHECKLIST.md`
- `docs/GEMINI_PROMPT_RULES.md`
- `docs/SPRITE_PIPELINE_START_HERE.md`
- `tools/incoming_sprite_manifest.json`
- `tools/motion_families.json`

## Folder Shape

```text
SPRITE_PIPELINE_KIT/
├─ README.md
├─ PROCESS_PLAYBOOK.md
├─ PRESET_GUIDE.md
├─ bootstrap_sprite_pipeline.ps1
└─ templates/
   ├─ README.template.md
   ├─ SPRITE_SOURCE_OF_TRUTH.template.md
   ├─ MOTION_AUTHORING_ROADMAP.template.md
   ├─ SPRITE_PIPELINE_CHECKLIST.template.md
   ├─ GEMINI_PROMPT_RULES.template.md
   ├─ incoming_sprite_manifest.template.json
   └─ motion_families.template.json
```

## Notes

- `FUTURE_SPRITE_ANIMATION_GUIDELINE.md` is the compiled future-facing guide that merges the reusable docs into one reference.
- The PowerShell wrapper calls the bundled Python bootstrap at `..\tools\bootstrap_sprite_pipeline.py`.
- The bundled long-form reference is `..\REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md`.
- This folder exists so you do not have to remember which docs, tools, and templates were involved.
