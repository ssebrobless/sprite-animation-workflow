# Sprite Pipeline Process Playbook

Updated: 2026-03-16

```text
╔════════════════════ Repeatable Workflow ════════════════════╗
║ 1. lock the contract                                        ║
║    source art, runtime rules, variant axes, motion families ║
║                           ▼                                 ║
║ 2. export focused handoff packs                             ║
║    base pose + editable board + runtime reference + prompt  ║
║                           ▼                                 ║
║ 3. generate in small Gemini jobs                            ║
║    direct full-size download only                           ║
║                           ▼                                 ║
║ 4. import only the family you just improved                 ║
║    keep source art untouched                                ║
║                           ▼                                 ║
║ 5. audit immediately                                        ║
║    contact sheets, runtime screenshots, coverage            ║
║                           ▼                                 ║
║ 6. propagate variants after motion is approved              ║
╚═════════════════════════════════════════════════════════════╝
```

## Core Rules

- Preserve the original art as the source of truth.
- Ask Gemini to edit, not redesign.
- Keep jobs small enough that frame sharpness survives.
- Download directly from Gemini at full size.
- Import into a verified lane, not over your source boards.
- Validate after every meaningful batch.

## Why This Worked

```text
bad lane
└─ huge mixed boards
   └─ blur, clipping, drift, missing body mass

working lane
├─ focused motion packs
├─ already-open Gemini session
├─ direct download
├─ immediate import
└─ immediate audit
```

## Recommended Motion-Pack Strategy

Use smaller packs when detail matters.

```text
easy assets
├─ idle pack
└─ walk pack

detail-sensitive assets
├─ idle pack
├─ walk_a
├─ walk_b
├─ care
└─ expression
```

## Good Default Order

1. Define `SPRITE_SOURCE_OF_TRUTH.md`
2. Fill `incoming_sprite_manifest.json`
3. Customize `motion_families.json`
4. Write prompt rules for the project domain
5. Build pack export/import scripts
6. Start with one representative entity
7. Approve base motion
8. Propagate palette/state variants locally

## Common Failure Modes

```text
blur
└─ too many frames per generation
   fix: split the pack

missing chunks / hollow bodies
└─ weak extraction or poor prompt specificity
   fix: compare source vs runtime and re-run only that pack

public-image softness
└─ share-link retrieval instead of direct download
   fix: use "Download full size image"

automation drift
└─ unstable browser state during long runs
   fix: reuse the already-open Gemini tab and resume in smaller batches
```

## Reuse Checklist

- Keep this folder with the new project bootstrap.
- Keep a per-project contract doc.
- Keep a per-project motion roadmap.
- Keep one coverage report and one audit board generator.
- Keep the resume queue written down for long Gemini runs.

## Long-Form Reference

For the full version of the method, use:

- [REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md](../REUSABLE_SPRITE_PIPELINE_PLAYBOOK.md)
