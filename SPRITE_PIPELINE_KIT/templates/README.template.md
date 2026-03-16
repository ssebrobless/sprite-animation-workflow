# {{PROJECT_NAME}} Sprite Pipeline Starter

Updated: 2026-03-16

```text
╔════════════════════ Start Here ════════════════════╗
║ 1. fill out SPRITE_SOURCE_OF_TRUTH.md             ║
║ 2. replace the manifest placeholder entries       ║
║ 3. customize motion_families.json                 ║
║ 4. customize MOTION_AUTHORING_ROADMAP.md          ║
║ 5. lock Gemini prompt rules                       ║
║ 6. build handoff/export/import scripts            ║
╚════════════════════════════════════════════════════╝
```

Starter files:

- `docs/SPRITE_SOURCE_OF_TRUTH.md`
- `docs/MOTION_AUTHORING_ROADMAP.md`
- `docs/SPRITE_PIPELINE_CHECKLIST.md`
- `docs/GEMINI_PROMPT_RULES.md`
- `tools/incoming_sprite_manifest.json`
- `tools/motion_families.json`

Suggested first customization:

1. replace placeholder {{ENTITY_LABEL_PLURAL}} with the real roster
2. define your runtime frame contract
3. customize motion families for the kind of sprites you are building
4. keep direct-download-only as a hard rule if you use Gemini

Project roots seeded by the bootstrap:

- source art: `{{SOURCE_ROOT}}`
- runtime sprites: `{{RUNTIME_ROOT}}`
- verified authored sprites: `{{AUTHORED_ROOT}}`

Seed {{ENTITY_LABEL_PLURAL}}:

{{ENTITY_BULLETS}}
