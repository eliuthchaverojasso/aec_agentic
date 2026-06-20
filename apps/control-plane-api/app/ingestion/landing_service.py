"""Orchestrate manifest-driven ingestion from the landing zone."""

from __future__ import annotations

import json
import hashlib
from collections import defaultdict
from pathlib import Path
from typing import Any

from sqlalchemy import select
from sqlalchemy.orm import Session

from app.config import settings
from app.ingestion.document_service import find_project_for_document, register_landing_document
from app.ingestion.extract_loader import summarize_project_extract
from app.ingestion.file_classifier import DOCUMENT_FILE_TYPES, infer_discipline_from_path_or_name, infer_sheet_number, infer_spec_section
from app.ingestion.file_classifier import classify_landing_file
from app.ingestion.loader import ingest_export
from app.ingestion.manifest_loader import LandingFileEntry, load_landing_manifest, resolve_landing_path
from app.ingestion.pdf_document_loader import build_pdf_document_record
from app.ingestion.requirements_loader import ingest_requirements_file
from app.models import Client, Project
from app.readiness.service import build_project_readiness
from app.schemas import LandingFileReport, LandingIngestReport


DEFAULT_EXPORT_TYPE = "all"


def ingest_landing_manifest(
    *,
    db: Session,
    manifest_path: str,
    dry_run: bool = False,
    recalculate_readiness: bool = True,
) -> LandingIngestReport:
    landing_root = settings.landing_dir.resolve()
    manifest = load_landing_manifest(landing_root, manifest_path)

    processed: dict[str, int] = defaultdict(int)
    created: dict[str, int] = defaultdict(int)
    updated: dict[str, int] = defaultdict(int)
    skipped: list[str] = []
    warnings: list[str] = []
    errors: list[str] = []
    files: list[LandingFileReport] = []
    touched_project_ids: set[int] = set()

    binding = dict(manifest.project_binding)

    if binding:
        report = _apply_project_binding(db, binding, dry_run=dry_run)
        warnings.extend(report["warnings"])
        errors.extend(report["errors"])
        updated["project_bindings"] += report["updated"]
        touched_project_ids.update(report["project_ids"])

    for entry in manifest.files:
        file_report = _process_manifest_entry(
            db=db,
            landing_root=landing_root,
            entry=entry,
            manifest_binding=binding,
            dry_run=dry_run,
        )
        files.append(file_report)
        if file_report.status == "completed":
            processed[_processed_key(file_report.type)] += 1
        elif file_report.status == "skipped":
            skipped.append(entry.path)

        warnings.extend(file_report.warnings)
        errors.extend(file_report.errors)
        _merge_counts(created, file_report.counts.get("created", {}))
        _merge_counts(updated, file_report.counts.get("updated", {}))
        project_id = file_report.counts.get("project_id")
        if isinstance(project_id, int):
            touched_project_ids.add(project_id)

        if file_report.status == "completed" and file_report.type == "revit_export" and binding:
            report = _apply_project_binding(db, binding, dry_run=dry_run)
            warnings.extend(report["warnings"])
            errors.extend(report["errors"])
            updated["project_bindings"] += report["updated"]
            touched_project_ids.update(report["project_ids"])

        entry_binding = _entry_binding(entry)
        if file_report.status == "completed" and file_report.type == "revit_export" and entry_binding:
            report = _apply_project_binding(db, {**binding, **entry_binding}, dry_run=dry_run)
            warnings.extend(report["warnings"])
            errors.extend(report["errors"])
            updated["project_bindings"] += report["updated"]
            touched_project_ids.update(report["project_ids"])

    if recalculate_readiness and not dry_run:
        readiness_gap_count = 0
        for project_id in touched_project_ids:
            project = db.get(Project, project_id)
            if project is None:
                continue
            readiness = build_project_readiness(db, project)
            readiness_gap_count += sum(readiness.gap_summary.values())
        if touched_project_ids:
            created["readiness_gaps"] += readiness_gap_count
            processed["readiness_calculations"] += len(touched_project_ids)
    elif recalculate_readiness and dry_run:
        warnings.append("Dry run requested; readiness was not recalculated.")

    if errors:
        status = "completed_with_errors" if any(item.status == "completed" for item in files) else "failed"
    else:
        status = "completed"

    if warnings:
        status = "completed_with_warnings" if status == "completed" else status

    return LandingIngestReport(
        status=status,
        dry_run=dry_run,
        manifest_path=str(manifest.manifest_path.relative_to(landing_root)),
        processed=dict(processed),
        created=dict(created),
        updated=dict(updated),
        skipped=skipped,
        warnings=warnings,
        errors=errors,
        files=files,
    )


