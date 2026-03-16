#!/usr/bin/env python3
"""
Bootstrap a reusable AI sprite/animation workflow starter kit into another project.
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
TEMPLATE_ROOT = ROOT / "SPRITE_PIPELINE_KIT" / "templates"

GENERIC_MOTION_FAMILIES = {
    "locomotion_idle": {
        "description": "idle-only refinement for sharpness-sensitive sprites",
        "frames": ["idle_00", "idle_01", "idle_02", "idle_03"],
    },
    "locomotion_walk_a": {
        "description": "first half of a locomotion cycle",
        "frames": ["walk_00", "walk_01", "walk_02"],
    },
    "locomotion_walk_b": {
        "description": "second half of a locomotion cycle",
        "frames": ["walk_03", "walk_04", "walk_05"],
    },
    "interaction": {
        "description": "common interaction poses",
        "frames": ["interact_00", "interact_01", "interact_02"],
    },
    "expression": {
        "description": "reaction or emotion poses",
        "frames": ["react_00", "react_01", "react_02"],
    },
}

PRESETS: dict[str, dict[str, object]] = {
    "generic": {
        "entity_label_singular": "entity",
        "entity_label_plural": "entities",
        "entities": ["entity_a", "entity_b", "entity_c"],
        "variant_axes": ["state", "faction", "palette"],
        "motion_families": GENERIC_MOTION_FAMILIES,
        "domain_guidance": [
            "Preserve silhouette, proportions, and readable motion arcs.",
            "Split dense families when sharpness drops.",
            "Keep direct-download-only as a hard rule if you use Gemini.",
        ],
    },
    "character": {
        "entity_label_singular": "character",
        "entity_label_plural": "characters",
        "entities": ["hero", "enemy", "npc"],
        "variant_axes": ["state", "faction", "palette"],
        "motion_families": {
            "idle": {"description": "idle refinement", "frames": ["idle_00", "idle_01", "idle_02", "idle_03"]},
            "move_a": {"description": "first half of move cycle", "frames": ["move_00", "move_01", "move_02"]},
            "move_b": {"description": "second half of move cycle", "frames": ["move_03", "move_04", "move_05"]},
            "attack": {"description": "attack motion", "frames": ["attack_00", "attack_01", "attack_02"]},
            "hurt": {"description": "hurt / hit reaction", "frames": ["hurt_00", "hurt_01"]},
        },
        "domain_guidance": [
            "Keep anatomy and balance readable at the target game scale.",
            "Preserve weapon, hair, accessory, or cape logic across frames.",
            "Ground contact and attack anticipation matter more than frame count.",
        ],
    },
    "creature": {
        "entity_label_singular": "creature",
        "entity_label_plural": "creatures",
        "entities": ["wolf", "serpent", "bird"],
        "variant_axes": ["age_stage", "gender", "palette"],
        "motion_families": {
            "locomotion_idle": GENERIC_MOTION_FAMILIES["locomotion_idle"],
            "locomotion_walk_a": GENERIC_MOTION_FAMILIES["locomotion_walk_a"],
            "locomotion_walk_b": GENERIC_MOTION_FAMILIES["locomotion_walk_b"],
            "care": {"description": "eat, drink, sleep, or rest", "frames": ["care_00", "care_01", "care_02"]},
            "expression": GENERIC_MOTION_FAMILIES["expression"],
        },
        "domain_guidance": [
            "Preserve species-specific silhouette and body mass.",
            "Use motion notes per archetype: quadruped, avian, reptile, amphibian, etc.",
            "Treat shape changes as intentional motion, not random distortion.",
        ],
    },
    "prop": {
        "entity_label_singular": "prop",
        "entity_label_plural": "props",
        "entities": ["door", "console", "chest"],
        "variant_axes": ["state", "theme", "palette"],
        "motion_families": {
            "idle": {"description": "static or ambient idle state", "frames": ["idle_00"]},
            "open_close_a": {"description": "first half of open/close cycle", "frames": ["open_00", "open_01", "open_02"]},
            "open_close_b": {"description": "second half of open/close cycle", "frames": ["open_03", "open_04", "open_05"]},
            "active_loop": {"description": "powered/active loop", "frames": ["active_00", "active_01", "active_02"]},
            "break": {"description": "break or failure state", "frames": ["break_00", "break_01"]},
        },
        "domain_guidance": [
            "Preserve construction details and hinge/segment logic.",
            "Mechanical motion should stay consistent across all frames.",
            "Prioritize readability of the state change over extra micro-motion.",
        ],
    },
    "vfx": {
        "entity_label_singular": "effect",
        "entity_label_plural": "effects",
        "entities": ["impact", "spark", "smoke"],
        "variant_axes": ["intensity", "element", "palette"],
        "motion_families": {
            "spawn": {"description": "spawn or ignition", "frames": ["spawn_00", "spawn_01", "spawn_02"]},
            "loop_a": {"description": "first half of loop", "frames": ["loop_00", "loop_01", "loop_02"]},
            "loop_b": {"description": "second half of loop", "frames": ["loop_03", "loop_04", "loop_05"]},
            "dissipate": {"description": "fade or dissipate", "frames": ["end_00", "end_01", "end_02"]},
        },
        "domain_guidance": [
            "Preserve readable frame shape and energy flow.",
            "Avoid muddy edges and low-contrast frame silhouettes.",
            "Split long loops if the effect loses clarity in generation.",
        ],
    },
    "ui_mascot": {
        "entity_label_singular": "mascot",
        "entity_label_plural": "mascots",
        "entities": ["helper", "companion", "badge_bot"],
        "variant_axes": ["mood", "theme", "palette"],
        "motion_families": {
            "idle": {"description": "idle animation", "frames": ["idle_00", "idle_01", "idle_02"]},
            "gesture": {"description": "wave, point, or gesture", "frames": ["gesture_00", "gesture_01", "gesture_02"]},
            "react": {"description": "small reaction loop", "frames": ["react_00", "react_01", "react_02"]},
            "appear": {"description": "appear or enter", "frames": ["appear_00", "appear_01", "appear_02"]},
        },
        "domain_guidance": [
            "Preserve recognizability at UI display sizes.",
            "Keep motion clear without cluttering the silhouette.",
            "Bias toward readable expression and simple motion arcs.",
        ],
    },
}


def render_template(template_text: str, values: dict[str, str]) -> str:
    rendered = template_text
    for key, value in values.items():
        rendered = rendered.replace(f"{{{{{key}}}}}", value)
    return rendered


def write_rendered_template(template_name: str, output_path: Path, values: dict[str, str]) -> None:
    template_path = TEMPLATE_ROOT / template_name
    template_text = template_path.read_text(encoding="utf-8")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(render_template(template_text, values), encoding="utf-8")


def bullet_lines(values: list[str]) -> str:
    return "\n".join(f"- `{value}`" for value in values)


def text_bullet_lines(values: list[str]) -> str:
    return "\n".join(f"- {value}" for value in values)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--target-root", type=Path, required=True, help="Project root to scaffold.")
    parser.add_argument("--project-name", default="", help="Human-readable project name. Defaults to target folder name.")
    parser.add_argument("--source-root", default="incoming_sprites", help="Canonical source art folder.")
    parser.add_argument("--runtime-root", default="sprites_runtime", help="Generated runtime sprite folder.")
    parser.add_argument("--authored-root", default="sprites_authored_verified", help="Verified authored sprite folder.")
    parser.add_argument("--preset", choices=sorted(PRESETS.keys()), default="generic", help="Starter preset to seed the scaffold.")
    parser.add_argument(
        "--entities",
        nargs="*",
        default=None,
        help="Optional starter entity ids to seed into the manifest template.",
    )
    parser.add_argument(
        "--species",
        nargs="*",
        default=None,
        help="Backward-compatible alias for --entities.",
    )
    parser.add_argument("--entity-label-singular", default=None, help="Generic singular label for sprite subjects.")
    parser.add_argument("--entity-label-plural", default=None, help="Generic plural label for sprite subjects.")
    parser.add_argument(
        "--variant-axes",
        nargs="*",
        default=None,
        help="Variant axes to seed into the generic manifest template.",
    )
    args = parser.parse_args()

    preset = PRESETS[args.preset]
    target_root = args.target_root.resolve()
    project_name = args.project_name or target_root.name
    source_root = args.source_root
    runtime_root = args.runtime_root
    authored_root = args.authored_root
    entity_ids = args.species if args.species else args.entities
    if not entity_ids:
        entity_ids = list(preset["entities"])  # type: ignore[arg-type]
    entity_label_singular = args.entity_label_singular or str(preset["entity_label_singular"])
    entity_label_plural = args.entity_label_plural or str(preset["entity_label_plural"])
    variant_axes = args.variant_axes or list(preset["variant_axes"])  # type: ignore[arg-type]
    motion_families = preset["motion_families"]
    domain_guidance = list(preset["domain_guidance"])  # type: ignore[arg-type]
    entity_json = json.dumps(entity_ids, indent=2)
    entity_bullets = bullet_lines(entity_ids)
    variant_axes_json = json.dumps(variant_axes, indent=2)
    variant_axes_bullets = bullet_lines(variant_axes)

    values = {
        "PROJECT_NAME": project_name,
        "TARGET_ROOT": str(target_root),
        "SOURCE_ROOT": source_root,
        "RUNTIME_ROOT": runtime_root,
        "AUTHORED_ROOT": authored_root,
        "ENTITY_LABEL_SINGULAR": entity_label_singular,
        "ENTITY_LABEL_PLURAL": entity_label_plural,
        "ENTITY_LABEL_SINGULAR_TITLE": entity_label_singular[:1].upper() + entity_label_singular[1:],
        "ENTITY_LABEL_PLURAL_TITLE": entity_label_plural[:1].upper() + entity_label_plural[1:],
        "ENTITY_IDS_JSON": entity_json,
        "ENTITY_BULLETS": entity_bullets,
        "VARIANT_AXES_JSON": variant_axes_json,
        "VARIANT_AXES_BULLETS": variant_axes_bullets,
        "PRESET_NAME": args.preset,
        "MOTION_FAMILIES_JSON": json.dumps(motion_families, indent=2),
        "DOMAIN_GUIDANCE_BULLETS": text_bullet_lines(domain_guidance),
    }

    # Folder skeleton
    for relative_dir in [
        "docs",
        "tools",
        source_root,
        runtime_root,
        authored_root,
        "artifacts",
        "artifacts/coverage",
        "artifacts/handoffs",
        "artifacts/previews",
        "artifacts/screenshots",
    ]:
        (target_root / relative_dir).mkdir(parents=True, exist_ok=True)

    # Seed docs/templates
    write_rendered_template(
        "SPRITE_SOURCE_OF_TRUTH.template.md",
        target_root / "docs" / "SPRITE_SOURCE_OF_TRUTH.md",
        values,
    )
    write_rendered_template(
        "MOTION_AUTHORING_ROADMAP.template.md",
        target_root / "docs" / "MOTION_AUTHORING_ROADMAP.md",
        values,
    )
    write_rendered_template(
        "SPRITE_PIPELINE_CHECKLIST.template.md",
        target_root / "docs" / "SPRITE_PIPELINE_CHECKLIST.md",
        values,
    )
    write_rendered_template(
        "GEMINI_PROMPT_RULES.template.md",
        target_root / "docs" / "GEMINI_PROMPT_RULES.md",
        values,
    )
    write_rendered_template(
        "incoming_sprite_manifest.template.json",
        target_root / "tools" / "incoming_sprite_manifest.json",
        values,
    )
    write_rendered_template(
        "motion_families.template.json",
        target_root / "tools" / "motion_families.json",
        values,
    )
    write_rendered_template(
        "README.template.md",
        target_root / "docs" / "SPRITE_PIPELINE_START_HERE.md",
        values,
    )

    summary = {
        "projectName": project_name,
        "targetRoot": str(target_root),
        "sourceRoot": source_root,
        "runtimeRoot": runtime_root,
        "authoredRoot": authored_root,
        "preset": args.preset,
        "entityLabelSingular": entity_label_singular,
        "entityLabelPlural": entity_label_plural,
        "seedEntities": entity_ids,
        "variantAxes": variant_axes,
        "files": [
            str(target_root / "docs" / "SPRITE_SOURCE_OF_TRUTH.md"),
            str(target_root / "docs" / "MOTION_AUTHORING_ROADMAP.md"),
            str(target_root / "docs" / "SPRITE_PIPELINE_CHECKLIST.md"),
            str(target_root / "docs" / "GEMINI_PROMPT_RULES.md"),
            str(target_root / "docs" / "SPRITE_PIPELINE_START_HERE.md"),
            str(target_root / "tools" / "incoming_sprite_manifest.json"),
            str(target_root / "tools" / "motion_families.json"),
        ],
    }
    summary_path = target_root / "artifacts" / "sprite-pipeline-bootstrap-summary.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")

    print(summary_path)


if __name__ == "__main__":
    main()
