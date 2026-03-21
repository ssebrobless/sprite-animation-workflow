# Next Wave Plan

Updated: 2026-03-20

```text
╔══ Next Wave Shape ═════════════════════════════════════════════════════════════╗
║ 1. Studio Feel                                                                ║
║    on-canvas transform polish, clearer edit targets, stronger first-use flow  ║
║                                                                              ║
║ 2. Paint Depth                                                                ║
║    stronger color workflow, richer layer tools, faster manual art creation    ║
║                                                                              ║
║ 3. Animation Polish Loop                                                      ║
║    edit while judging motion, compare better, keep playback visible           ║
║                                                                              ║
║ 4. Review Precision                                                           ║
║    frame blockers, approval confidence, export trust clarity                  ║
║                                                                              ║
║ 5. AI Loop Deepening                                                          ║
║    provider-agnostic requests, visible attempts, candidate lifecycle polish   ║
║                                                                              ║
║ 6. Cross-Project Alpha Hardening                                              ║
║    blank projects, imported projects, packaging, validation, portability      ║
╚══════════════════════════════════════════════════════════════════════════════╝
```

## Goal

Push the app from a strong manual-first sprite tool into a release-quality workstation for a new solo creator.

The next wave should improve all of these together:

- manual art quality and speed
- visible animation polish workflow
- AI request and candidate visibility
- cross-project onboarding and portability

The app should keep working for Wevito, but the target is broader: anyone should be able to use it to plan, draw, review, test, and export sprite animation work from one place.

## Success Criteria

A new user should be able to:

- start from a blank project
- adopt an existing project
- draw sprites manually in-app
- test animation quality without running the game
- use AI assistance without losing manual control
- keep all meaningful work visible inside the app

## Phase 1: Studio Feel

```text
Review -> Animate -> Paint -> Save -> Replay
```

Focus:

- on-canvas transform polish
- clearer active edit target emphasis
- clearer next-step guidance for a first-time user
- stronger separation between:
  - frozen edit frame
  - live playback reference

Implementation targets:

- visible selection box polish
- drag-style move/resize/rotate behavior from the canvas
- better “what is currently being edited” signaling
- more obvious “save, replay, review” guidance

Definition of done:

- a user can tell what frame is active at a glance
- a user can reshape selections from the work area, not just the sidebar
- the edit/playback loop feels obvious without explanation

## Phase 2: Paint Depth

Focus:

- stronger manual art controls
- faster layer and color iteration
- better support for drawing from scratch instead of only retouching

Implementation targets:

- true color wheel feel on top of the current color controls
- stronger project/species palette workflow
- layer polish:
  - drag reorder
  - duplicate into new
  - flatten copy
  - lock-all-but-active polish
  - clearer active-layer emphasis
- before/after compare polish in Paint

Definition of done:

- common sprite editing work does not require leaving the app
- a user can build a clean palette and stay consistent
- a user can construct or polish frames comfortably from scratch

## Phase 3: Animation Polish Loop

Focus:

- make motion judgment as strong as frame editing

Implementation targets:

- keep live playback visible while painting
- stronger previous/next/onion reference flow in Paint
- authored/runtime compare polish
- better before/after blink inside the same workspace

Definition of done:

- a user can edit while understanding how the frame reads in motion
- the app makes spacing, silhouette drift, and timing issues easy to catch

## Phase 4: Review Precision

Focus:

- stronger approval confidence
- better export trust decisions

Implementation targets:

- clearer frame blockers vs row blockers vs export blockers
- tighter trusted-ready summaries
- easier jump-to-problem navigation
- better visible approval and history signals

Definition of done:

- a user can see exactly why something is not export-ready
- trusted export feels explainable, not mysterious

## Phase 5: AI Loop Deepening

Focus:

- provider-agnostic visible automation
- cleaner request-to-candidate workflow

Implementation targets:

- clearer provider/task/target presentation
- richer prompt preview and attempt history
- tighter request -> candidate -> review/apply trail
- stronger manual takeover and return-to-AI flow

Definition of done:

- the user can always tell what AI is doing
- AI attempts are reviewable and reversible
- manual intervention never loses frame context or progress

## Phase 6: Cross-Project Alpha Hardening

Focus:

- make the app reliable beyond one linked game/project

Implementation targets:

- blank-project wizard polish
- discovered/imported project adoption hardening
- portability and validation report polish
- packaging expectations for Windows and macOS

Definition of done:

- a user can start cleanly with no existing files
- a user can adopt a partial external project with less confusion
- project kits and validation outputs feel ready for alpha usage

## Recommended Implementation Order

1. on-canvas transform polish
2. stronger color wheel and palette workflow
3. always-visible edit/playback compare loop
4. review and trusted export hardening
5. AI request/candidate console polish
6. cross-project alpha packaging and validation

## Immediate Next Slice

```text
selection box
-> drag handles
-> resize / rotate polish
-> stronger active edit target emphasis
-> clearer save + replay guidance
```

## Relation To Existing Docs

This plan is the short, execution-focused next step after:

- `PERFECT_LOOP_EXECUTION_PLAN.md`
- `PERFECT_LOOP_MILESTONE_BACKLOG.md`
- `PRODUCT_SPEC.md`
- `RELEASE_CHECKLIST.md`

Use this document when choosing the next implementation slices.