def _process_manifest_entry(
    *,
    db: Session,
    landing_root: Path,
    entry: LandingFileEntry,
    manifest_binding: dict[str, Any],
    dry_run: bool,
) -> LandingFileReport:
    warnings: list[str] = []
    errors: list[str] = []
    counts: dict[str, Any] = {"created": {}, "updated": {}}

    try:
        path = resolve_landing_path(landing_root, entry.path)
    except ValueError as exc:
        return LandingFileReport(
            path=entry.path,
            type=entry.type,
            status="failed",
            message=str(exc),
            required=entry.required,
            errors=[str(exc)],
        )

    if not path.exists():
        message = f"Landing file not found: {entry.path}"
        return LandingFileReport(
            path=entry.path,
            type=entry.type,
            status="failed" if entry.required else "skipped",
            message=message,
            required=entry.required,
            warnings=[] if entry.required else [message],
            errors=[message] if entry.required else [],
        )

    if path.name.lower().endswith(".meta.json"):
        message = "Revit export sidecar metadata is not ingested as a document"
        return LandingFileReport(
            path=entry.path,
            type="unknown",
            status="failed" if entry.required else "skipped",
            message=message,
            required=entry.required,
            counts=counts,
            warnings=[] if entry.required else [message],
            errors=[message] if entry.required else [],
        )

    classification = classify_landing_file(path, entry.type)
    file_type = classification.type
    warnings.append(f"Classification: {classification.type} ({classification.confidence}) - {classification.reason}")

    try:
        if file_type == "revit_export":
            result = _process_revit_export(db, path, entry, dry_run=dry_run)
            source_doc = _register_source_document(
                db=db,
                path=path,
                landing_root=landing_root,
                entry=entry,
                manifest_binding=manifest_binding,
                file_type=file_type,
                document_category="revit_export",
                project_id=result.get("project_id"),
                client_id=None,
                dry_run=dry_run,
            )
            counts.update(result)
            counts["source_document"] = source_doc
            if entry.batch_id:
                counts["batch_id"] = entry.batch_id
            if entry.export_mode:
                counts["export_mode"] = entry.export_mode
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status="completed",
                message="Revit export processed" if not dry_run else "Revit export validated for dry run",
                required=entry.required,
                counts=counts,
                warnings=warnings,
            )

        if file_type == "owner_requirements":
            result = _process_owner_requirements(db, path, entry, manifest_binding, dry_run=dry_run)
            source_doc = _register_source_document(
                db=db,
                path=path,
                landing_root=landing_root,
                entry=entry,
                manifest_binding=manifest_binding,
                file_type=file_type,
                document_category="owner_requirements",
                project_id=None,
                client_id=result.get("client_id"),
                dry_run=dry_run,
            )
            counts.update(result)
            counts["source_document"] = source_doc
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status="completed",
                message="Owner requirements processed" if not dry_run else "Owner requirements validated for dry run",
                required=entry.required,
                counts=counts,
                warnings=warnings + result.get("warnings", []),
            )

        if file_type in DOCUMENT_FILE_TYPES:
            result = _process_pdf_document(
                db=db,
                path=path,
                landing_root=landing_root,
                entry=entry,
                manifest_binding=manifest_binding,
                dry_run=dry_run,
            )
            counts.update(result)
            status = "completed"
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status=status,
                message="PDF document indexed" if not dry_run else "PDF document validated for dry run",
                required=entry.required,
                counts=counts,
                warnings=warnings + result.get("warnings", []),
            )

        if file_type == "project_extract":
            summary = summarize_project_extract(
                extract_path=path,
                landing_root=landing_root,
                dry_run=dry_run,
                client_code=entry.client_code or _clean(manifest_binding.get("client_code")),
                project_title=entry.project_title or _clean(manifest_binding.get("project_title")),
            )
            counts["created"] = {"project_extract_summaries": 0 if dry_run else 1}
            counts["summary"] = summary
            if summary.get("parse_error"):
                warnings.append(str(summary["parse_error"]))
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status="completed",
                message="Project extract summarized" if not dry_run else "Project extract summary previewed",
                required=entry.required,
                counts=counts,
                warnings=warnings,
            )

        if file_type == "binding":
            binding = _load_binding_file(path)
            report = _apply_project_binding(db, {**manifest_binding, **binding}, dry_run=dry_run)
            counts["updated"] = {"project_bindings": report["updated"]}
            counts["project_ids"] = sorted(report["project_ids"])
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status="completed",
                message="Binding applied" if not dry_run else "Binding validated for dry run",
                required=entry.required,
                counts=counts,
                warnings=warnings + report["warnings"],
                errors=report["errors"],
            )

        message = "File type is unknown; no parser was run"
        return LandingFileReport(
            path=entry.path,
            type="unknown",
            status="failed" if entry.required else "skipped",
            message=message,
            required=entry.required,
            counts=counts,
            warnings=warnings + ([] if entry.required else [message]),
            errors=[message] if entry.required else [],
        )
    except Exception as exc:  # noqa: BLE001
        if not dry_run:
            db.rollback()
        message = f"{file_type} ingestion failed: {exc}"
        if not entry.required:
            return LandingFileReport(
                path=entry.path,
                type=file_type,
                status="skipped",
                message=message,
                required=entry.required,
                counts=counts,
                warnings=warnings + [message],
                errors=[],
            )
        return LandingFileReport(
            path=entry.path,
            type=file_type,
            status="failed",
            message=message,
            required=entry.required,
            counts=counts,
            warnings=warnings,
            errors=[message],
        )


