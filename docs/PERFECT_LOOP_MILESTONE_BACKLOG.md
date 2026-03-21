# Perfect Loop Milestone Backlog

Updated: 2026-03-20

```text
╔══ Build Track ═════════════════════════════════════════════════════════╗
║ M1  Studio Core                                                      ║
║ M2  Manual Art Workstation                                           ║
║ M3  Frame Review + Approval                                          ║
║ M4  AI Requests + Candidate Staging                                  ║
║ M5  Project Onboarding + Discovery                                   ║
║ M6  Visible AI Automation + Human Override                           ║
║ M7  Export + Release + Cross-Project Hardening                       ║
╚═══════════════════════════════════════════════════════════════════════╝
```

This backlog translates the execution roadmap into concrete implementation work.

Primary roadmap:

- `PERFECT_LOOP_EXECUTION_PLAN.md`
- `RELEASE_CHECKLIST.md`

## Current Release-Wave Focus

```text
Release Hardening      -> package, validate, export, prove portability
Manual Art Deepening   -> stronger Paint/Studio workflow for solo creators
AI Loop Deepening      -> provider adapters, visible attempts, safer candidates
Cross-Project Validation -> blank projects + imported projects + readiness
```

This wave keeps the existing workspace structure and makes it release-ready for a new solo creator without breaking existing Wevito project data.

## Working Rules

- Do not add more automation complexity until the Studio loop is stable.
- Prefer visible in-app workflows over hidden script behavior.
- Keep manual art first-class in every milestone.
- Add frame-level review before expanding AI automation breadth.
- Treat AI output as staged candidates until approved.

## M1: Studio Core

Goal:
- make `Review -> Animate -> Paint -> Save + Replay` reliable, visible, and easy for a new user

### M1.1 Workspace Stability

- harden Studio tab switching
- keep one consistent selected row / family / sequence / frame
- preserve context when moving between Review, Animate, and Paint
- ensure save/revert/update always refresh Animate correctly

Acceptance:
- switching between Review, Animate, and Paint never loses the selected frame unexpectedly
- a saved frame always reappears correctly in Animate

### M1.2 Animate Clarity

- split Animate into:
  - frozen edit frame
  - live playback preview
- add explicit playback state
- keep frame stepping separate from playback
- improve current-frame emphasis

Acceptance:
- users can always tell which frame is being edited
- users can watch motion and inspect a still frame at the same time

### M1.3 Paint Reliability

- stabilize editor load/save/revert paths
- protect against tab-switch crashes
- ensure editor history and layers remain consistent
- verify canvas always reflects the current target frame

Acceptance:
- Paint no longer freezes or crashes during normal loop usage
- editor changes save back to the intended authored PNG

## M2: Manual Art Workstation

Goal:
- make the app good enough to start and finish hand-made sprite work without leaving the tool for common tasks

### M2.1 From-Scratch Creation

- blank frame creation
- blank sequence creation
- create missing frame at current slot
- create missing sequence skeleton from family definition

Acceptance:
- a user can start from no files and create a frame or sequence directly in-app

### M2.2 Layer Workstation

- layer thumbnails
- merge up
- duplicate into new
- flatten copy
- lock all but active
- rename and reorder polish

Acceptance:
- layers feel like a practical sprite workflow, not just a hidden stack

### M2.3 Advanced Editing

- lasso selection
- transform handles
- better move/scale/rotate interactions
- improved fill behavior
- stronger color picker and wheel
- project/species palette sets

Acceptance:
- ordinary sprite cleanup and redraw work can happen in-app without constant workaround buttons

## M3: Frame Review + Approval

Goal:
- move quality control from row-level only to precise frame-level review

### M3.1 Frame Notes

- per-frame note storage
- quick note entry from Animate and Paint
- issue tags:
  - off-model
  - stiff motion
  - broken outline
  - wrong silhouette
  - placeholder
  - anthropomorphic drift

