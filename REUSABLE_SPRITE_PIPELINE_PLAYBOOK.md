# Reusable Sprite Pipeline Playbook

Updated: 2026-03-15

```text
╔════════════════════ Reusable AI Sprite Workflow ════════════════════╗
║ 1. lock source of truth                                             ║
║    canonical sprites, frame contract, palette rules                 ║
║                 ▼                                                   ║
║ 2. export focused authoring packs                                   ║
║    base pose + editable board + runtime reference + prompt          ║
║                 ▼                                                   ║
║ 3. generate in small Gemini jobs                                    ║
║    idle / walk_a / walk_b, direct full-size download only           ║
║                 ▼                                                   ║
║ 4. extract + import verified frames                                 ║
║    authored_verified/<entity>/<variant_axes>/...                    ║
║                 ▼                                                   ║
║ 5. propagate color variants after base motion is approved           ║
║                 ▼                                                   ║
║ 6. audit visually + in runtime                                      ║
║    coverage, contact sheets, targeted scenarios, screenshot proof   ║
╚══════════════════════════════════════════════════════════════════════╝
```

## Purpose

This is the reusable workflow we proved out in Wevito for taking canonical source sprites, improving them with Gemini, importing them safely, and validating them until they are runtime-ready.

The key idea is simple:

- preserve the original uploaded art as the source of truth
- ask Gemini to edit, not redesign
- keep each generation small enough to stay sharp
- import only the family you just improved
- validate immediately before moving on

## What Made This Work

```text
old approach
└─ large mixed boards
   └─ too many frames per generation
      └─ blur, clipping, hollow bodies, drift

working approach
├─ canonical source boards
├─ focused motion families
├─ one upload pack per run
├─ direct full-size download
├─ immediate import
└─ immediate audit
```

The biggest quality gains came from:

1. Splitting dense jobs into smaller motion families
2. Using direct full-size downloads instead of share-link/public images
3. Importing into a verified authored lane instead of overwriting source art
4. Running visual audits after every meaningful batch

## The Reusable Workflow

### 1. Lock the contract first

Before generating anything, define these items in writing:

- canonical source sprite location
- which assets are the source of truth
- runtime frame contract
- which dimensions are fixed and which are variable
- color-variant policy
- which motions/actions must exist
- which variant axes exist for the project

For Wevito, that meant:

- `incoming_sprites` = source of truth
- runtime output = generated/imported PNGs
- color variants are derived after base motion approval

### 2. Build an asset manifest

Every source board should be machine-addressable.

Minimum fields:

- `entity_id`
- source filename
- variant axes
- board/cell layout
- output folder rules

This prevents later confusion about which board represents which variant.

### 3. Export focused authoring packs

Do not send raw source boards directly to Gemini with a vague prompt.

Instead, generate a handoff pack that includes:

- base pose
- editable board for the specific family
- runtime reference board
- prompt text
- optional approved reference for tricky cases

The authoring pack should make the task visually obvious.

### 4. Use focused motion families

This was the most important quality fix.

```text
do not do
└─ one giant board with too many frames

do this instead
├─ locomotion_idle
├─ locomotion_walk_a
├─ locomotion_walk_b
├─ care
└─ expression
```

Why:

- fewer frames per generation = more pixel budget per frame
- outlines stay sharper
- small or detail-sensitive sprites survive better
- motion drift is easier to catch

### 5. Generate from the already-open Gemini session

Best practice:

- keep Gemini open
- stay logged in on the correct account
- reuse the existing tab/window
- create a new chat from there

Do not depend on opening a fresh browser window every run unless you have to.

For Wevito, the stable lane became:

- attach to the already-open `Google Gemini - Google Chrome` window
- hit `New chat`
- paste the prepared upload pack
- paste the prepared prompt
- submit
- download the full-size result directly

### 6. Download directly, never through public/share links

This matters a lot.

```text
bad
└─ public/share-link image
   └─ often softer, resampled, or lower-fidelity

good
└─ Gemini "Download full size image"
   └─ best available fidelity
```

If a generation looks unexpectedly soft, confirm the retrieval path before you blame the prompt.

### 7. Import only the family you just edited

Never do a full destructive overwrite when you only improved one slice of motion.

Import rules:

