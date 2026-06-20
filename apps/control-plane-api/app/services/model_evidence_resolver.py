"""Deterministic resolver that creates RequirementEvidence candidates from Revit model elements.

Workflow:
  Element rows (Revit export) → scored against Requirements → RequirementEvidence candidates
  Candidates stay at evidence_status="needs_review" + review_status="candidate" until
  a reviewer explicitly accepts or rejects them.  Candidate evidence never counts as covered.
"""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.models import (
    Element,
    Export,
    Project,
    Requirement,
    RequirementEvidence,
)

# ---------------------------------------------------------------------------
# Intent patterns
# ---------------------------------------------------------------------------

_INTENT_PATTERNS: list[dict[str, Any]] = [
    # Most specific patterns checked first to avoid false matches on common words.
    {
        "intent_type": "technology",
        # "data" and "device" are checked here first so they don't fall into electrical_fixture
        "keywords": ["telecom", "data", "communications", "security", "fire alarm", "nurse call", "telephone"],
        "expected_categories": {
            "Communication Devices",
            "Data Devices",
            "Fire Alarm Devices",
            "Security Devices",
            "Nurse Call Devices",
            "Telephone Devices",
        },
        "required_params": ["Mark", "System Name"],
    },
    {
        "intent_type": "mechanical",
        "keywords": ["mechanical", "ahu", "rtu", "fan", "vav", "equipment", "hvac"],
        "expected_categories": {"Mechanical Equipment"},
        "required_params": ["Mark", "Type Mark"],
    },
    {
        "intent_type": "lighting",
        # Checked before plumbing: "lighting/light" in text → match here before "fixture" in plumbing fires
        # "fixture" omitted — too generic; present in "plumbing fixtures" text
        "keywords": ["lighting", "light", "luminaire", "lamp"],
        "expected_categories": {"Lighting Fixtures"},
        "required_params": ["Panel", "Circuit Number"],
    },
    {
        "intent_type": "plumbing",
        "keywords": ["plumbing", "pipe", "fixture", "water", "waste", "gas", "drain"],
        "expected_categories": {
            "Plumbing Fixtures",
            "Pipes",
            "Pipe Fittings",
            "Pipe Accessories",
        },
        "required_params": ["Mark", "Type Mark"],
    },
    {
        "intent_type": "electrical_panel",
        # Plain "panel" removed — it appears in fixture requirements as a parameter name
        "keywords": ["panelboard", "switchboard", "distribution", "mdp", "transformer"],
        "expected_categories": {"Electrical Equipment"},
        "required_params": ["Supply From"],
    },
    {
        "intent_type": "electrical_fixture",
        # "device" removed — too generic; matched technology requirements
        "keywords": ["receptacle", "outlet", "power", "plug"],
        "expected_categories": {"Electrical Fixtures"},
        "required_params": ["Panel", "Circuit Number"],
    },
]

# Threshold below which no candidate is created at all
_SCORE_THRESHOLD_LOW = 0.40
# Threshold above which the candidate is created with no extra gate
_SCORE_THRESHOLD_HIGH = 0.65


# ---------------------------------------------------------------------------
# Public helpers
# ---------------------------------------------------------------------------

def param_value(element: Element, *names: str) -> str | None:
    """Return the first non-empty string value found in instance or type parameters."""
    for store in (element.instance_parameters or {}, element.type_parameters or {}):
        for name in names:
            raw = store.get(name)
            if raw is not None:
                val = str(raw).strip()
                if val:
                    return val
    return None


def element_text(element: Element) -> str:
    """Concatenate name, family, and type into a single search string."""
    parts = [element.name, element.family, element.type, element.category]
    return " ".join(p for p in parts if p).lower()


# ---------------------------------------------------------------------------
# Core scoring
# ---------------------------------------------------------------------------

def classify_requirement_intent(requirement: Requirement) -> dict[str, Any]:
    """Classify a requirement into a known intent pattern.

    Returns the matching pattern dict (or a generic fallback) plus a
    `source_fields` list indicating which requirement fields contributed.
    """
    text = " ".join(
        part
        for part in [
            requirement.discipline or "",
            requirement.category or "",
            requirement.requirement_text or "",
        ]
    ).lower()

    for pattern in _INTENT_PATTERNS:
        # Check if any keyword appears in the combined text
        if any(kw in text for kw in pattern["keywords"]):
            return {**pattern, "source_fields": ["discipline", "category", "requirement_text"]}

    return {
        "intent_type": "generic_model",
        "keywords": [],
        "expected_categories": set(),
        "required_params": [],
        "source_fields": [],
    }


