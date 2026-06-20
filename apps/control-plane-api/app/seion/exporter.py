"""Export official EMA PostgreSQL facts into SEION-KGE JSONL files.

The export is advisory input only. It does not run SEION training/scoring and
does not write official readiness, compliance, issue, or evidence records.
"""

from __future__ import annotations

import json
import re
from collections.abc import Iterable
from pathlib import Path
from typing import Any

from sqlalchemy import select
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.orm import Session

from app.models import (
    Client,
    Element,
    Export,
    Issue,
    Model,
    Project,
    Requirement,
    RequirementCompliance,
    RequirementEvidence,
)
from app.seion.schemas import SeionEntity, SeionGraphExportResult, SeionTriple

DEFAULT_OUTPUT_DIR = Path(__file__).resolve().parents[2] / "artifacts" / "seion"


def export_seion_graph(
    db: Session,
    output_dir: Path | None = None,
) -> SeionGraphExportResult:
    """Write SEION-KGE-friendly entities/triples JSONL from official DB facts."""

    target_dir = output_dir or DEFAULT_OUTPUT_DIR
    target_dir.mkdir(parents=True, exist_ok=True)
    warnings: list[str] = []
    entities: dict[str, SeionEntity] = {}
    triples: set[tuple[str, str, str]] = set()

    def add_entity(entity: SeionEntity) -> None:
        entities.setdefault(entity.uid, entity)

    def add_triple(head: str | None, relation: str, tail: str | None, **properties: Any) -> None:
        if not head or not tail:
            return
        triples.add((head, relation, tail))

    projects = _safe_query(db, select(Project), "project", warnings)
    clients = _safe_query(db, select(Client), "client", warnings)
    models = _safe_query(db, select(Model), "model", warnings)
    exports = _safe_query(db, select(Export), "export", warnings)
    elements = _safe_query(db, select(Element), "element", warnings)
    issues = _safe_query(db, select(Issue), "issue", warnings)
    requirements = _safe_query(db, select(Requirement), "requirement", warnings)
    compliances = _safe_query(db, select(RequirementCompliance), "requirement_compliance", warnings)
    evidence_rows = _safe_query(db, select(RequirementEvidence), "requirement_evidence", warnings)

    projects_by_id = {row.id: row for row in projects}
    exports_by_id = {row.id: row for row in exports}
    elements_by_id = {row.id: row for row in elements}
    requirements_by_id = {row.id: row for row in requirements}

    for client in clients:
        add_entity(SeionEntity(_uid("client", client.id), "client", client.display_name, {"code": client.code}))

    for project in projects:
        project_uid = _uid("project", project.id)
        add_entity(
            SeionEntity(
                project_uid,
                "project",
                project.project_title,
                {
                    "project_code": project.project_code,
                    "phase": project.phase,
                    "has_client": project.client_id is not None,
                },
            )
        )
        if project.client_id:
            add_entity(SeionEntity(_uid("client", project.client_id), "client"))
            add_triple(project_uid, "belongs_to_client", _uid("client", project.client_id))

    for model in models:
        model_uid = _uid("model", model.id)
        add_entity(
            SeionEntity(
                model_uid,
                "model",
                model.revit_file_name,
                {"discipline": model.discipline, "model_type": model.model_type},
            )
        )
        if model.discipline:
            discipline_uid = _uid("discipline", _normalize_token(model.discipline))
            add_entity(SeionEntity(discipline_uid, "discipline", model.discipline))

    for export in exports:
        export_uid = _uid("export", export.id)
        project_uid = _uid("project", export.project_id)
        add_entity(
            SeionEntity(
                export_uid,
                "export",
                export.file_name,
                {"export_type": export.export_type, "status": export.status, "element_count": export.element_count},
            )
        )
        add_triple(project_uid, "has_export", export_uid)
        add_triple(export_uid, "belongs_to_project", project_uid)

    for element in elements:
        element_uid = _uid("element", element.id)
        export_uid = _uid("export", element.export_id)
        add_entity(
            SeionEntity(
                element_uid,
                "element",
                element.name or element.unique_id,
                {"unique_id": element.unique_id, "element_id": element.element_id, "category": element.category},
            )
        )
        add_triple(export_uid, "contains_element", element_uid)
        add_triple(element_uid, "belongs_to_export", export_uid)
        if element.category:
            category_uid = _uid("category", _normalize_token(element.category))
            add_entity(SeionEntity(category_uid, "category", element.category))
            add_triple(element_uid, "has_category", category_uid)

    for issue in issues:
        issue_uid = _uid("issue", issue.id)
        add_entity(
            SeionEntity(
                issue_uid,
                "issue",
                issue.message,
                {"rule_code": issue.rule_code, "issue_type": issue.issue_type, "source": issue.source},
            )
        )
        add_triple(_uid("project", issue.project_id), "has_issue", issue_uid)
        if issue.element_db_id:
            add_triple(issue_uid, "affects_element", _uid("element", issue.element_db_id))
        elif issue.element_unique_id:
            matched = next((row for row in elements if row.unique_id == issue.element_unique_id), None)
            if matched:
                add_triple(issue_uid, "affects_element", _uid("element", matched.id))
        severity_uid = _uid("severity", _normalize_token(issue.severity))
        status_uid = _uid("status", _normalize_token(issue.status))
        add_entity(SeionEntity(severity_uid, "severity", issue.severity))
        add_entity(SeionEntity(status_uid, "status", issue.status))
        add_triple(issue_uid, "has_severity", severity_uid)
        add_triple(issue_uid, "has_status", status_uid)

    for requirement in requirements:
        requirement_uid = _uid("requirement", requirement.id)
        add_entity(
            SeionEntity(
                requirement_uid,
                "requirement",
                f"REQ-{requirement.id}",
                {
                    "discipline": requirement.discipline,
                    "category": requirement.category,
                    "is_actionable": requirement.is_actionable,
                    "is_active": requirement.is_active,
                },
            )
        )
        add_triple(_uid("client", requirement.client_id), "has_requirement", requirement_uid)
        if requirement.discipline:
            discipline_uid = _uid("discipline", _normalize_token(requirement.discipline))
            add_entity(SeionEntity(discipline_uid, "discipline", requirement.discipline))
            add_triple(requirement_uid, "belongs_to_discipline", discipline_uid)

    for project in projects:
        if not project.client_id:
            continue
        for requirement in requirements:
            if requirement.client_id == project.client_id and requirement.is_active and requirement.is_actionable:
                add_triple(_uid("project", project.id), "evaluates_requirement", _uid("requirement", requirement.id))

    for compliance in compliances:
        compliance_uid = _uid("requirement_compliance", compliance.id)
        requirement_uid = _uid("requirement", compliance.requirement_id)
        add_entity(SeionEntity(compliance_uid, "requirement_compliance", properties={"status": compliance.status}))
        add_triple(compliance_uid, "evaluates", requirement_uid)
        status_uid = _uid("status", _normalize_token(compliance.status))
        add_entity(SeionEntity(status_uid, "status", compliance.status))
        add_triple(compliance_uid, "has_status", status_uid)

    for evidence in evidence_rows:
        evidence_uid = _uid("requirement_evidence", evidence.id)
        add_entity(
            SeionEntity(
                evidence_uid,
                "requirement_evidence",
                evidence.source_ref,
                {"evidence_type": evidence.evidence_type, "evidence_status": evidence.evidence_status},
            )
        )
        add_triple(evidence_uid, "supports_requirement", _uid("requirement", evidence.requirement_id))
        add_triple(evidence_uid, "belongs_to_project", _uid("project", evidence.project_id))

    entities_path = target_dir / "entities.jsonl"
    triples_path = target_dir / "triples.jsonl"
    _write_jsonl(entities_path, (_entity_to_json(entity) for entity in sorted(entities.values(), key=lambda row: row.uid)))
    _write_jsonl(
        triples_path,
        ({"head": head, "relation": relation, "tail": tail} for head, relation, tail in sorted(triples)),
    )

    return SeionGraphExportResult(
        entity_count=len(entities),
        triple_count=len(triples),
        entities_path=entities_path,
        triples_path=triples_path,
        warnings=warnings,
    )


def _safe_query(db: Session, stmt: Any, label: str, warnings: list[str]) -> list[Any]:
    try:
        return list(db.execute(stmt).scalars().all())
    except SQLAlchemyError as exc:
        db.rollback()
        warnings.append(f"Skipped {label}: {exc.__class__.__name__}")
        return []


def _uid(kind: str, value: Any) -> str:
    return f"{kind}:{value}"


def _normalize_token(value: str) -> str:
    normalized = re.sub(r"[^a-z0-9]+", "_", value.strip().lower())
    return normalized.strip("_") or "unknown"


def _entity_to_json(entity: SeionEntity) -> dict[str, Any]:
    payload: dict[str, Any] = {"uid": entity.uid, "type": entity.type}
    if entity.label:
        payload["label"] = entity.label
    if entity.properties:
        payload["properties"] = {key: value for key, value in entity.properties.items() if value is not None}
    return payload


def _write_jsonl(path: Path, rows: Iterable[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row, ensure_ascii=False, default=str))
            handle.write("\n")

