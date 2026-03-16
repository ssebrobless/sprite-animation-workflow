# Sprite Pipeline Preset Guide

Updated: 2026-03-16

```text
╔════════════════════ Preset Selection ════════════════════╗
║ generic    │ anything that needs a neutral starter       ║
║ character  │ humanoids, enemies, fighters, NPCs         ║
║ creature   │ animals, monsters, beasts, companions      ║
║ prop       │ doors, devices, containers, machinery      ║
║ vfx        │ sparks, smoke, impacts, magic, energy      ║
║ ui_mascot  │ helpers, chat bots, badge characters       ║
╚═══════════════════════════════════════════════════════════╝
```

## When To Use Each Preset

- `generic`
  - best when you want the workflow but not any strong domain assumptions
- `character`
  - for bipedal characters with attacks, hurt states, and move cycles
- `creature`
  - for animals or monsters where silhouette and anatomy shifts matter
- `prop`
  - for doors, chests, control panels, stations, tools, or machines
- `vfx`
  - for effects where energy flow and frame readability matter more than anatomy
- `ui_mascot`
  - for small helper characters that live inside interface space

## How To Pick

```text
is it a living thing with anatomy?
├─ yes
│  ├─ mostly humanoid? → character
│  └─ mostly animal/monster? → creature
└─ no
   ├─ mechanical/object state changes? → prop
   ├─ energy / smoke / impact / magic? → vfx
   └─ interface helper / mascot? → ui_mascot
```

If none fit cleanly, use `generic` and customize `motion_families.json` after bootstrap.

## Example Commands

```powershell
# Creature project
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\work\pet-game" -ProjectName "Pet Game" -Preset creature

# Prop project
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\work\factory-ui" -ProjectName "Factory UI" -Preset prop

# Fully custom generic project
powershell -ExecutionPolicy Bypass -File .\SPRITE_PIPELINE_KIT\bootstrap_sprite_pipeline.ps1 -TargetRoot "C:\work\space-game" -ProjectName "Space Game" -Preset generic -Entities shuttle drone crate -EntityLabelSingular asset -EntityLabelPlural assets -VariantAxes state faction palette
```