def _process_revit_export(
    db: Session,
    path: Path,
    entry: LandingFileEntry,
    *,
    dry_run: bool,
) -> dict[str, Any]:
    if path.suffix.lower() != ".json":
        raise ValueError("Revit export files must be JSON")
    export_type = (entry.discipline or DEFAULT_EXPORT_TYPE).lower()
    if export_type in {"mep", "multi"}:
        export_type = "all"

    if dry_run:
        return {"created": {"exports": 0, "elements": 0, "issues": 0}}

    result = ingest_export(
        db=db,
        json_path=path,
        export_type=export_type,
        original_filename=path.name,
        organization_name=settings.default_organization,
    )
    return {
        "project_id": result["project_id"],
        "export_id": result["export_id"],
        "created": {
            "exports": 1,
            "models": 1,
            "elements": int(result.get("element_count", 0)),
            "issues": int(result.get("issue_count", 0)),
        },
    }


ClientResolution = tuple[str | None, str, str | None, str | None]
# (client_code, resolution_source, warning, blocker_message)


def resolve_client_code_for_owner_reqs(
    db: Session,
    manifest_binding: dict[str, Any],
    project_folder_name: str | None,
    manifest_entries: list[LandingFileEntry] | None = None,
) -> ClientResolution:
    """Shared client resolution for owner requirements.

    Priority:
      1. First manifest entry of type owner_requirements with client_code
      2. Manifest/project binding client_code
      3. Project matched by binding project_title (single, with client_id)
      4. Project matched by folder name (single, with client_id)

    Returns (client_code, resolution_source, warning, blocker_message).
    """
    # Priority 1: check first owner_requirements entry
    if manifest_entries:
        for entry in manifest_entries:
            if entry.type == "owner_requirements" and entry.client_code:
                return (entry.client_code, "entry_client_code", None, None)

    # Priority 2: manifest/project binding client_code
    binding_client = _clean(manifest_binding.get("client_code"))
    if binding_client:
        return (binding_client, "binding_client_code", None, None)

    # Priority 3: project matched by binding project_title
    project_title = _clean(manifest_binding.get("project_title"))
    if project_title:
        projects = _find_projects(db, project_title)
        if len(projects) == 1 and projects[0].client_id:
            client_obj = db.get(Client, projects[0].client_id)
            if client_obj:
                warning = f"Using client binding from project '{project_title}': {client_obj.code}"
                return (client_obj.code, "project_title_match", warning, None)
        if len(projects) > 1:
            blocker = (
                f"Multiple projects match manifest title '{project_title}'. "
                "Bind explicitly via the project bind endpoint."
            )
            return (None, "none", None, blocker)

    # Priority 4: project matched by folder name
    if project_folder_name:
        project = db.execute(
            select(Project).where(Project.project_name == project_folder_name)
        ).scalar_one_or_none()
        if project is not None and project.client_id:
            client_obj = db.get(Client, project.client_id)
            if client_obj:
                warning = f"Using client binding from folder '{project_folder_name}': {client_obj.code}"
                return (client_obj.code, "folder_match", warning, None)

    blocker = (
        "Owner requirements need client binding. "
        "Bind project to client via the landing bind endpoint "
        "or add client_code to the manifest entry/binding."
    )
    return (None, "none", None, blocker)


