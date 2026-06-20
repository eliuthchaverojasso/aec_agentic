from __future__ import annotations

import csv
import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from app.config import settings
from app.ingestion.manifest_loader import resolve_landing_path
from app.schemas import (
    ComplianceCorpusOut,
    ComplianceGateOut,
    ComplianceImportOut,
    ComplianceLoaderPreviewOut,
    ComplianceLoaderPreviewRequest,
    ComplianceRuleOut,
    ComplianceStatusOut,
)


@dataclass
class _Corpus:
    id: int
    name: str
    code_family: str
    edition: str | None
    jurisdiction: str | None
    source_type: str
    loader_status: str
    health_score: float | None
    gate_status: str
    notes: str | None
    created_at: datetime


@dataclass
class _Rule:
    id: int
    corpus_id: int
    reference: str
    title: str | None
    requirement_text: str
    discipline: str | None
    validation_type: str = "semantic_review"
    status: str = "candidate"
    review_status: str = "pending"
    source_document: str | None = None
    created_at: datetime = field(default_factory=lambda: datetime.now(timezone.utc))


_CORPORA: list[_Corpus] = []
_RULES: list[_Rule] = []
_LAST_RUN_AT: datetime | None = None
_NEXT_CORPUS_ID = 1
_NEXT_RULE_ID = 1


def _now() -> datetime:
    return datetime.now(timezone.utc)


def _safe_path(path: str | None) -> Path | None:
    if not path:
        return None
    return resolve_landing_path(settings.landing_dir, path)


def _parse_blocks_count(path: Path | None) -> tuple[int, list[str], list[str]]:
    if path is None:
        return 0, [], []
    warnings: list[str] = []
    refs: list[str] = []
    count = 0
    with path.open("r", encoding="utf-8") as handle:
        for line in handle:
            line = line.strip()
            if not line:
                continue
            count += 1
            if len(refs) < 10:
                try:
                    payload = json.loads(line)
                    ref = payload.get("reference_id") or payload.get("section") or payload.get("id")
                    if ref:
                        refs.append(str(ref))
                except json.JSONDecodeError:
                    warnings.append("Some block rows are not valid JSON.")
                    break
    return count, refs, warnings


def _parse_edges_count(path: Path | None) -> int:
    if path is None:
        return 0
    with path.open("r", encoding="utf-8", newline="") as handle:
        reader = csv.reader(handle)
        rows = list(reader)
        return max(0, len(rows) - 1)


def preview_nec_loader(payload: ComplianceLoaderPreviewRequest) -> ComplianceLoaderPreviewOut:
    blocks_path = _safe_path(payload.blocks_path)
    edges_path = _safe_path(payload.edges_path)
    audit_path = _safe_path(payload.structure_audit_path)
    gates_path = _safe_path(payload.gates_path)

    blocks_count, refs, warnings = _parse_blocks_count(blocks_path)
    edges_count = _parse_edges_count(edges_path)

    health_score: float | None = None
    failed_gates: list[ComplianceGateOut] = []
    gate_status = "unknown"

    if audit_path and audit_path.exists():
        audit_payload = json.loads(audit_path.read_text(encoding="utf-8"))
        score = audit_payload.get("health_score")
        health_score = float(score) if isinstance(score, (int, float)) else None

    if gates_path and gates_path.exists():
        gates_payload = json.loads(gates_path.read_text(encoding="utf-8"))
        gate_items = gates_payload.get("gates") if isinstance(gates_payload, dict) else None
        if isinstance(gate_items, list):
            for gate in gate_items:
                name = str(gate.get("name", "gate"))
                passed = bool(gate.get("passed", False))
                detail = str(gate.get("detail", "")) or None
                if not passed:
                    failed_gates.append(ComplianceGateOut(name=name, passed=False, detail=detail))
        gate_status = "failed" if failed_gates else "passed"

    if failed_gates and not payload.override_review_required:
        warnings.append("Critical gates failed; import will require reviewer activation.")

    return ComplianceLoaderPreviewOut(
        status="ok",
        code_family=payload.code_family,
        blocks_count=blocks_count,
        edges_count=edges_count,
        health_score=health_score,
        gate_status=gate_status,
        failed_gates=failed_gates,
        sample_references=refs[:5],
        warnings=warnings,
    )


