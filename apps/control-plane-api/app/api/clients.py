"""Client (owner/district) + owner-requirements API endpoints."""

from __future__ import annotations

import logging
import shutil
import uuid
from datetime import datetime, timezone
from pathlib import Path

from fastapi import APIRouter, Depends, File, Form, HTTPException, Query, UploadFile
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from app.config import settings
from app.database import get_db
from app.ingestion.requirements_loader import ingest_requirements_file
from app.services.evidence_service import (
    coverage_status_from_requirement,
    evidence_review_status,
    latest_project_evidence_by_requirement,
    normalized_status_from_evidence,
    requirement_evidence_status_from_review,
)
from app.models import (
    Client,
    Issue,
    Model as ModelRecord,
    Project,
    Requirement,
    RequirementCompliance,
    RequirementSourceFile,
)
from app.schemas import (
    ClientCreate,
    ClientOut,
    ImportMode,
    ProjectComplianceMatrix,
    ProjectComplianceRow,
    ProjectRequirementCounts,
    ProjectRequirementRow,
    ProjectRequirementsResponse,
    RequirementComplianceOut,
    RequirementComplianceUpdate,
    RequirementMappingUpdate,
    RequirementIngestResponse,
    RequirementListResponse,
    RequirementOut,
    RequirementSourceFileOut,
)

logger = logging.getLogger(__name__)

router = APIRouter(prefix="/api/v1", tags=["clients"])
DEFAULT_ORG_ID = 1


# ----------------------------------------------------------------------------
# Clients
# ----------------------------------------------------------------------------


@router.get("/clients", response_model=list[ClientOut], summary="List owners / districts")
def list_clients(db: Session = Depends(get_db)) -> list[ClientOut]:
    rows = db.execute(select(Client).order_by(Client.display_name)).scalars().all()
    return [ClientOut.model_validate(r) for r in rows]


@router.post("/clients", response_model=ClientOut, status_code=201, summary="Create a client")
def create_client(payload: ClientCreate, db: Session = Depends(get_db)) -> ClientOut:
    code = payload.code.strip().upper().replace(" ", "_")
    org_id = DEFAULT_ORG_ID

    existing = db.execute(
        select(Client).where(Client.organization_id == org_id, Client.code == code)
    ).scalar_one_or_none()
    if existing is not None:
        raise HTTPException(status_code=409, detail=f"Client {code} already exists")

    client = Client(
        organization_id=org_id,
        code=code,
        display_name=payload.display_name.strip(),
        sharepoint_path=payload.sharepoint_path,
    )
    db.add(client)
    db.commit()
    db.refresh(client)
    return ClientOut.model_validate(client)


@router.get("/clients/{client_id}", response_model=ClientOut, summary="Get a client")
def get_client(client_id: int, db: Session = Depends(get_db)) -> ClientOut:
    client = db.get(Client, client_id)
    if client is None:
        raise HTTPException(status_code=404, detail="Client not found")
    return ClientOut.model_validate(client)


# ----------------------------------------------------------------------------
# Requirements ingest + query
# ----------------------------------------------------------------------------


def _resolve_client(db: Session, client_id: int) -> Client:
    client = db.get(Client, client_id)
    if client is None:
        raise HTTPException(status_code=404, detail="Client not found")
    return client


def _resolve_landing_path(relative_path: str) -> Path:
    landing_root = settings.landing_dir.resolve()
    stored_path = (landing_root / relative_path).resolve()
    try:
        stored_path.relative_to(landing_root)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail="Path must be inside the landing directory") from exc
    return stored_path