def _process_owner_requirements(
    db: Session,
    path: Path,
    entry: LandingFileEntry,
    manifest_binding: dict[str, Any],
    *,
    dry_run: bool,
) -> dict[str, Any]:
    if path.suffix.lower() not in {".xlsx", ".xlsm"}:
        raise ValueError("Owner requirements files must be XLSX/XLSM")

    client_code, resolution_source, warning, blocker = resolve_client_code_for_owner_reqs(
        db=db,
        manifest_binding=manifest_binding,
        project_folder_name=entry.path.split("/", 1)[0] if "/" in entry.path else None,
        manifest_entries=None,
    )

    if blocker:
        raise ValueError(blocker)

    warnings: list[str] = []
    if warning:
        warnings.append(warning)

    client = _get_client_by_code(db, client_code)
    if client is None:
        raise ValueError(f"Client not found for code {client_code}")

    if dry_run:
        return {"client_id": client.id, "created": {"requirements": 0}, "updated": {"requirements": 0}, "warnings": warnings}

    result = ingest_requirements_file(
        db=db,
        client=client,
        xlsx_path=path,
        original_filename=path.name,
    )
    db.commit()
    return {
        "client_id": client.id,
        "source_file_id": result["source_file_id"],
        "created": {"requirements": int(result.get("row_count_new", 0))},
        "updated": {
            "requirements": int(result.get("row_count_updated", 0)),
            "requirements_deactivated": int(result.get("row_count_deactivated", 0)),
        },
        "warnings": warnings,
    }


def _process_pdf_document(
    *,
    db: Session,
    path: Path,
    landing_root: Path,
    entry: LandingFileEntry,
    manifest_binding: dict[str, Any],
    dry_run: bool,
) -> dict[str, Any]:
    if path.suffix.lower() != ".pdf":
        raise ValueError("PDF document entries must use .pdf files")

    relative_path = str(path.relative_to(landing_root)).replace("\\", "/")
    project_folder = relative_path.split("/", 1)[0] if "/" in relative_path else None
    project = find_project_for_document(
        db,
        project_title=entry.project_title or _clean(manifest_binding.get("project_title")),
        project_folder=project_folder,
    )
    record = build_pdf_document_record(path, relative_path, include_text_preview=False)
    checksum = entry.checksum_sha256 or _sha256_file(path)
    file_size = entry.file_size_bytes if entry.file_size_bytes is not None else path.stat().st_size
    page_count = entry.page_count if entry.page_count is not None else record.get("page_count")
    file_type = entry.type if entry.type in DOCUMENT_FILE_TYPES else record["file_type"]
    document_category = entry.document_category or {
        "drawing_pdf": "drawing",
        "specification_pdf": "specification",
        "pdf_document": "document",
    }.get(file_type)
    values = {
        "project_id": project.id if project else None,
        "client_id": project.client_id if project else None,
        "project_folder": project_folder,
        "relative_path": relative_path,
        "file_name": path.name,
        "file_ext": path.suffix.lower(),
        "file_type": file_type,
        "document_category": document_category,
        "discipline": entry.discipline or record.get("discipline") or infer_discipline_from_path_or_name(path),
        "sheet_number": entry.sheet_number or record.get("sheet_number") or infer_sheet_number(path.name),
        "sheet_title": entry.sheet_title or record.get("sheet_title"),
        "spec_section": entry.spec_section or record.get("spec_section") or infer_spec_section(path.name),
        "spec_title": entry.spec_title or record.get("spec_title"),
        "page_count": page_count,
        "file_size_bytes": file_size,
        "checksum_sha256": checksum,
        "manifest_path": entry.path,
        "source_system": entry.source_system or "landing",
        "ingestion_status": "indexed",
        "evidence_status": "candidate",
        "metadata_json": {
            **entry.metadata,
            **record.get("metadata", {}),
            "required": entry.required,
            "official_evidence": False,
            "note": "Indexed landing document; not official evidence until linked by a deterministic workflow.",
        },
    }
    persistence = register_landing_document(db, values=values, dry_run=dry_run)
    return {
        "project_id": project.id if project else None,
        "document_id": persistence["document_id"],
        "created": {"landing_documents": 0 if dry_run or not persistence["created"] else 1},
        "updated": {"landing_documents": 0 if dry_run or not persistence["updated"] else 1},
        "pages": int(page_count or 0),
        "warnings": record.get("warnings", []),
    }