def import_nec_corpus(payload: ComplianceLoaderPreviewRequest) -> ComplianceImportOut:
    global _NEXT_CORPUS_ID, _NEXT_RULE_ID, _LAST_RUN_AT
    preview = preview_nec_loader(payload)
    review_required = preview.gate_status == "failed"
    corpus_status = "candidate_review_required" if review_required else "candidate"
    corpus = _Corpus(
        id=_NEXT_CORPUS_ID,
        name=payload.name,
        code_family=payload.code_family,
        edition=payload.edition,
        jurisdiction=payload.jurisdiction,
        source_type=payload.source_type,
        loader_status=corpus_status,
        health_score=preview.health_score,
        gate_status=preview.gate_status,
        notes="Candidate corpus imported from local NEC structured files.",
        created_at=_now(),
    )
    _NEXT_CORPUS_ID += 1
    _CORPORA.append(corpus)

    rules_created = 0
    for ref in preview.sample_references:
        rule = _Rule(
            id=_NEXT_RULE_ID,
            corpus_id=corpus.id,
            reference=ref,
            title=f"Candidate rule {ref}",
            requirement_text=f"Candidate requirement from {ref}. Review before activation.",
            discipline=None,
            status="candidate",
            review_status="required",
        )
        _NEXT_RULE_ID += 1
        _RULES.append(rule)
        rules_created += 1

    _LAST_RUN_AT = _now()
    return ComplianceImportOut(
        status="ok",
        corpus=_to_corpus_out(corpus),
        rules_created=rules_created,
        references_created=rules_created,
        review_required=review_required,
    )


def list_corpora() -> list[ComplianceCorpusOut]:
    return [_to_corpus_out(item) for item in _CORPORA]


def get_corpus(corpus_id: int) -> ComplianceCorpusOut | None:
    row = next((item for item in _CORPORA if item.id == corpus_id), None)
    return _to_corpus_out(row) if row else None


def list_rules(corpus_id: int | None = None, status: str | None = None) -> list[ComplianceRuleOut]:
    rows = _RULES
    if corpus_id is not None:
        rows = [row for row in rows if row.corpus_id == corpus_id]
    if status:
        rows = [row for row in rows if row.status == status]
    return [_to_rule_out(row) for row in rows]


def get_status() -> ComplianceStatusOut:
    candidate_rules = len([row for row in _RULES if row.status == "candidate"])
    active_rules = len([row for row in _RULES if row.status == "active"])
    return ComplianceStatusOut(
        status="ok",
        corpora_count=len(_CORPORA),
        candidate_rules=candidate_rules,
        active_rules=active_rules,
        findings_count=0,
        latest_loader_run_at=_LAST_RUN_AT,
    )


def _to_corpus_out(item: _Corpus) -> ComplianceCorpusOut:
    return ComplianceCorpusOut(
        id=item.id,
        name=item.name,
        code_family=item.code_family,
        edition=item.edition,
        jurisdiction=item.jurisdiction,
        source_type=item.source_type,
        loader_status=item.loader_status,
        health_score=item.health_score,
        gate_status=item.gate_status,
        notes=item.notes,
        created_at=item.created_at,
    )


def _to_rule_out(item: _Rule) -> ComplianceRuleOut:
    return ComplianceRuleOut(
        id=item.id,
        corpus_id=item.corpus_id,
        reference=item.reference,
        title=item.title,
        requirement_text=item.requirement_text,
        discipline=item.discipline,
        validation_type=item.validation_type,  # type: ignore[arg-type]
        status=item.status,  # type: ignore[arg-type]
        review_status=item.review_status,
        source_document=item.source_document,
        created_at=item.created_at,
    )
