# Perfect Loop Execution Plan

Updated: 2026-03-20

Implementation backlog:

- `PERFECT_LOOP_MILESTONE_BACKLOG.md`

```text
╔══ Target Product ═══════════════════════════════════════════════════════╗
║ one visible sprite workstation                                         ║
║                                                                        ║
║ plan -> generate -> animate -> paint -> review -> approve -> export    ║
║                      ▲                          │                       ║
║                      └──── AI assist / AI loop ─┘                       ║
╚═════════════════════════════════════════════════════════════════════════╝
```

## Product Goal

Build the Sprite Workflow App into a complete cross-platform workstation for sprite and animation production.

The app must support three equal creation modes:

- manual-first art creation
- AI-assisted iteration
- AI-loop automation with visible human override

The app must work for new projects and existing projects, and it must make all important work visible inside the app:

- browsing
- animation playback
- frame editing
- review notes
- requests
- AI progress
- approvals

## Definition Of A Perfect Loop

```text
╔══ Perfect Loop ═════════════════════════════════════════════════════════╗
║ 1. Project Setup                                                       ║
║    choose project or create one                                        ║
║                                                                        ║
║ 2. Plan                                                                ║
║    define species / variants / families / frame counts                 ║
║                                                                        ║
║ 3. Start Work                                                          ║
║    blank canvas OR template OR AI candidate                            ║
║                                                                        ║
║ 4. Animate                                                             ║
║    frozen edit frame + live playback + compare                         ║
║                                                                        ║
║ 5. Paint                                                               ║
║    visible in-app drawing and editing                                  ║
║                                                                        ║
║ 6. Review                                                              ║
║    notes, statuses, per-frame issues, approval gates                   ║
║                                                                        ║
║ 7. AI Loop                                                             ║
║    generate / repair / retry using notes and captures                  ║
║                                                                        ║
║ 8. Approve And Export                                                  ║
║    mark trusted, propagate, ship, or hand off                          ║
╚═════════════════════════════════════════════════════════════════════════╝
```

Success means a user can:

- start from nothing and draw a sprite set manually
- start from a runtime template and polish it
- start from an AI-generated candidate and repair it
- watch every important step happen in the app
- hand control back and forth between themselves and the AI without losing context

## Product Principles

- Manual art is a first-class path, not a fallback.
- AI is optional assistance, not the only content source.
- The app must always show what is being edited or reviewed.
- The app must preserve project structure and keep outputs organized.
- A user should be able to stop automation instantly and step in manually.
- Frame-level review is more important than coverage counts.
- New-project onboarding must be as strong as existing-project discovery.

## Execution Shape

```text
╔══ Delivery Order ═══════════════════════════════════════════════════════╗
║ Phase 1  Studio Core                                                   ║
║ Phase 2  Manual Art Workstation                                        ║
║ Phase 3  Review And Approval System                                    ║
║ Phase 4  AI Request And Candidate Loop                                 ║
║ Phase 5  Project Planning And Onboarding                               ║
║ Phase 6  Full Automation And Human Override                            ║
║ Phase 7  Export, Packaging, And Reuse                                  ║
╚═════════════════════════════════════════════════════════════════════════╝
```

## Phase 1: Studio Core

Goal:
- make `Review -> Animate -> Paint -> Save + Replay` completely reliable

Scope:
- stable workspace switching
- frozen edit frame + live playback behavior
- single-instance launch behavior
- visible playback state
- frame selection that pauses and stays paused
- safe editor load/save/revert flow

Deliverables:
- one clear Studio loop
- no surprise autoplay during editing
- no crashes from switching into Paint or Animate
- current frame always obvious

Acceptance criteria:
- a user can pick a row, choose a frame, enter Paint, save, and replay without confusion
- switching tabs does not lose frame context
- one app window remains open
- editor and viewer states remain in sync

## Phase 2: Manual Art Workstation

Goal:
- make the app good enough to draw and finish sprites without external art tools for common tasks

Scope:
- blank frame creation
- blank sequence creation
- layer stack improvements
- layer thumbnails
- merge up
- duplicate into new
- lock all but active
- lasso selection
- transform handles
- palette sets per project/species
- stronger color picker and wheel
- before/after blink compare

Deliverables:
- manual drawing from blank PNGs
- practical pixel editing and reshaping
- richer layer workflow
- strong palette workflow

Acceptance criteria:
- a user can create a new frame from nothing and finish it in-app
- a user can create a whole sequence skeleton without outside files
- common sprite polish tasks do not require leaving the app

## Phase 3: Review And Approval System

Goal:
- make visual quality review as strong as asset editing

Scope:
- per-frame notes
- per-frame statuses
- frame tags:
  - approved
  - needs_review
  - to_be_repaired
  - template_only
  - do_not_use
