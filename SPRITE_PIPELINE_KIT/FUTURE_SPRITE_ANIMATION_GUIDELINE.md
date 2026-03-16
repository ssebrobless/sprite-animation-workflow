# Future Sprite + Animation Guideline

Updated: 2026-03-16

```text
╔══════════════════ Proven Workflow Shape ══════════════════╗
║ 1. lock the contract                                     ║
║    source art • runtime rules • variant axes • families  ║
║                         ▼                                ║
║ 2. export focused handoff packs                          ║
║    base pose • editable board • reference • prompt       ║
║                         ▼                                ║
║ 3. generate in small Gemini jobs                         ║
║    one family at a time • direct full-size download      ║
║                         ▼                                ║
║ 4. import only the improved family                       ║
║    write to verified lane • leave source art untouched   ║
║                         ▼                                ║
║ 5. propagate derived variants locally                    ║
║    colors • palettes • factions • themes                 ║
║                         ▼                                ║
║ 6. audit immediately                                     ║
║    coverage • boards • previews • runtime screenshots    ║
╚═══════════════════════════════════════════════════════════╝
```

## Purpose

This is the compiled reusable guideline for the sprite and animation process that proved reliable during Wevito.

Use it when you want to:

- preserve canonical source art
- improve motion or polish with Gemini
- avoid blur, drift, clipping, and redesign
- keep the workflow reusable across projects

## The Core Method

```text
bad lane
└─ giant mixed jobs
   └─ too many frames per run
      └─ blur • drift • missing parts • weak QA

working lane
├─ source-of-truth contract
├─ focused motion packs
├─ already-open Gemini session
├─ direct full-size download
├─ verified import lane
└─ immediate audit + resume queue
```

## Non-Negotiable Rules

- Preserve the original source art as the source of truth.
- Ask Gemini to edit, not redesign.
- Keep jobs small enough that sharpness survives.
- Reuse the already-open logged-in Gemini tab/window when possible.
- Download results with `Download full size image`.
- Import only the family you just improved.
- Write imports into a verified authored lane, not over the source boards.
- Audit after every meaningful batch.

## Future-Use Runbook

### Phase 1: Lock The Contract

Before generating anything, write down:

- canonical source art root
- runtime output root
- verified authored root
- runtime frame naming
- variant axes
- motion families
- fixed vs variable frame-size rules

Use these project docs first:

- `docs/SPRITE_SOURCE_OF_TRUTH.md`
- `docs/MOTION_AUTHORING_ROADMAP.md`
- `docs/SPRITE_PIPELINE_CHECKLIST.md`
- `docs/GEMINI_PROMPT_RULES.md`

### Phase 2: Make Assets Machine-Addressable

Create a manifest for every source board.

Minimum manifest fields:

- `entity_id`
- source filename
- variant axes
- board layout or component order
- output routing notes

This prevents later confusion about which board maps to which runtime variant.

### Phase 3: Define Motion Families

Start with families that are small and easy to audit.

```text
preferred structure
├─ idle
├─ walk_a
├─ walk_b
├─ care / interaction
└─ expression / reaction
```

Why this works:

- fewer frames per generation keeps pixel detail sharper
- retries are cheaper
- broken motion is isolated faster
- small sprites survive AI editing better

Split more aggressively when:

- frames are blurry
- silhouettes drift
- anatomy collapses
- one family contains too many pose changes

### Phase 4: Export Focused Handoff Packs

Do not send raw source boards with vague prompts.

Each handoff pack should make the task visually obvious:

- base pose
- editable board for one family
- runtime reference board
- prompt text
- optional approved reference for tricky cases

### Phase 5: Generate In Small Gemini Jobs

Best operating lane:

- keep Gemini open
- stay logged into the correct account
- reuse the same stable tab/window
- create a new chat
- upload one prepared pack
- paste one focused prompt
- generate one family
- download full size directly

Do not depend on:

- public image share links
- reopening a fresh browser every run
- giant mixed-family jobs

### Phase 6: Import Only The Improved Family

Import rules:

- extract the edited board from the Gemini result
- import only the named family frames
- write them into the verified authored lane
- keep every untouched family intact

This lets you improve:

- `idle` without touching `walk`
- `walk_a` without touching `walk_b`
- `care` without touching `expression`

### Phase 7: Propagate Variants Locally

