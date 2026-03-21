# Product Spec

Updated: 2026-03-20

```text
╔══ Product Shape ═════════════════════════════════════════════════════╗
║ Plan                                                                ║
║   project wizard / discovery / checklist                            ║
║      ▼                                                              ║
║ Review                                                              ║
║   browse rows / inspect coverage / queue issues                     ║
║      ▼                                                              ║
║ Studio                                                              ║
║   animate / compare / paint / save / replay                         ║
║      ▼                                                              ║
║ Stage                                                               ║
║   candidates / requests / AI handoff / manual override              ║
║      ▼                                                              ║
║ Approve + Export                                                    ║
║   trusted sets / reports / project kits / validation sandboxes      ║
╚══════════════════════════════════════════════════════════════════════╝
```

## Product Direction

The Sprite Workflow App is a reusable cross-platform sprite and animation workstation.

It is designed to work for:

- manual-only sprite projects
- AI-assisted sprite projects
- mixed workflows where a user and AI take turns iterating on the same assets

Wevito is only the first configured profile, not the product boundary.

## Core Principles

- manual art comes first
- everything important is visible in-app
- AI output is staged before approval
- users can interrupt automation instantly without losing context
- planning, editing, review, and export all happen in one workspace

## Current Functional Areas

### Planning

- blank-project blueprint editing
- existing-project asset adoption
- planning diagnostics
- unmapped discovered asset surfacing
- starter workspace and asset skeleton generation
- validation report generation
- validation sandbox creation
- reusable project kit export

### Review

- filterable row browser
- row-level and frame-level review state
- issue tags
- repair queue
- trusted export blockers

### Studio

- frozen edit target
- live playback monitor
- authored/runtime compare
- onion skin
- frame navigation and frame helpers
- visible handoff between review, animation, and paint

### Paint

- brush, erase, dropper, fill
- selection and lasso
- move, scale, rotate, flip
- line, rectangle, ellipse
- drag painting
- undo/redo
- layers with locking, merge, flatten, thumbnails, and template/reference usage
- swatches and reusable project palettes
- frame history and restore

### Requests And Candidates

- request drafting from rows, frames, and candidates
- staged candidates from authored/runtime/editor
- candidate compare, apply, reject, restore
- visible AI activity history
- linked candidates inside automation view

### Automation

- hidden workflow process runner for app-owned terminal actions
- visible AI/manual control mode
- queue, start, pause, resume
- stop all automation
- manual override without losing frame/editor context

## Ideal User Loop

```text
select project
-> discover or plan assets
-> review a row
-> animate and pause on a frame
-> paint edits visibly
-> save and replay
-> stage candidate or create request
-> approve or export
```

## Definition Of Done

The product is successful when a new user can:

- start a blank sprite project in-app
- import or adopt an existing sprite project in-app
- draw sprites manually without relying on external editors for common work
- test animations without running the game
- use AI assistance visibly without losing manual control
- review and export trusted results for another project or teammate

## Roadmap

Primary execution sources:

- [PERFECT_LOOP_EXECUTION_PLAN.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/PERFECT_LOOP_EXECUTION_PLAN.md)
- [PERFECT_LOOP_MILESTONE_BACKLOG.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/PERFECT_LOOP_MILESTONE_BACKLOG.md)
- [NEXT_WAVE_PLAN.md](C:/Users/fishe/Documents/projects/sprite-workflow-app/docs/NEXT_WAVE_PLAN.md)
