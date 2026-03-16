# {{PROJECT_NAME}} Motion Authoring Roadmap

Updated: 2026-03-16

```text
╔════════════════════ Motion Authoring Flow ════════════════════╗
║ source boards                                                 ║
║   ▼                                                           ║
║ focused handoff packs                                         ║
║   ▼                                                           ║
║ Gemini direct full-size download                              ║
║   ▼                                                           ║
║ family import into {{AUTHORED_ROOT}}                          ║
║   ▼                                                           ║
║ local color/variant propagation                               ║
║   ▼                                                           ║
║ coverage + previews + runtime screenshots                     ║
╚════════════════════════════════════════════════════════════════╝
```

## Motion Families

Preset:

- `{{PRESET_NAME}}`

See `tools/motion_families.json` and customize it for your project.

## Why Split Packs

- keeps Gemini sharper
- reduces silhouette drift
- improves small-frame readability
- makes retries cheaper

## Recommended Order

1. hardest blur-prone or shape-sensitive {{ENTITY_LABEL_PLURAL}} first
2. highest-value motion family before secondary families
3. one {{ENTITY_LABEL_SINGULAR}} fully through base motion before moving to the next

## Gemini Rules

- reuse the already-open logged-in Gemini tab
- create a new chat from there
- upload a single prepared pack image
- use direct full-size download only
- import immediately after generation

## Domain Guidance

{{DOMAIN_GUIDANCE_BULLETS}}

## Seed {{ENTITY_LABEL_PLURAL_TITLE}}

{{ENTITY_BULLETS}}
