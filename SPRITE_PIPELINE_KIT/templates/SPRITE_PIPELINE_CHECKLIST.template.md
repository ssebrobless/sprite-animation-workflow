# {{PROJECT_NAME}} Sprite Pipeline Checklist

Updated: 2026-03-16

## Setup

- [ ] Fill out `docs/SPRITE_SOURCE_OF_TRUTH.md`
- [ ] Replace placeholder entries in `tools/incoming_sprite_manifest.json`
- [ ] Customize `tools/motion_families.json`
- [ ] Lock runtime file naming and frame-family contract
- [ ] Decide which families require split Gemini packs

## Export And Generation

- [ ] Build focused authoring/handoff pack exporter
- [ ] Build family-aware import path
- [ ] Build color/variant propagation path
- [ ] Reuse the already-open Gemini tab by default
- [ ] Enforce direct full-size Gemini downloads only

## Validation

- [ ] Add family-aware coverage reporting
- [ ] Add extracted-board visual inspection
- [ ] Add preview/contact-sheet generation
- [ ] Add runtime screenshot validation
- [ ] Add an explicit resume queue for interrupted {{ENTITY_LABEL_PLURAL}}

## Production Readiness

- [ ] Complete priority motion families across all required {{ENTITY_LABEL_PLURAL}} and variant axes
- [ ] Complete secondary motion families across all required {{ENTITY_LABEL_PLURAL}} and variant axes
- [ ] Complete special-case or interaction families across all required {{ENTITY_LABEL_PLURAL}} and variant axes
- [ ] Approve color/variant propagation across all required outputs
- [ ] Archive evidence paths for the final verified set