def _register_source_document(
    *,
    db: Session,
    path: Path,
    landing_root: Path,
    entry: LandingFileEntry,
    manifest_binding: dict[str, Any],
    file_type: str,
    document_category: str,
    project_id: int | None,
    client_id: int | None,
    dry_run: bool,
) -> dict[str, Any]:
    relative_path = str(path.relative_to(landing_root)).replace("\\", "/")
    project_folder = relative_path.split("/", 1)[0] if "/" in relative_path else None
    project = db.get(Project, project_id) if project_id else find_project_for_document(
        db,
        project_title=entry.project_title or _clean(manifest_binding.get("project_title")),
        project_folder=project_folder,
    )
    values = {
        "project_id": project.id if project else None,
        "client_id": client_id or (project.client_id if project else None),
        "project_folder": project_folder,
        "relative_path": relative_path,
        "file_name": path.name,
        "file_ext": path.suffix.lower(),
        "file_type": file_type,
        "document_category": document_category,
        "discipline": entry.discipline or infer_discipline_from_path_or_name(path),
        "file_size_bytes": path.stat().st_size,
        "checksum_sha256": entry.checksum_sha256 or _sha256_file(path),
        "manifest_path": entry.path,
        "source_system": entry.source_system or "landing",
        "ingestion_status": "indexed",
        "evidence_status": "candidate",
        "metadata_json": {
            **entry.metadata,
            "required": entry.required,
            "official_evidence": False,
            "note": "Indexed source file; official readiness comes from deterministic ingestion outputs.",
        },
    }
    return register_landing_document(db, values=values, dry_run=dry_run)


def _apply_project_binding(
    db: Session,
    binding: dict[str, Any],
    *,
    dry_run: bool,
) -> dict[str, Any]:
    warnings: list[str] = []
    errors: list[str] = []
    updated = 0
    project_ids: set[int] = set()

    project_title = _clean(binding.get("project_title"))
    client_code = _clean(binding.get("client_code"))
    client_name = _clean(binding.get("client_name")) or _clean(binding.get("owner_name"))
    current_milestone = _clean(binding.get("current_milestone"))
    project_stage = _clean(binding.get("project_stage"))
    next_milestone = _clean(binding.get("next_milestone"))
    due_date = _clean(binding.get("due_date"))

    if not project_title and not client_code and not client_name:
        return {"warnings": warnings, "errors": errors, "updated": updated, "project_ids": project_ids}

    client = _get_or_create_client(db, client_code=client_code, client_name=client_name, dry_run=dry_run)
    if (client_code or client_name) and client is None and dry_run:
        warnings.append(f"Dry run would create/link client {client_code or client_name}.")

    projects = _find_projects(db, project_title)
    if project_title and not projects:
        warnings.append(f"No project matched binding project_title={project_title}")

    for project in projects:
        project_ids.add(project.id)
        if dry_run:
            continue
        changed = False
        if client is not None and project.client_id != client.id:
            project.client_id = client.id
            project.client_name = client.display_name
            changed = True
        milestone = current_milestone or project_stage
        if milestone and project.phase != milestone:
            project.phase = milestone
            changed = True
        if changed:
            updated += 1
    if not dry_run and updated:
        db.commit()

    if next_milestone:
        warnings.append("next_milestone is captured in manifest only; Project schema does not persist it yet.")
    if due_date:
        warnings.append("due_date is captured in manifest only; Project schema does not persist it yet.")

    return {"warnings": warnings, "errors": errors, "updated": updated, "project_ids": project_ids}