@router.post(
    "/clients/{client_id}/requirements/ingest",
    response_model=RequirementIngestResponse,
    summary="Upload an xlsx of owner requirements and sync into the client's catalog",
)
def ingest_requirements(
    client_id: int,
    file: UploadFile = File(..., description="SharePoint-exported xlsx of owner requirements"),
    import_mode: ImportMode = Query(default="full_snapshot", description="Import mode: full_snapshot, partial_update, append_only"),
    dry_run: bool = Query(default=False, description="If true, report diff without modifying database"),
    db: Session = Depends(get_db),
) -> RequirementIngestResponse:
    client = _resolve_client(db, client_id)

    if not file.filename or not file.filename.lower().endswith(".xlsx"):
        raise HTTPException(status_code=400, detail="File must be a .xlsx")

    landing = settings.landing_dir / "requirements"
    landing.mkdir(parents=True, exist_ok=True)

    ts = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S")
    uid = uuid.uuid4().hex[:8]
    stored_path = landing / f"{ts}_{uid}_{Path(file.filename).name}"
    with open(stored_path, "wb") as fh:
        shutil.copyfileobj(file.file, fh)

    try:
        result = ingest_requirements_file(
            db=db,
            client=client,
            xlsx_path=stored_path,
            original_filename=file.filename,
            import_mode=import_mode,
            dry_run=dry_run,
        )
        if not dry_run:
            db.commit()
    except Exception as exc:
        db.rollback()
        logger.exception("Requirements ingest failed for client=%s file=%s", client.code, file.filename)
        raise HTTPException(status_code=500, detail=f"Ingest failed: {exc}") from exc

    return RequirementIngestResponse(**result)


@router.post(
    "/clients/{client_id}/requirements/ingest-path",
    response_model=RequirementIngestResponse,
    summary="Ingest an xlsx that is already present inside the landing zone",
)
def ingest_requirements_from_path(
    client_id: int,
    relative_path: str = Form(..., description="Path relative to landing dir (xlsx)"),
    import_mode: ImportMode = Query(default="full_snapshot", description="Import mode: full_snapshot, partial_update, append_only"),
    dry_run: bool = Query(default=False, description="If true, report diff without modifying database"),
    db: Session = Depends(get_db),
) -> RequirementIngestResponse:
    client = _resolve_client(db, client_id)

    stored_path = _resolve_landing_path(relative_path)
    if not stored_path.exists():
        raise HTTPException(status_code=404, detail=f"File not found: {relative_path}")
    if stored_path.suffix.lower() != ".xlsx":
        raise HTTPException(status_code=400, detail="File must be a .xlsx")

    try:
        result = ingest_requirements_file(
            db=db,
            client=client,
            xlsx_path=stored_path,
            original_filename=stored_path.name,
            import_mode=import_mode,
            dry_run=dry_run,
        )
        if not dry_run:
            db.commit()
    except Exception as exc:
        db.rollback()
        logger.exception("Requirements ingest failed for client=%s file=%s", client.code, relative_path)
        raise HTTPException(status_code=500, detail=f"Ingest failed: {exc}") from exc

    return RequirementIngestResponse(**result)


@router.get(
    "/clients/{client_id}/requirements",
    response_model=RequirementListResponse,
    summary="List requirements for a client with filters + pagination",
)
def list_requirements(
    client_id: int,
    discipline: str | None = Query(default=None),
    category: str | None = Query(default=None),
    active: bool = Query(default=True),
    search: str | None = Query(default=None, min_length=2),
    page: int = Query(default=1, ge=1),
    page_size: int = Query(default=50, ge=1, le=500),
    db: Session = Depends(get_db),
) -> RequirementListResponse:
    _resolve_client(db, client_id)

    base = select(Requirement).where(Requirement.client_id == client_id)
    if discipline:
        base = base.where(Requirement.discipline == discipline.upper())
    if category:
        base = base.where(Requirement.category.ilike(f"%{category}%"))
    if active:
        base = base.where(Requirement.is_active.is_(True))
    if search:
        base = base.where(Requirement.requirement_text.ilike(f"%{search}%"))

    total = db.execute(select(func.count()).select_from(base.subquery())).scalar_one()

    stmt = (
        base.order_by(Requirement.discipline, Requirement.id)
        .offset((page - 1) * page_size)
        .limit(page_size)
    )
    rows = db.execute(stmt).scalars().all()

    return RequirementListResponse(
        client_id=client_id,
        total=int(total),
        page=page,
        page_size=page_size,
        items=[RequirementOut.model_validate(r) for r in rows],
    )