def score_element_against_requirement(
    requirement: Requirement,
    element: Element,
    intent: dict[str, Any],
) -> tuple[float, dict[str, Any]]:
    """Return (score, scoring_breakdown).

    Deterministic scoring components:
    - Category compatible        : +0.35
    - Keyword in element text    : +0.15 each, capped at 3 (0.45 max)
    - Required param present     : +0.15 each
    - Requirement category found : +0.10
    - Level exists               : +0.05
    """
    score = 0.0
    breakdown: dict[str, Any] = {
        "category_match": False,
        "matched_keywords": [],
        "matched_params": [],
        "category_in_text": False,
        "level_bonus": False,
    }

    elem_text = element_text(element)
    expected_categories: set[str] = intent.get("expected_categories", set())
    keywords: list[str] = intent.get("keywords", [])
    required_params: list[str] = intent.get("required_params", [])
    req_category = (requirement.category or "").lower()

    # +0.35 – category match
    if element.category and expected_categories and element.category in expected_categories:
        score += 0.35
        breakdown["category_match"] = True

    # +0.15 per keyword, cap at 3
    matched_kw: list[str] = []
    for kw in keywords:
        if kw in elem_text:
            matched_kw.append(kw)
    capped = matched_kw[:3]
    score += 0.15 * len(capped)
    breakdown["matched_keywords"] = capped

    # +0.15 per required param present
    matched_params: list[str] = []
    for param_name in required_params:
        if param_value(element, param_name) is not None:
            matched_params.append(param_name)
    score += 0.15 * len(matched_params)
    breakdown["matched_params"] = matched_params

    # +0.10 – requirement category appears in element text
    if req_category and req_category in elem_text:
        score += 0.10
        breakdown["category_in_text"] = True

    # +0.05 – element has a level
    if element.level:
        score += 0.05
        breakdown["level_bonus"] = True

    return round(min(score, 1.0), 4), breakdown


def find_requirement_model_candidates(
    requirement: Requirement,
    elements: list[Element],
    max_candidates: int = 5,
) -> list[dict[str, Any]]:
    """Score elements against a requirement and return top candidates.

    Only returns elements whose score meets the minimum threshold.
    """
    intent = classify_requirement_intent(requirement)
    scored: list[tuple[float, dict[str, Any], Element]] = []

    for element in elements:
        score, breakdown = score_element_against_requirement(requirement, element, intent)
        if score >= _SCORE_THRESHOLD_LOW:
            scored.append((score, breakdown, element))

    scored.sort(key=lambda t: t[0], reverse=True)

    candidates: list[dict[str, Any]] = []
    for score, breakdown, element in scored[:max_candidates]:
        candidates.append(
            {
                "element": element,
                "score": score,
                "breakdown": breakdown,
                "intent": intent,
                "matched_params": {
                    p: param_value(element, p)
                    for p in intent.get("required_params", [])
                    if param_value(element, p)
                },
            }
        )
    return candidates


# ---------------------------------------------------------------------------
# Orchestrator
# ---------------------------------------------------------------------------