- review queues by frame, row, and family
- approval gates before export or propagation
- before/after history strip

Deliverables:
- row review and frame review
- approval workflow
- trusted-set filtering

Acceptance criteria:
- a user can flag a single bad frame without condemning the whole row
- the app can show exactly what is approved and what is still pending
- AI requests can target a specific frame or sequence directly from review data

## Phase 4: AI Request And Candidate Loop

Goal:
- make AI generation a visible, controllable, non-destructive part of the workflow

Scope:
- in-app request builder
- frame/row/sequence scoped requests
- prompt builder from:
  - notes
  - current frame
  - runtime template
  - captures
  - must preserve
  - must avoid
- candidate staging area before import
- candidate compare view
- AI result approval or rejection

Deliverables:
- request queue
- candidate tray
- visible handoff text
- visible accepted/rejected candidate history

Acceptance criteria:
- AI never silently overwrites trusted work
- users can see what request produced what candidate
- candidates can be reviewed before becoming live authored frames

## Phase 5: Project Planning And Onboarding

Goal:
- support both blank projects and discovered projects equally well

Scope:
- blank-project wizard
- discovered-asset adoption flow
- configurable species / age / gender / color axes
- family blueprint editor
- sequence/frame checklist generator
- setup prompts when no sprite data exists

Deliverables:
- `Create Project`
- `Adopt Existing Project`
- project checklist generation

Acceptance criteria:
- someone with no existing sprite files can start a structured project in the app
- someone with a partial sprite project can link folders and continue from discovered assets
- all created tasks map into the same review/request/editor flow

## Phase 6: Full Automation And Human Override

Goal:
- support AI loops while keeping manual override fast and safe

Scope:
- AI task queue
- visible progress panel:
  - current item
  - current frame/row
  - current prompt/candidate step
- `Stop All AI`
- `Resume Manual`
- `Return To AI`
- automatic capture bundles for AI review
- action logs inside the app
- automation checkpoint/resume

Deliverables:
- visible automation console
- interruption-safe AI workflow
- app-owned progress and state history

Acceptance criteria:
- a user can stop the AI immediately and continue manually without losing context
- the app always shows what the AI is currently working on
- completed AI work re-enters the same candidate/review flow as manual work

## Phase 7: Export, Packaging, And Reuse

Goal:
- make the app useful across projects and shippable as a real tool

Scope:
- sprite sheet export
- frame export
- project config export/import
- packaged Windows build
- packaged macOS build
- reusable sample configs
- documentation refresh

Deliverables:
- reusable project profiles
- export workflows
- packaged app releases

Acceptance criteria:
- a new project can onboard without Wevito-specific assumptions
- assets and metadata can be exported cleanly
- the app is usable by another person without repo tribal knowledge

## Cross-Cutting Work

These should be improved continuously, not delayed to one phase:

- stability and crash logging
- visible activity feed
- keyboard shortcuts
- UI clarity and reduced scrolling
- dark-theme polish
- hidden background workflow actions
- single-instance relaunch behavior
- test coverage for non-UI services and view model logic

## Milestone Order

```text
╔══ Milestones ════════════════════════════════════════════════╗
║ M1  reliable studio loop                                     ║
║ M2  blank-frame + manual art baseline                        ║
║ M3  per-frame review and approval                            ║
║ M4  candidate staging and request loop                       ║
║ M5  project creation/adoption                                ║
║ M6  visible AI automation with human override                ║
║ M7  exports, packaging, and cross-project release            ║
╚══════════════════════════════════════════════════════════════╝
```

## Immediate Next Slice

The highest-value next build slice is:

1. split Animate into:
   - frozen edit frame
   - live playback panel
2. add per-frame note/status records
3. add candidate staging records and compare cards

Why:
- it closes the biggest remaining gap in the visible AI/manual loop
- it makes frame-level judgment precise
- it creates the bridge from editing into AI iteration

## Risks

- too much feature work before hardening the Studio loop
- overfitting to Wevito-specific assumptions
- letting AI integration outrun manual-art quality
- hiding state in scripts instead of surfacing it in the app

## Guardrails

- do not add new AI automation until the current Studio loop stays stable
- do not accept AI outputs directly into trusted authored assets
- keep every major workflow visible in-app before calling it complete
- prefer frame-level controls over batch-only controls when quality matters

## Definition Of Done

The app is “perfect loop ready” when:

- a user can start a project from nothing
- a user can draw or generate sprites entirely through the app
- a user can review animation quality without running the game
- the AI can work visibly from requests, notes, and captures
- the user can interrupt AI and continue manually instantly
- approvals, exports, and project organization all happen in one tool
