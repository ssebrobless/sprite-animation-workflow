# {{PROJECT_NAME}} Sprite Source Of Truth

Updated: 2026-03-16

```text
╔════════════════════ Source Contract ════════════════════╗
║ canonical source art  │ {{SOURCE_ROOT}}                ║
║ runtime output        │ {{RUNTIME_ROOT}}               ║
║ verified authored     │ {{AUTHORED_ROOT}}              ║
║ project root          │ {{TARGET_ROOT}}                ║
╚═════════════════════════════════════════════════════════╝
```

## Source Of Truth

- Canonical source art lives in `{{SOURCE_ROOT}}`
- Generated runtime output lives in `{{RUNTIME_ROOT}}`
- Verified authored/imported overrides live in `{{AUTHORED_ROOT}}`

## Required Decisions

- What is the canonical input board format?
- Which files are true source and which are previews/exports?
- Which variant axes are real source boards and which are derived outputs?
- Which animations exist in the runtime contract?
- Which frame sizes are fixed and which can vary by family?

## {{ENTITY_LABEL_PLURAL_TITLE}} Roster

{{ENTITY_BULLETS}}

## Variant Axes

{{VARIANT_AXES_BULLETS}}

## Runtime Contract

Fill this in before generation starts:

- frame families:
  - defined in `tools/motion_families.json`
- output file naming:
  - `<animation>_<nn>.png`
- color/variant policy:
  - base motion first
  - derived variants after approval when possible

## Non-Negotiables

- Preserve the canonical look.
- AI edits should refine, not redesign the source {{ENTITY_LABEL_SINGULAR}}.
- Direct-download assets only if using Gemini.
- Validate every imported family before moving on.