@router.get(
    "/clients/{client_id}/requirements/source-files",
    response_model=list[RequirementSourceFileOut],
    summary="List ingested xlsx files for a client (ingestion history)",
)
def list_source_files(
    client_id: int, db: Session = Depends(get_db)
) -> list[RequirementSourceFileOut]:
    _resolve_client(db, client_id)
    rows = (
        db.execute(
            select(RequirementSourceFile)
            .where(RequirementSourceFile.client_id == client_id)
            .order_by(RequirementSourceFile.ingested_at.desc())
        )
        .scalars()
        .all()
    )
    return [RequirementSourceFileOut.model_validate(r) for r in rows]


@router.get(
    "/requirements/{requirement_id}",
    response_model=RequirementOut,
    summary="Get a single requirement",
)
def get_requirement(requirement_id: int, db: Session = Depends(get_db)) -> RequirementOut:
    req = db.get(Requirement, requirement_id)
    if req is None:
        raise HTTPException(status_code=404, detail="Requirement not found")
    return RequirementOut.model_validate(req)


# ----------------------------------------------------------------------------
# Project compliance matrix (read + manual mark)
# ----------------------------------------------------------------------------


def _resolve_project(db: Session, project_id: int) -> Project:
    project = db.get(Project, project_id)
    if project is None:
        raise HTTPException(status_code=404, detail="Project not found")
    return project


@router.get(
    "/projects/{project_id}/compliance",
    response_model=ProjectComplianceMatrix,
    summary="Compliance matrix for a project, based on its client's requirements",
)
def get_project_compliance(
    project_id: int,
    discipline: str | None = Query(default=None),
    db: Session = Depends(get_db),
) -> ProjectComplianceMatrix:
    project = _resolve_project(db, project_id)

    if project.client_id is None:
        return ProjectComplianceMatrix(
            project_id=project_id,
            client_id=None,
            total_requirements=0,
            by_status={},
            items=[],
        )

    stmt = (
        select(Requirement, RequirementCompliance)
        .outerjoin(
            RequirementCompliance,
            (RequirementCompliance.requirement_id == Requirement.id)
            & (RequirementCompliance.project_id == project_id),
        )
        .where(
            Requirement.client_id == project.client_id,
            Requirement.is_active.is_(True),
        )
        .order_by(Requirement.discipline, Requirement.id)
    )

    if discipline:
        stmt = stmt.where(Requirement.discipline == discipline.upper())

    rows: list[ProjectComplianceRow] = []
    status_counter: dict[str, int] = {}

    for req, comp in db.execute(stmt).all():
        status = comp.status if comp else "not_evaluated"
        evidence = comp.evidence if comp and isinstance(comp.evidence, dict) else {}
        rows.append(
            ProjectComplianceRow(
                requirement_id=req.id,
                discipline=req.discipline,
                category=req.category,
                milestone=_milestone_from_compliance(comp) or _milestone_from_requirement(req),
                requirement_text=req.requirement_text,
                compliance_id=comp.id if comp else None,
                status=status,
                evaluated_by=comp.evaluated_by if comp else None,
                evaluated_at=comp.evaluated_at if comp else None,
                notes=comp.notes if comp else None,
                evidence_type=evidence.get("type") or evidence.get("evidence_type"),
                evidence_status=evidence.get("status") or evidence.get("evidence_status"),
                evidence_source=evidence.get("source") or evidence.get("source_ref"),
            )
        )
        status_counter[status] = status_counter.get(status, 0) + 1

    return ProjectComplianceMatrix(
        project_id=project_id,
        client_id=project.client_id,
        total_requirements=len(rows),
        by_status=status_counter,
        items=rows,
    )