- extract the board from the Gemini result pack
- import only the target family frames
- write them into a verified authored lane
- keep other animations intact

That lets you improve:

- `idle` without touching `walk`
- `walk_a` without touching `walk_b`
- `care` without touching `expression`

### 8. Propagate colors after the base motion is approved

Do not waste Gemini runs on every tint unless the tint changes anatomy or silhouette.

The practical order is:

```text
base motion first
└─ one entity + one key variant set in one canonical color
   └─ approve
      └─ propagate colors locally
```

This saved a lot of cost and made QA much easier.

### 9. Audit immediately

Every generation step should be followed by an audit step.

Audit layers that worked well:

- coverage report
- extracted board inspection
- contact sheet / preview board
- runtime screenshot
- targeted scenario launch

If you wait until the end of the whole roster, errors compound.

## Prompting Rules That Helped

These constraints mattered:

- preserve the exact uploaded character identity
- edit only the named frames
- leave all other frames unchanged
- keep the board layout and labels intact
- no blur
- no anti-aliasing
- no checkerboard residue
- no matte halos
- no clipped silhouettes

Project/domain-specific notes also mattered:

- creatures: preserve silhouette logic and readable anatomy
- birds: preserve filled chest/body mass
- snakes/ribbons/tentacles: avoid collapsing into one row-spanning strip
- upright-to-grounded characters: keep posture changes intentional across families
- props/devices: preserve mechanical hinge/segment consistency
- VFX: preserve readable energy flow and frame shape

## Common Failure Modes And Fixes

```text
failure: blur
cause  : too many frames in one generation
fix    : split into smaller families

failure: hollow body / missing insides
cause  : Gemini over-simplified bird torso fill
fix    : explicitly require filled chest/body mass

failure: clipped snouts / heads / tails
cause  : bad extraction or over-tight crop
fix    : improve crop logic and compare source vs runtime

failure: one stubborn pack hangs forever
cause  : Gemini-side stall
fix    : retry just that pack, not the whole entity set

failure: noisy automation state
cause  : long batch resumes poorly
fix    : run smaller batches and keep a clear resume queue
```

## Make The Kit Project-Agnostic

To reuse this on non-animal projects, structure everything in generic terms:

```text
entity
├─ canonical source board
├─ variant axes
├─ motion families
└─ import/audit rules
```

That means:

- use `entity_id` instead of hard-coding `species`
- describe `variant_axes` instead of assuming `age/gender/color`
- keep motion families configurable per project
- keep prompts domain-aware, not animal-only

This approach works for:

- characters
- enemies / monsters
- vehicles
- props / devices
- VFX sprites
- UI mascots
- decorative animated objects

## Minimum Reusable Toolkit

If you want to repeat this process on another project, build these pieces first:

```text
required
├─ source-of-truth doc
├─ asset manifest
├─ authoring-pack exporter
├─ focused family spec file
├─ Gemini handoff generator
├─ import script
├─ color propagation script
├─ coverage report
└─ screenshot/contact-sheet audit
```

Without those, you end up re-explaining the project to the tool instead of running a repeatable process.

## Suggested Per-Project Bootstrap

For a new project, create these up front:

1. `SPRITE_SOURCE_OF_TRUTH.md`
2. `MOTION_AUTHORING_ROADMAP.md`
3. `incoming_*_manifest.json`
4. `prepare_*_handoff.py`
5. `import_*_board.py`
6. `report_*_coverage.py`

That gives future-you and future-Codex enough structure to start correctly.

## How To Avoid Re-Explaining Yourself

The best way to make this repeatable is to keep three layers:

```text
layer 1: reusable playbook
└─ the method that worked across projects

layer 2: project contract
└─ what this specific project's sprites, motions, and rules are

layer 3: automation scripts
└─ the concrete commands that execute the method
```

If you only keep layer 3, the scripts become mysterious.
If you only keep layer 1, every project still needs too much explanation.
You want all three.

## Recommended Next Step

If you want this to be even more reusable than a doc, the next best step is to turn this workflow into a Codex skill or starter kit with:

- reusable folder layout
- manifest template
- prompt template
- handoff exporter
- import/coverage stubs

That would let a future project start from a prepared kit instead of rebuilding the process from scratch.