Acceptance:
- a user can flag a single frame without marking the whole row bad

### M3.2 Frame Statuses

- per-frame statuses:
  - unreviewed
  - approved
  - needs_review
  - to_be_repaired
  - template_only
  - do_not_use
- row summary derived from frame states

Acceptance:
- the app can clearly show mixed-quality rows

### M3.3 Approval Gates

- trusted-set filters
- review queue by frame, row, and family
- before/after change history for edited frames

Acceptance:
- only approved frames or rows move into trusted implementation paths

## M4: AI Requests + Candidate Staging

Goal:
- make AI generation a visible, scoped, reviewable loop

### M4.1 Request Builder

- requests from:
  - selected frame
  - selected row
  - selected sequence
- prompt generation from:
  - notes
  - preserves
  - avoids
  - authored frame
  - runtime template
  - editor captures

Acceptance:
- requests are specific and reusable

### M4.2 Candidate Staging

- import AI results into a staging area
- candidate compare cards
- accept / reject / revise actions
- candidate history tied to request

Acceptance:
- AI results never overwrite trusted assets immediately

### M4.3 AI Visibility

- visible status of current AI task
- visible current target frame/row
- visible prompt preview and last output

Acceptance:
- a user can always see what the AI is currently doing

## M5: Project Onboarding + Discovery

Goal:
- support blank projects and discovered projects equally well

### M5.1 Blank Project Wizard

- project creation flow
- define species / axes / colors
- define families and frame counts
- generate starter checklist and folder expectations

Acceptance:
- a brand-new sprite project can start in the app without outside setup

### M5.2 Existing Project Adoption

- discover candidate sprite roots
- adopt found folders
- map existing files to project blueprint
- show missing and extra assets

Acceptance:
- partially existing projects can be brought into the app cleanly

### M5.3 Project Templates

- reusable presets
- project profile export/import
- starter family definitions

Acceptance:
- new projects do not require manual config editing unless desired

## M6: Visible AI Automation + Human Override

Goal:
- support longer-running AI loops while keeping manual override immediate

### M6.1 Automation Console

- current queue
- current task
- status log
- progress display
- current request and frame target

Acceptance:
- the automation state is fully visible in-app

### M6.2 Human Override

- stop all AI
- resume manual
- return to AI
- preserve request, frame, and candidate context when switching modes

Acceptance:
- a user can step in instantly without losing progress

### M6.3 AI Capture Loop

- automatic captures on save / request / candidate review
- AI reads app-generated capture bundles
- visible capture history

Acceptance:
- the AI has concrete visual context for repairs and generation

## M7: Export + Release + Cross-Project Hardening

Goal:
- make the app reusable and shippable for others

### M7.1 Export Workflows

- sprite sheet export
- frame export
- review/export summaries
- trusted set export

### M7.2 Packaging

- Windows packaged app
- macOS packaged app
- launcher cleanup and release docs

### M7.3 Cross-Project Validation

- validate on Wevito
- validate on one blank sample project
- validate on one imported external-style project

Acceptance:
- the app proves it is not Wevito-only

## Immediate Next Backlog

```text
╔══ Next Build Slice ════════════════════════════════════════════════════╗
║ 1. Animate split: frozen edit frame + live playback panel             ║
║ 2. Frame-level notes and statuses                                     ║
║ 3. Candidate staging records and compare cards                        ║
╚═══════════════════════════════════════════════════════════════════════╝
```

### Immediate Tasks

1. Create a dedicated live playback pane in Animate.
2. Add frame note data model and persistence.
3. Add frame status chips and queue filters.
4. Add staged-candidate model and local storage.
5. Add candidate compare UI with approve/reject.

## Completion Criteria

The backlog is complete when the app can:

- create sprites manually from blank canvases
- edit and animate them visibly in one loop
- generate or repair candidates through AI without hiding the process
- let a user interrupt AI and work manually instantly
- review, approve, and export trusted assets for any sprite project