@router.patch(
    "/projects/{project_id}/requirements/{requirement_id}/mapping",
    response_model=RequirementComplianceOut,
    summary="Update milestone mapping and requirement flags for a (project, requirement) pair",
)
def update_project_requirement_mapping(
    project_id: int,
    requirement_id: int,
    payload: RequirementMappingUpdate,
    db: Session = Depends(get_db),
) -> RequirementComplianceOut:
    project = _resolve_project(db, project_id)
    if project.client_id is None:
        raise HTTPException(status_code=400, detail="Project is not associated with a client")

    req = db.get(Requirement, requirement_id)
    if req is None:
        raise HTTPException(status_code=404, detail="Requirement not found")
    if req.client_id != project.client_id:
        raise HTTPException(status_code=400, detail="Requirement does not belong to this project's client")

    if payload.discipline is not None:
        req.discipline = payload.discipline.strip().upper()
    if payload.is_actionable is not None:
        req.is_actionable = payload.is_actionable

    existing = db.execute(
        select(RequirementCompliance).where(
            RequirementCompliance.requirement_id == requirement_id,
            RequirementCompliance.project_id == project_id,
        )
    ).scalar_one_or_none()
    if existing is None:
        existing = RequirementCompliance(
            requirement_id=requirement_id,
            project_id=project_id,
            model_id=None,
            status="not_evaluated",
            evidence={},
            evaluated_by="manual",
            notes=payload.notes,
        )
        db.add(existing)
    evidence = dict(existing.evidence or {})
    if payload.milestone is not None:
        evidence["milestone"] = payload.milestone.strip()
    if payload.discipline is not None:
        evidence["discipline"] = req.discipline
    if payload.is_actionable is not None:
        evidence["is_actionable"] = payload.is_actionable
    evidence["mapping_updated_at"] = datetime.now(timezone.utc).isoformat()
    existing.evidence = evidence
    existing.evaluated_by = existing.evaluated_by or "manual"
    if payload.notes is not None:
        existing.notes = payload.notes
    existing.evaluated_at = datetime.now(timezone.utc)

    db.commit()
    db.refresh(existing)
    return RequirementComplianceOut.model_validate(existing)