Do not spend Gemini generations on every tint or palette if those variants can be derived safely.

Preferred order:

```text
approve one strong base motion
        ▼
propagate colors / palettes / themes locally
        ▼
audit propagated outputs
```

Use AI per variant only when:

- the variant changes silhouette
- the variant changes anatomy or parts
- the variant needs hand-authored exceptions

### Phase 8: Audit Immediately

Every batch should be followed by proof:

- coverage report
- extracted board inspection
- contact sheet or preview board
- runtime screenshot
- targeted scenario launch
- explicit resume queue update

If you wait until the end of a full roster, small mistakes compound into expensive cleanup.

## Prompting Standard

Every focused edit prompt should include:

- preserve the exact uploaded character or entity identity
- edit only the named frames
- leave every other frame unchanged
- keep the board layout and labels intact
- pixel art only
- no blur
- no anti-aliasing
- no checkerboard residue
- no matte halos
- no clipped silhouettes
- return image only

Project-specific guidance should then be added for the domain:

- creatures: preserve silhouette, anatomy, body mass, readable gait
- characters: preserve balance, limbs, gear, attack anticipation
- props: preserve hinge logic, segment alignment, readable state changes
- VFX: preserve frame readability, energy flow, clean shape progression
- UI mascots: preserve recognizability at tiny display sizes

## Common Failure Modes

```text
blur
└─ too many frames in one generation
   fix: split the pack

missing chunks / clipped parts
└─ extraction or crop is too aggressive
   fix: compare source vs imported board and tighten crop rules

hollow torso / missing body mass
└─ prompt is underspecified for filled forms
   fix: explicitly require solid readable body mass

soft public image
└─ share-link retrieval instead of direct download
   fix: use "Download full size image"

automation drift
└─ unstable browser state during long runs
   fix: reuse the open tab and work in smaller batches

one stubborn pack stalls
└─ Gemini-side generation issue
   fix: retry only that pack and keep the resume queue current
```

## Minimum Reusable Toolkit

```text
required toolkit
├─ source-of-truth doc
├─ motion roadmap
├─ checklist
├─ prompt rules doc
├─ source manifest
├─ motion family spec
├─ handoff/export script
├─ import script
├─ variant propagation script
├─ coverage report
└─ preview / screenshot audit
```

Without these pieces, the process becomes memory-based instead of repeatable.

## Kit File Map

Use this folder set as the reusable package:

- `README.md`
  - kit overview and bootstrap entry
- `PROCESS_PLAYBOOK.md`
  - concise repeatable workflow
- `PRESET_GUIDE.md`
  - choose `generic`, `character`, `creature`, `prop`, `vfx`, or `ui_mascot`
- `bootstrap_sprite_pipeline.ps1`
  - scaffold the starter kit into another project
- `templates/SPRITE_SOURCE_OF_TRUTH.template.md`
  - contract doc
- `templates/MOTION_AUTHORING_ROADMAP.template.md`
  - family-by-family roadmap
- `templates/SPRITE_PIPELINE_CHECKLIST.template.md`
  - production checklist
- `templates/GEMINI_PROMPT_RULES.template.md`
  - prompt rules starter
- `templates/incoming_sprite_manifest.template.json`
  - source manifest starter
- `templates/motion_families.template.json`
  - motion-family starter
- `templates/README.template.md`
  - generated per-project start-here doc

## Suggested Setup For A New Project

```text
new project
├─ bootstrap starter kit
├─ fill source-of-truth doc
├─ replace manifest placeholders
├─ customize motion families
├─ customize prompt rules
├─ build export/import scripts
├─ test one representative entity first
└─ scale only after the audit loop is working
```

Practical order:

1. Bootstrap the kit into the new project.
2. Replace placeholder entities and variant axes.
3. Lock the runtime contract in writing.
4. Build focused export/import scripts.
5. Prove the loop on one representative entity.
6. Propagate variants locally after approval.
7. Keep a written resume queue for long runs.

## Preset Selection

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

If none fit cleanly, use `generic` and customize from there.

## What To Carry Forward Every Time

- Keep the source art untouched.
- Keep the jobs focused.
- Keep the browser/session stable.
- Keep imports family-scoped.
- Keep validation immediate.
- Keep the resume queue written down.

That combination was the real breakthrough.