def _find_projects(db: Session, project_title: str | None) -> list[Project]:
    if not project_title:
        return []
    exact = db.execute(
        select(Project).where(Project.project_title == project_title)
    ).scalars().all()
    if exact:
        return list(exact)

    token = f"%{project_title}%"
    reverse_token = f"%{project_title.replace('_', ' ')}%"
    return list(
        db.execute(
            select(Project).where(
                (Project.project_title.ilike(token))
                | (Project.project_name.ilike(token))
                | (Project.project_title.ilike(reverse_token))
                | (Project.project_name.ilike(reverse_token))
            )
        ).scalars()
    )


def _get_client_by_code(db: Session, code: str | None) -> Client | None:
    if not code:
        return None
    normalized = code.strip().upper().replace(" ", "_")
    return db.execute(select(Client).where(Client.code == normalized)).scalar_one_or_none()


def _get_or_create_client(
    db: Session,
    *,
    client_code: str | None,
    client_name: str | None,
    dry_run: bool,
) -> Client | None:
    if not client_code and not client_name:
        return None
    normalized = _normalize_client_code(client_code or client_name or "")
    client = db.execute(select(Client).where(Client.code == normalized)).scalar_one_or_none()
    if client is not None or dry_run:
        return client
    organization_id = db.execute(select(Project.organization_id).limit(1)).scalar_one_or_none() or 1
    client = Client(
        organization_id=organization_id,
        code=normalized,
        display_name=(client_name or normalized.replace("_", " ")).strip(),
    )
    db.add(client)
    db.flush()
    return client


def _load_binding_file(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        raise ValueError("Binding JSON must contain an object")
    return payload


def _merge_counts(target: dict[str, int], source: Any) -> None:
    if not isinstance(source, dict):
        return
    for key, value in source.items():
        if isinstance(value, int):
            target[key] += value


def _processed_key(file_type: str) -> str:
    names = {
        "revit_export": "revit_exports",
        "owner_requirements": "owner_requirements",
        "drawing_pdf": "drawing_pdfs",
        "specification_pdf": "specification_pdfs",
        "pdf_document": "pdf_documents",
        "project_extract": "project_extracts",
        "binding": "bindings",
        "unknown": "unknown",
    }
    return names.get(file_type, file_type)


def _entry_binding(entry: LandingFileEntry) -> dict[str, Any]:
    binding: dict[str, Any] = {}
    if entry.project_title:
        binding["project_title"] = entry.project_title
    if entry.client_code:
        binding["client_code"] = entry.client_code
    if entry.metadata.get("client_name"):
        binding["client_name"] = entry.metadata["client_name"]
    if entry.metadata.get("owner_name"):
        binding["owner_name"] = entry.metadata["owner_name"]
    if entry.metadata.get("current_milestone"):
        binding["current_milestone"] = entry.metadata["current_milestone"]
    if entry.metadata.get("next_milestone"):
        binding["next_milestone"] = entry.metadata["next_milestone"]
    if entry.metadata.get("due_date"):
        binding["due_date"] = entry.metadata["due_date"]
    return binding


def _clean(value: Any) -> str | None:
    if value is None:
        return None
    text = str(value).strip()
    return text or None


def _normalize_client_code(value: str) -> str:
    code = value.strip().upper().replace("&", "AND")
    code = "".join(ch if ch.isalnum() else "_" for ch in code)
    while "__" in code:
        code = code.replace("__", "_")
    return code.strip("_") or "OWNER"


def _sha256_file(path: Path, chunk: int = 1024 * 1024) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        while True:
            data = handle.read(chunk)
            if not data:
                break
            digest.update(data)
    return digest.hexdigest()