def resolve_project_model_evidence(
    db: Session,
    project_id: int,
    max_candidates_per_requirement: int = 5,
) -> dict[str, Any]:
    """Bridge Revit element rows to owner requirement evidence candidates.

    Creates or updates RequirementEvidence rows with:
      evidence_type    = "model"
      evidence_status  = "needs_review"
      review_status    = "candidate"  (in metadata_json)
      source_ref       = "model:<element.unique_id>"

    Returns a summary dict.  Does NOT auto-approve or create compliance rows.
    """
    warnings: list[str] = []

    # ── Load project ───────────────────────────────────────────────────────
    project = db.get(Project, project_id)
    if project is None:
        return {
            "project_id": project_id,
            "state": "project_not_found",
            "error": f"Project {project_id} not found",
            "latest_export_id": None,
            "requirements_checked": 0,
            "requirements_with_candidates": 0,
            "candidate_evidence_created": 0,
            "candidate_evidence_updated": 0,
            "requirements_missing_model_evidence": 0,
            "review_required": 0,
            "warnings": [f"Project {project_id} not found"],
        }

    if project.client_id is None:
        return {
            "project_id": project_id,
            "state": "no_client_linked",
            "error": "Project has no client linked; cannot resolve requirements",
            "latest_export_id": None,
            "requirements_checked": 0,
            "requirements_with_candidates": 0,
            "candidate_evidence_created": 0,
            "candidate_evidence_updated": 0,
            "requirements_missing_model_evidence": 0,
            "review_required": 0,
            "warnings": ["Project has no client_id; bind the project to a client first"],
        }

    # ── Load latest completed export ────────────────────────────────────────
    latest_export: Export | None = db.execute(
        select(Export)
        .where(Export.project_id == project_id, Export.status == "completed")
        .order_by(Export.completed_at.desc().nulls_last(), Export.id.desc())
        .limit(1)
    ).scalar_one_or_none()

    if latest_export is None:
        return {
            "project_id": project_id,
            "state": "no_completed_export",
            "error": "No completed Revit export found for this project",
            "latest_export_id": None,
            "requirements_checked": 0,
            "requirements_with_candidates": 0,
            "candidate_evidence_created": 0,
            "candidate_evidence_updated": 0,
            "requirements_missing_model_evidence": 0,
            "review_required": 0,
            "warnings": ["Run a Revit export and complete ingestion before resolving evidence"],
        }

    # ── Load elements for latest export ────────────────────────────────────
    elements: list[Element] = list(
        db.execute(
            select(Element).where(Element.export_id == latest_export.id)
        ).scalars()
    )

    if not elements:
        warnings.append(f"Export {latest_export.id} has no elements; nothing to match against")

    # ── Load active+actionable requirements for client ──────────────────────
    requirements: list[Requirement] = list(
        db.execute(
            select(Requirement)
            .where(
                Requirement.client_id == project.client_id,
                Requirement.is_active.is_(True),
                Requirement.is_actionable.is_(True),
            )
            .order_by(Requirement.discipline, Requirement.id)
        ).scalars()
    )

    # ── Process each requirement ────────────────────────────────────────────
    now = datetime.now(timezone.utc)
    created = 0
    updated = 0
    with_candidates: set[int] = set()

    for requirement in requirements:
        candidates = find_requirement_model_candidates(
            requirement, elements, max_candidates=max_candidates_per_requirement
        )

        for candidate in candidates:
            element: Element = candidate["element"]
            score: float = candidate["score"]
            breakdown: dict[str, Any] = candidate["breakdown"]
            intent: dict[str, Any] = candidate["intent"]

            source_ref = f"model:{element.unique_id}"

            # Build metadata
            meta: dict[str, Any] = {
                "review_status": "candidate",
                "source_label": _build_source_label(element),
                "export_id": latest_export.id,
                "model_id": element.model_id,
                "element_id": element.element_id,
                "element_unique_id": element.unique_id,
                "category": element.category,
                "name": element.name,
                "family": element.family,
                "type": element.type,
                "level": element.level,
                "matched_intent": intent.get("intent_type"),
                "matched_fields": breakdown.get("matched_keywords", []),
                "matched_params": candidate.get("matched_params", {}),
                "scoring_breakdown": breakdown,
            }

            # Upsert: find existing by (project_id, requirement_id, source_ref)
            existing: RequirementEvidence | None = db.execute(
                select(RequirementEvidence).where(
                    RequirementEvidence.project_id == project_id,
                    RequirementEvidence.requirement_id == requirement.id,
                    RequirementEvidence.source_ref == source_ref,
                )
            ).scalar_one_or_none()

            if existing is None:
                # Only SQLite needs manual id; PostgreSQL auto-increments
                bind = db.get_bind()
                dialect_name = getattr(bind.dialect, "name", "") if bind is not None else ""
                if dialect_name != "postgresql":
                    current_max = db.execute(
                        select(func.coalesce(func.max(RequirementEvidence.id), 0))
                    ).scalar_one()
                    new_id: int | None = int(current_max) + 1
                else:
                    new_id = None

                new_evidence = RequirementEvidence(
                    id=new_id,
                    project_id=project_id,
                    requirement_id=requirement.id,
                    evidence_type="model",
                    evidence_status="needs_review",
                    source_ref=source_ref,
                    element_unique_id=element.unique_id,
                    confidence=score,
                    metadata_json=meta,
                )
                db.add(new_evidence)
                created += 1
            else:
                # Preserve accepted/rejected status; only refresh if still candidate
                existing_review_status = (existing.metadata_json or {}).get("review_status", "candidate")
                if existing_review_status in {"candidate", "needs_review"}:
                    existing.evidence_status = "needs_review"
                    existing.confidence = score
                    existing.metadata_json = {**(existing.metadata_json or {}), **meta}
                    existing.updated_at = now
                updated += 1

            with_candidates.add(requirement.id)

    db.commit()

    requirements_missing = len(requirements) - len(with_candidates)
    review_required = created + sum(
        1
        for req_id in with_candidates
        if req_id not in {r.id for r in requirements if r.id in with_candidates}
    )

    return {
        "project_id": project_id,
        "state": "ok",
        "latest_export_id": latest_export.id,
        "requirements_checked": len(requirements),
        "requirements_with_candidates": len(with_candidates),
        "candidate_evidence_created": created,
        "candidate_evidence_updated": updated,
        "requirements_missing_model_evidence": requirements_missing,
        "review_required": created + updated,
        "warnings": warnings,
    }


# ---------------------------------------------------------------------------
# Private helpers
# ---------------------------------------------------------------------------

def _build_source_label(element: Element) -> str:
    parts = [p for p in [element.category, element.family, element.type, element.name] if p]
    label = " · ".join(parts[:3])
    if element.level:
        label = f"{label} @ {element.level}"
    return label or element.unique_id