@router.get(
    "/projects/{project_id}/requirements",
    response_model=ProjectRequirementsResponse,
    summary="Project-level owner requirements with readiness/evidence semantics",
)
def get_project_requirements(
    project_id: int,
    search: str | None = Query(default=None),
    discipline: str | None = Query(default=None),
    source_type: str | None = Query(default=None),
    status: str | None = Query(default=None),
    actionable: bool | None = Query(default=None),
    evidence_status: str | None = Query(default=None),
    page: int = Query(default=1, ge=1),
    page_size: int = Query(default=100, ge=1, le=500),
    db: Session = Depends(get_db),
) -> ProjectRequirementsResponse:
    project = _resolve_project(db, project_id)
    if project.client_id is None:
        return ProjectRequirementsResponse(
            project_id=project_id,
            project_name=project.project_name or project.project_title,
            state="no_client_linked",
            counts=ProjectRequirementCounts(),
            items=[],
            page=page,
            page_size=page_size,
            total=0,
        )

    client = db.get(Client, project.client_id)
    req_stmt = select(Requirement).where(
        Requirement.client_id == project.client_id,
        Requirement.is_active.is_(True),
    )
    requirements = list(db.execute(req_stmt).scalars())
    if not requirements:
        return ProjectRequirementsResponse(
            project_id=project_id,
            project_name=project.project_name or project.project_title,
            client_id=project.client_id,
            client_name=client.display_name if client else project.client_name,
            state="client_linked_no_requirements",
            counts=ProjectRequirementCounts(),
            items=[],
            page=page,
            page_size=page_size,
            total=0,
        )

    compliance_by_requirement = _latest_project_compliance(db, project_id)
    evidence_by_requirement = latest_project_evidence_by_requirement(db, project_id)
    issue_by_discipline = _first_issue_by_discipline(db, project_id)

    rows: list[ProjectRequirementRow] = []
    counts = ProjectRequirementCounts()
    by_discipline: dict[str, ProjectRequirementCounts] = {}
    by_source_type: dict[str, int] = {}
    by_owner_status: dict[str, int] = {}

    for req in requirements:
        evidence = evidence_by_requirement.get(req.id)
        readiness_status = coverage_status_from_requirement(req, compliance_by_requirement.get(req.id), evidence)
        ev_review = evidence_review_status(evidence) if evidence else "none"
        row = ProjectRequirementRow(
            requirement_id=req.id,
            discipline=req.discipline,
            category=req.category,
            milestone=_milestone_from_compliance(compliance_by_requirement.get(req.id)) or _milestone_from_requirement(req),
            requirement_text=req.requirement_text,
            owner_status=req.owner_status,
            is_actionable=req.is_actionable,
            readiness_status=readiness_status,
            normalized_status=normalized_status_from_evidence(req.is_actionable, ev_review),
            evidence_status=requirement_evidence_status_from_review(ev_review),
            evidence_review_status=ev_review,
            evidence_source_label=evidence.source_label if evidence else None,
            related_issue_id=issue_by_discipline.get(req.discipline),
            related_sheet=evidence.sheet_number if evidence else None,
            updated_at=req.last_seen_at,
        )
        _add_requirement_counts(counts, req.is_actionable, readiness_status, ev_review)
        discipline_counts = by_discipline.setdefault(req.discipline, ProjectRequirementCounts())
        _add_requirement_counts(discipline_counts, req.is_actionable, readiness_status, ev_review)
        by_source_type[row.source_type] = by_source_type.get(row.source_type, 0) + 1
        owner_status = req.owner_status or "unspecified"
        by_owner_status[owner_status] = by_owner_status.get(owner_status, 0) + 1
        rows.append(row)

    if counts.actionable > 0:
        counts.requirement_coverage_ratio = round(counts.covered / counts.actionable, 4)
        counts.requirement_coverage_percent = round(counts.covered / counts.actionable * 100.0, 2)

    filtered = rows
    if search:
        token = search.lower()
        filtered = [row for row in filtered if token in f"{row.requirement_text} {row.discipline} {row.category or ''}".lower()]
    if discipline and discipline != "all":
        filtered = [row for row in filtered if row.discipline == discipline.upper()]
    if source_type and source_type != "all":
        filtered = [row for row in filtered if row.source_type == source_type]
    if status and status != "all":
        filtered = [row for row in filtered if row.readiness_status == status]
    if actionable is not None:
        filtered = [row for row in filtered if row.is_actionable is actionable]
    if evidence_status and evidence_status != "all":
        filtered = [row for row in filtered if row.evidence_status == evidence_status or row.normalized_status == evidence_status]

    total = len(filtered)
    start = (page - 1) * page_size
    end = start + page_size
    paged = filtered[start:end]

    return ProjectRequirementsResponse(
        project_id=project_id,
        project_name=project.project_name or project.project_title,
        client_id=project.client_id,
        client_name=client.display_name if client else project.client_name,
        state=_compute_requirements_state(requirements, counts, total),
        counts=counts,
        by_discipline=by_discipline,
        by_source_type=by_source_type,
        by_owner_status=by_owner_status,
        items=paged,
        page=page,
        page_size=page_size,
        total=total,
    )


@router.put(
    "/projects/{project_id}/requirements/{requirement_id}/compliance",
    response_model=RequirementComplianceOut,
    summary="Create or update compliance state for a (project, requirement) pair",
)
def upsert_project_requirement_compliance(
    project_id: int,
    requirement_id: int,
    payload: RequirementComplianceUpdate,
    db: Session = Depends(get_db),
) -> RequirementComplianceOut:
    project = _resolve_project(db, project_id)
    if project.client_id is None:
        raise HTTPException(
            status_code=400,
            detail="Project is not associated with a client",
        )

    req = db.get(Requirement, requirement_id)
    if req is None:
        raise HTTPException(status_code=404, detail="Requirement not found")
    if req.client_id != project.client_id:
        raise HTTPException(
            status_code=400,
            detail="Requirement does not belong to this project's client",
        )
    if payload.model_id is not None:
        model_belongs_to_project = db.execute(
            select(ModelRecord.id).where(
                ModelRecord.id == payload.model_id,
                ModelRecord.project_id == project_id,
            )
        ).scalar_one_or_none()
        if model_belongs_to_project is None:
            raise HTTPException(
                status_code=400,
                detail="Model does not belong to this project",
            )

    existing = db.execute(
        select(RequirementCompliance).where(
            RequirementCompliance.requirement_id == requirement_id,
            RequirementCompliance.project_id == project_id,
            RequirementCompliance.model_id.is_(payload.model_id) if payload.model_id is None
            else RequirementCompliance.model_id == payload.model_id,
        )
    ).scalar_one_or_none()

    if existing is None:
        existing = RequirementCompliance(
            requirement_id=requirement_id,
            project_id=project_id,
            model_id=payload.model_id,
            status=payload.status,
            evidence=payload.evidence,
            evaluated_by=payload.evaluated_by or "manual",
            notes=payload.notes,
        )
        db.add(existing)
    else:
        existing.status = payload.status
        existing.evidence = payload.evidence
        existing.evaluated_by = payload.evaluated_by or "manual"
        existing.notes = payload.notes
        existing.evaluated_at = datetime.now(timezone.utc)

    db.commit()
    db.refresh(existing)
    return RequirementComplianceOut.model_validate(existing)


def _latest_project_compliance(db: Session, project_id: int) -> dict[int, RequirementCompliance]:
    rows = db.execute(
        select(RequirementCompliance)
        .where(RequirementCompliance.project_id == project_id)
        .order_by(
            RequirementCompliance.requirement_id,
            RequirementCompliance.evaluated_at.desc(),
            RequirementCompliance.id.desc(),
        )
    ).scalars()
    latest: dict[int, RequirementCompliance] = {}
    for row in rows:
        latest.setdefault(row.requirement_id, row)
    return latest


def _first_issue_by_discipline(db: Session, project_id: int) -> dict[str, int]:
    rows = db.execute(
        select(Issue).where(Issue.project_id == project_id, Issue.status == "open").order_by(Issue.severity.desc(), Issue.id)
    ).scalars()
    result: dict[str, int] = {}
    for issue in rows:
        text = f"{issue.message or ''} {issue.issue_type or ''} {issue.traceability or ''}".upper()
        for discipline in ("ELECTRICAL", "MECHANICAL", "PLUMBING", "TECHNOLOGY", "LIGHTING"):
            if discipline[:5] in text or discipline in text:
                result.setdefault(discipline, issue.id)
    return result


def _add_requirement_counts(
    counts: ProjectRequirementCounts,
    is_actionable: bool,
    status: str,
    ev_review_status: str = "none",
) -> None:
    counts.total += 1
    if is_actionable:
        counts.actionable += 1
    else:
        counts.non_actionable += 1
    if status == "compliant":
        counts.covered += 1
    if status in {"compliant", "non_compliant", "needs_review"}:
        counts.evaluated += 1
    if status == "missing":
        counts.missing += 1
    if status == "needs_review":
        counts.needs_review += 1
    if status == "not_applicable":
        counts.not_applicable += 1
    if ev_review_status == "rejected" or status == "non_compliant":
        counts.rejected += 1
    if ev_review_status == "candidate":
        counts.candidate_evidence_count += 1
    elif ev_review_status == "accepted":
        counts.accepted_evidence_count += 1
    elif ev_review_status == "rejected":
        counts.rejected_evidence_count += 1
    elif is_actionable:
        counts.no_evidence_count += 1


def _compute_requirements_state(
    requirements: list,
    counts: ProjectRequirementCounts,
    filtered_total: int,
) -> str:
    if not requirements:
        return "client_linked_no_requirements"
    if filtered_total == 0:
        return "filtered_empty"
    if counts.accepted_evidence_count > 0:
        return "readiness_available"
    if counts.candidate_evidence_count > 0 or counts.needs_review > 0:
        return "evidence_candidates_pending"
    return "requirements_loaded_no_evidence"


def _milestone_from_requirement(req: Requirement) -> str | None:
    text = f"{req.category or ''} {req.requirement_text}".upper()
    for milestone in ("DD 50", "DD 75", "DD 95", "CD 50", "CD 75", "CD 95", "CD 100"):
        if milestone.replace(" ", "") in text.replace(" ", "") or milestone in text:
            return milestone
    return None


def _milestone_from_compliance(comp: RequirementCompliance | None) -> str | None:
    if comp is None or not isinstance(comp.evidence, dict):
        return None
    raw = comp.evidence.get("milestone")
    if isinstance(raw, str) and raw.strip():
        return raw.strip()
    return None
