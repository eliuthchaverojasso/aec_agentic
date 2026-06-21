from datetime import date, datetime
from typing import Any

from sqlalchemy import (
    BigInteger,
    Boolean,
    CheckConstraint,
    Date,
    DateTime,
    ForeignKey,
    Index,
    Integer,
    Numeric,
    Float,
    String,
    Text,
    UniqueConstraint,
    func,
)
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import DeclarativeBase, Mapped, mapped_column, relationship


class Base(DeclarativeBase):
    pass


class Organization(Base):
    __tablename__ = "organization"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False, unique=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    projects: Mapped[list["Project"]] = relationship(back_populates="organization")
    clients: Mapped[list["Client"]] = relationship(back_populates="organization")


class Client(Base):
    __tablename__ = "client"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    organization_id: Mapped[int] = mapped_column(ForeignKey("organization.id", ondelete="CASCADE"))
    code: Mapped[str] = mapped_column(String(100), nullable=False)
    display_name: Mapped[str] = mapped_column(String(255), nullable=False)
    sharepoint_path: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("organization_id", "code"),)

    organization: Mapped["Organization"] = relationship(back_populates="clients")
    requirements: Mapped[list["Requirement"]] = relationship(back_populates="client", cascade="all, delete-orphan")
    source_files: Mapped[list["RequirementSourceFile"]] = relationship(
        back_populates="client", cascade="all, delete-orphan"
    )


class Project(Base):
    __tablename__ = "project"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    organization_id: Mapped[int] = mapped_column(ForeignKey("organization.id", ondelete="CASCADE"))
    client_id: Mapped[int | None] = mapped_column(
        ForeignKey("client.id", ondelete="SET NULL"), nullable=True
    )
    project_title: Mapped[str] = mapped_column(String(500), nullable=False)
    project_code: Mapped[str | None] = mapped_column(String(100), nullable=True)
    project_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    job_number: Mapped[str | None] = mapped_column(String(100), nullable=True)
    revit_version: Mapped[str | None] = mapped_column(String(20), nullable=True)
    client_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    location: Mapped[str | None] = mapped_column(String(255), nullable=True)
    jurisdiction: Mapped[str | None] = mapped_column(String(255), nullable=True)
    phase: Mapped[str | None] = mapped_column(String(100), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("organization_id", "project_title"),)

    organization: Mapped["Organization"] = relationship(back_populates="projects")
    models: Mapped[list["Model"]] = relationship(back_populates="project", cascade="all, delete-orphan")


class Model(Base):
    __tablename__ = "model"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    revit_file_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    revit_version: Mapped[str | None] = mapped_column(String(20), nullable=True)
    discipline: Mapped[str | None] = mapped_column(String(100), nullable=True)
    model_type: Mapped[str | None] = mapped_column(String(100), nullable=True)
    last_sync_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    exported_by: Mapped[str | None] = mapped_column(String(255), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("project_id", "revit_file_name", "discipline"),)

    project: Mapped["Project"] = relationship(back_populates="models")
    exports: Mapped[list["Export"]] = relationship(back_populates="model", cascade="all, delete-orphan")


class Export(Base):
    __tablename__ = "export"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    model_id: Mapped[int] = mapped_column(ForeignKey("model.id", ondelete="CASCADE"))
    export_type: Mapped[str] = mapped_column(String(50), nullable=False)
    file_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    file_size_bytes: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    element_count: Mapped[int | None] = mapped_column(Integer, nullable=True)
    status: Mapped[str] = mapped_column(String(50), nullable=False, default="pending")
    started_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    completed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    duration_seconds: Mapped[float | None] = mapped_column(Numeric(10, 2), nullable=True)
    error_message: Mapped[str | None] = mapped_column(Text, nullable=True)

    __table_args__ = (
        CheckConstraint(
            "status IN ('pending','in_progress','completed','failed','warning')",
            name="chk_export_status",
        ),
    )

    model: Mapped["Model"] = relationship(back_populates="exports")
    sync_logs: Mapped[list["SyncLog"]] = relationship(back_populates="export", cascade="all, delete-orphan")
    elements: Mapped[list["Element"]] = relationship(back_populates="export", cascade="all, delete-orphan")
    issues: Mapped[list["Issue"]] = relationship(back_populates="export", cascade="all, delete-orphan")


class SyncLog(Base):
    __tablename__ = "sync_log"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    export_id: Mapped[int] = mapped_column(ForeignKey("export.id", ondelete="CASCADE"))
    step: Mapped[str] = mapped_column(String(100), nullable=False)
    status: Mapped[str] = mapped_column(String(50), nullable=False)
    started_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    completed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    duration_seconds: Mapped[float | None] = mapped_column(Numeric(10, 2), nullable=True)
    message: Mapped[str | None] = mapped_column(Text, nullable=True)

    export: Mapped["Export"] = relationship(back_populates="sync_logs")


class Element(Base):
    __tablename__ = "element"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    unique_id: Mapped[str] = mapped_column(String(100), nullable=False)
    element_id: Mapped[int] = mapped_column(BigInteger, nullable=False)
    model_id: Mapped[int] = mapped_column(ForeignKey("model.id", ondelete="CASCADE"))
    export_id: Mapped[int] = mapped_column(ForeignKey("export.id", ondelete="CASCADE"))
    category: Mapped[str | None] = mapped_column(String(100), nullable=True)
    name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    family: Mapped[str | None] = mapped_column(String(500), nullable=True)
    type: Mapped[str | None] = mapped_column(String(500), nullable=True)
    level: Mapped[str | None] = mapped_column(String(100), nullable=True)
    instance_parameters: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    type_parameters: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("unique_id", "export_id"),)

    export: Mapped["Export"] = relationship(back_populates="elements")


class Rule(Base):
    __tablename__ = "rule"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    rule_code: Mapped[str] = mapped_column(String(20), nullable=False, unique=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    description: Mapped[str | None] = mapped_column(Text, nullable=True)
    discipline: Mapped[str | None] = mapped_column(String(100), nullable=True)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    check_type: Mapped[str | None] = mapped_column(String(50), nullable=True)
    active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    version: Mapped[str] = mapped_column(String(20), nullable=False, default="1.0")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class Issue(Base):
    __tablename__ = "issue"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    organization_id: Mapped[int] = mapped_column(ForeignKey("organization.id", ondelete="CASCADE"))
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    model_id: Mapped[int] = mapped_column(ForeignKey("model.id", ondelete="CASCADE"))
    export_id: Mapped[int] = mapped_column(ForeignKey("export.id", ondelete="CASCADE"))
    element_unique_id: Mapped[str | None] = mapped_column(String(100), nullable=True)
    element_db_id: Mapped[int | None] = mapped_column(
        BigInteger, ForeignKey("element.id", ondelete="SET NULL"), nullable=True
    )
    rule_id: Mapped[int | None] = mapped_column(ForeignKey("rule.id"), nullable=True)
    rule_code: Mapped[str | None] = mapped_column(String(20), nullable=True)
    issue_type: Mapped[str | None] = mapped_column(String(50), nullable=True)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    status: Mapped[str] = mapped_column(String(50), nullable=False, default="open")
    source: Mapped[str] = mapped_column(String(50), nullable=False, default="automated")
    message: Mapped[str | None] = mapped_column(Text, nullable=True)
    traceability: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    assigned_to_user_id: Mapped[int | None] = mapped_column(Integer, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    due_date: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    reviewed_by_user_id: Mapped[int | None] = mapped_column(Integer, nullable=True)
    reviewed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    resolution_notes: Mapped[str | None] = mapped_column(Text, nullable=True)

    __table_args__ = (
        CheckConstraint(
            "severity IN ('low','medium','high','critical')",
            name="chk_issue_severity",
        ),
        CheckConstraint(
            "status IN ('open','in_review','reviewed','closed','reopened')",
            name="chk_issue_status",
        ),
        CheckConstraint("source IN ('automated','manual')", name="chk_issue_source"),
    )

    export: Mapped["Export"] = relationship(back_populates="issues")


class AppUser(Base):
    __tablename__ = "app_user"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    name: Mapped[str] = mapped_column(String(255), nullable=False)
    email: Mapped[str | None] = mapped_column(String(255), unique=True, nullable=True)
    role: Mapped[str] = mapped_column(String(100), nullable=False, default="engineer")
    password_hash: Mapped[str | None] = mapped_column(Text, nullable=True)
    auth_provider: Mapped[str] = mapped_column(String(30), nullable=False, default="local")
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    is_locked: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    failed_login_attempts: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    last_login_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    password_changed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    must_change_password: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), onupdate=func.now())


class RequirementSourceFile(Base):
    __tablename__ = "requirement_source_file"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    client_id: Mapped[int] = mapped_column(ForeignKey("client.id", ondelete="CASCADE"))
    original_filename: Mapped[str] = mapped_column(String(500), nullable=False)
    file_hash: Mapped[str] = mapped_column(String(64), nullable=False)
    row_count_raw: Mapped[int | None] = mapped_column(Integer, nullable=True)
    row_count_loaded: Mapped[int | None] = mapped_column(Integer, nullable=True)
    row_count_skipped: Mapped[int | None] = mapped_column(Integer, nullable=True)
    export_date: Mapped[date | None] = mapped_column(Date, nullable=True)
    ingested_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("client_id", "file_hash"),)

    client: Mapped["Client"] = relationship(back_populates="source_files")
    requirements: Mapped[list["Requirement"]] = relationship(back_populates="source_file")


class Requirement(Base):
    __tablename__ = "requirement"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    client_id: Mapped[int] = mapped_column(ForeignKey("client.id", ondelete="CASCADE"))
    source_file_id: Mapped[int | None] = mapped_column(
        ForeignKey("requirement_source_file.id", ondelete="SET NULL"), nullable=True
    )
    discipline: Mapped[str] = mapped_column(String(50), nullable=False)
    category: Mapped[str | None] = mapped_column(String(255), nullable=True)
    requirement_text: Mapped[str] = mapped_column(Text, nullable=False)
    content_hash: Mapped[str] = mapped_column(String(64), nullable=False)
    owner_status: Mapped[str | None] = mapped_column(String(50), nullable=True)
    resource: Mapped[str | None] = mapped_column(String(500), nullable=True)
    links: Mapped[str | None] = mapped_column(Text, nullable=True)
    modified_by: Mapped[str | None] = mapped_column(String(255), nullable=True)
    date_updated: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    sharepoint_path: Mapped[str | None] = mapped_column(Text, nullable=True)
    is_actionable: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    is_active: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)
    first_seen_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    last_seen_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (UniqueConstraint("client_id", "content_hash"),)

    client: Mapped["Client"] = relationship(back_populates="requirements")
    source_file: Mapped["RequirementSourceFile | None"] = relationship(back_populates="requirements")


class RequirementCompliance(Base):
    __tablename__ = "requirement_compliance"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    requirement_id: Mapped[int] = mapped_column(ForeignKey("requirement.id", ondelete="CASCADE"))
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    model_id: Mapped[int | None] = mapped_column(
        ForeignKey("model.id", ondelete="CASCADE"), nullable=True
    )
    status: Mapped[str] = mapped_column(String(30), nullable=False, default="not_evaluated")
    evidence: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    evaluated_by: Mapped[str | None] = mapped_column(String(50), nullable=True)
    evaluated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    notes: Mapped[str | None] = mapped_column(Text, nullable=True)

    __table_args__ = (
        UniqueConstraint("requirement_id", "project_id", "model_id"),
        CheckConstraint(
            "status IN ('compliant','non_compliant','not_evaluated','not_applicable','needs_review')",
            name="chk_compliance_status",
        ),
    )


class RequirementEvidence(Base):
    __tablename__ = "requirement_evidence"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    requirement_id: Mapped[int] = mapped_column(ForeignKey("requirement.id", ondelete="CASCADE"))
    evidence_type: Mapped[str] = mapped_column(String(30), nullable=False)
    evidence_status: Mapped[str] = mapped_column(String(30), nullable=False)
    source_ref: Mapped[str | None] = mapped_column(Text, nullable=True)
    element_unique_id: Mapped[str | None] = mapped_column(String(100), nullable=True)
    sheet_number: Mapped[str | None] = mapped_column(String(100), nullable=True)
    spec_section: Mapped[str | None] = mapped_column(String(100), nullable=True)
    confidence: Mapped[float | None] = mapped_column(Numeric(5, 2), nullable=True)
    metadata_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        CheckConstraint(
            "evidence_type IN ('model','sheet','spec','manual','hybrid')",
            name="chk_requirement_evidence_type",
        ),
        CheckConstraint(
            "evidence_status IN ('covered','missing','needs_review','blocked','not_applicable')",
            name="chk_requirement_evidence_status",
        ),
    )

    @property
    def review_status(self) -> str:
        metadata = self.metadata_json or {}
        raw_status = metadata.get("review_status")
        if isinstance(raw_status, str) and raw_status in {"candidate", "accepted", "rejected", "needs_review", "none"}:
            return raw_status
        if self.evidence_status == "covered":
            return "accepted"
        if self.evidence_status == "needs_review":
            return "needs_review"
        if self.evidence_status == "blocked":
            return "needs_review"
        return "none"

    @property
    def source_label(self) -> str | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("source_label")
        if isinstance(raw, str) and raw.strip():
            return raw.strip()
        return self.source_ref

    @property
    def review_note(self) -> str | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("review_note")
        return raw.strip() if isinstance(raw, str) and raw.strip() else None

    @property
    def reviewed_by(self) -> str | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("reviewed_by")
        if isinstance(raw, str) and raw.strip():
            return raw.strip()
        raw_id = metadata.get("reviewed_by_user_id")
        return str(raw_id) if raw_id is not None else None

    @property
    def reviewed_at(self) -> datetime | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("reviewed_at")
        if raw is None:
            return None
        if isinstance(raw, datetime):
            return raw
        if isinstance(raw, str):
            normalized = raw.replace("Z", "+00:00")
            try:
                return datetime.fromisoformat(normalized)
            except ValueError:
                return None
        return None

    @property
    def document_id(self) -> int | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("document_id")
        if isinstance(raw, int):
            return raw
        if isinstance(raw, str) and raw.isdigit():
            return int(raw)
        return None

    @property
    def sheet_id(self) -> int | None:
        metadata = self.metadata_json or {}
        raw = metadata.get("sheet_id")
        if isinstance(raw, int):
            return raw
        if isinstance(raw, str) and raw.isdigit():
            return int(raw)
        return None

    @property
    def model_element_id(self) -> str | None:
        if isinstance(self.element_unique_id, str) and self.element_unique_id.strip():
            return self.element_unique_id.strip()
        metadata = self.metadata_json or {}
        raw = metadata.get("model_element_id")
        return str(raw) if raw is not None else None


class LandingDocument(Base):
    __tablename__ = "landing_document"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    project_id: Mapped[int | None] = mapped_column(ForeignKey("project.id", ondelete="SET NULL"), nullable=True)
    client_id: Mapped[int | None] = mapped_column(ForeignKey("client.id", ondelete="SET NULL"), nullable=True)
    project_folder: Mapped[str | None] = mapped_column(String(500), nullable=True)
    relative_path: Mapped[str] = mapped_column(Text, nullable=False)
    file_name: Mapped[str] = mapped_column(String(500), nullable=False)
    file_ext: Mapped[str] = mapped_column(String(20), nullable=False)
    file_type: Mapped[str] = mapped_column(String(50), nullable=False)
    document_category: Mapped[str | None] = mapped_column(String(50), nullable=True)
    discipline: Mapped[str | None] = mapped_column(String(100), nullable=True)
    sheet_number: Mapped[str | None] = mapped_column(String(100), nullable=True)
    sheet_title: Mapped[str | None] = mapped_column(String(500), nullable=True)
    spec_section: Mapped[str | None] = mapped_column(String(100), nullable=True)
    spec_title: Mapped[str | None] = mapped_column(String(500), nullable=True)
    page_count: Mapped[int | None] = mapped_column(Integer, nullable=True)
    file_size_bytes: Mapped[int | None] = mapped_column(BigInteger, nullable=True)
    checksum_sha256: Mapped[str | None] = mapped_column(String(64), nullable=True)
    manifest_path: Mapped[str | None] = mapped_column(Text, nullable=True)
    source_system: Mapped[str] = mapped_column(String(100), nullable=False, default="landing")
    ingestion_status: Mapped[str] = mapped_column(String(50), nullable=False, default="indexed")
    indexed_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    processed_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    evidence_status: Mapped[str] = mapped_column(String(50), nullable=False, default="candidate")
    metadata_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)

    __table_args__ = (
        UniqueConstraint("relative_path", "checksum_sha256", name="uq_landing_document_path_hash"),
        Index("idx_landing_document_project_category", "project_id", "document_category"),
        Index("idx_landing_document_type", "file_type"),
    )


class DrawingSheet(Base):
    __tablename__ = "drawing_sheet"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    document_id: Mapped[int] = mapped_column(ForeignKey("landing_document.id", ondelete="CASCADE"))
    project_id: Mapped[int | None] = mapped_column(ForeignKey("project.id", ondelete="SET NULL"), nullable=True)
    sheet_number: Mapped[str] = mapped_column(String(100), nullable=False)
    sheet_title: Mapped[str | None] = mapped_column(String(500), nullable=True)
    discipline: Mapped[str | None] = mapped_column(String(100), nullable=True)
    page_number: Mapped[int | None] = mapped_column(Integer, nullable=True)
    metadata_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)

    __table_args__ = (
        UniqueConstraint("document_id", "sheet_number", name="uq_drawing_sheet_document_sheet"),
        Index("idx_drawing_sheet_project", "project_id", "sheet_number"),
    )


class DocumentTextSnippet(Base):
    __tablename__ = "document_text_snippet"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    document_id: Mapped[int] = mapped_column(ForeignKey("landing_document.id", ondelete="CASCADE"))
    page_number: Mapped[int | None] = mapped_column(Integer, nullable=True)
    text_preview: Mapped[str] = mapped_column(Text, nullable=False)
    extraction_method: Mapped[str] = mapped_column(String(100), nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("document_id", "page_number", name="uq_document_text_snippet_page"),
        Index("idx_document_text_snippet_document", "document_id"),
    )


class ReadinessSnapshot(Base):
    __tablename__ = "readiness_snapshot"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    export_id: Mapped[int | None] = mapped_column(ForeignKey("export.id", ondelete="SET NULL"), nullable=True)
    overall_score: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    label: Mapped[str] = mapped_column(String(50), nullable=False)
    requirement_coverage_score: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    qaqc_health_score: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    sync_freshness_score: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    gap_summary: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class TradeReadinessSnapshot(Base):
    __tablename__ = "trade_readiness_snapshot"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    snapshot_id: Mapped[int] = mapped_column(ForeignKey("readiness_snapshot.id", ondelete="CASCADE"))
    discipline: Mapped[str] = mapped_column(String(100), nullable=False)
    score: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    requirements_total: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    requirements_covered: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    missing_requirements: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    needs_review: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    open_issues: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    critical_gaps: Mapped[int] = mapped_column(Integer, nullable=False, default=0)


class ReadinessAction(Base):
    __tablename__ = "readiness_action"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    requirement_id: Mapped[int | None] = mapped_column(
        ForeignKey("requirement.id", ondelete="SET NULL"), nullable=True
    )
    issue_id: Mapped[int | None] = mapped_column(ForeignKey("issue.id", ondelete="SET NULL"), nullable=True)
    rule_code: Mapped[str | None] = mapped_column(String(30), nullable=True)
    action_type: Mapped[str] = mapped_column(String(100), nullable=False)
    title: Mapped[str] = mapped_column(String(255), nullable=False)
    description: Mapped[str | None] = mapped_column(Text, nullable=True)
    status: Mapped[str] = mapped_column(String(30), nullable=False, default="open")
    priority: Mapped[str] = mapped_column(String(30), nullable=False, default="medium")
    owner: Mapped[str | None] = mapped_column(String(255), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    updated_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        CheckConstraint(
            "status IN ('open','in_review','done','dismissed')",
            name="chk_readiness_action_status",
        ),
        CheckConstraint(
            "priority IN ('low','medium','high','critical')",
            name="chk_readiness_action_priority",
        ),
    )


class RuleExecutionLog(Base):
    __tablename__ = "rule_execution_log"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int | None] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"), nullable=True)
    export_id: Mapped[int | None] = mapped_column(ForeignKey("export.id", ondelete="SET NULL"), nullable=True)
    rule_code: Mapped[str] = mapped_column(String(30), nullable=False)
    status: Mapped[str] = mapped_column(String(30), nullable=False)
    findings_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    duration_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    error_message: Mapped[str | None] = mapped_column(Text, nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())


class SeionPrediction(Base):
    __tablename__ = "seion_prediction"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int | None] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"), nullable=True)
    head_uid: Mapped[str] = mapped_column(Text, nullable=False)
    relation: Mapped[str] = mapped_column(Text, nullable=False)
    tail_uid: Mapped[str] = mapped_column(Text, nullable=False)
    score: Mapped[float] = mapped_column(Float, nullable=False)
    rank: Mapped[int | None] = mapped_column(Integer, nullable=True)
    model_version: Mapped[str] = mapped_column(Text, nullable=False)
    status: Mapped[str] = mapped_column(String(30), nullable=False, default="suggested")
    source: Mapped[str] = mapped_column(Text, nullable=False, default="seion_kge")
    reviewer_note: Mapped[str | None] = mapped_column(Text, nullable=True)
    accepted_by: Mapped[str | None] = mapped_column(Text, nullable=True)
    accepted_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    metadata_json: Mapped[dict[str, Any]] = mapped_column("metadata", JSONB, nullable=False, default=dict)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    updated_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)

    __table_args__ = (
        CheckConstraint(
            "status IN ('suggested','accepted','rejected','stale','superseded')",
            name="chk_seion_prediction_status",
        ),
        Index("idx_seion_prediction_project_status", "project_id", "status"),
        Index("idx_seion_prediction_relation", "relation"),
    )


class PipelineOperationLog(Base):
    __tablename__ = "pipeline_operation_log"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    run_id: Mapped[str | None] = mapped_column(String(64), nullable=True)
    request_id: Mapped[str | None] = mapped_column(String(64), nullable=True)
    project_id: Mapped[int | None] = mapped_column(ForeignKey("project.id", ondelete="SET NULL"), nullable=True)
    project_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    operation_type: Mapped[str] = mapped_column(String(100), nullable=False)
    operation_label: Mapped[str | None] = mapped_column(String(255), nullable=True)
    source: Mapped[str] = mapped_column(String(100), nullable=False, default="backend")
    endpoint: Mapped[str | None] = mapped_column(String(255), nullable=True)
    method: Mapped[str | None] = mapped_column(String(20), nullable=True)
    status: Mapped[str] = mapped_column(String(30), nullable=False, default="started")
    severity: Mapped[str] = mapped_column(String(20), nullable=False, default="info")
    started_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    finished_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    duration_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    actor_type: Mapped[str | None] = mapped_column(String(100), nullable=True)
    actor_label: Mapped[str | None] = mapped_column(String(255), nullable=True)
    landing_root: Mapped[str | None] = mapped_column(Text, nullable=True)
    project_folder_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    file_path_relative: Mapped[str | None] = mapped_column(Text, nullable=True)
    file_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    file_hash: Mapped[str | None] = mapped_column(String(128), nullable=True)
    counts_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    request_summary_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    response_summary_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    warnings_json: Mapped[list[Any] | None] = mapped_column(JSONB, nullable=True)
    errors_json: Mapped[list[Any] | None] = mapped_column(JSONB, nullable=True)
    environment_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    metadata_json: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)

    __table_args__ = (
        Index("idx_pipeline_operation_project_started", "project_id", "started_at"),
        Index("idx_pipeline_operation_type_status", "operation_type", "status"),
        Index("idx_pipeline_operation_run_request", "run_id", "request_id"),
    )


# ---------------------------------------------------------------------------
# Requirement Audit & Evaluation Bundle v1
#
# These tables ingest the deterministic Evaluation Bundle produced by the C#
# engine. They RECORD how each decision was reached and the coherence of the
# requirement set; they never recompute a status. See
# db/migrations/20260615_001_requirement_audit_v1.sql.
# ---------------------------------------------------------------------------


class RequirementAuditRun(Base):
    __tablename__ = "requirement_audit_run"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    export_id: Mapped[int | None] = mapped_column(ForeignKey("export.id", ondelete="SET NULL"), nullable=True)
    source_file_id: Mapped[int | None] = mapped_column(
        ForeignKey("requirement_source_file.id", ondelete="SET NULL"), nullable=True
    )

    run_uid: Mapped[str] = mapped_column(String(64), nullable=False)
    run_status: Mapped[str] = mapped_column(String(30), nullable=False, default="completed")
    as_of: Mapped[datetime] = mapped_column(DateTime(timezone=True), nullable=False)

    schema_version: Mapped[str] = mapped_column(String(50), nullable=False, default="1.0")
    engine_version: Mapped[str | None] = mapped_column(String(50), nullable=True)
    ruleset_version: Mapped[str | None] = mapped_column(String(50), nullable=True)
    taxonomy_version: Mapped[str | None] = mapped_column(String(50), nullable=True)
    score_policy_version: Mapped[str | None] = mapped_column(String(50), nullable=True)

    input_hash: Mapped[str | None] = mapped_column(String(64), nullable=True)
    output_hash: Mapped[str | None] = mapped_column(String(64), nullable=True)

    project_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    model_name: Mapped[str | None] = mapped_column(String(500), nullable=True)
    requirements_file: Mapped[str | None] = mapped_column(String(500), nullable=True)

    requirements_total: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    status_counts: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    coherence_grade: Mapped[str | None] = mapped_column(String(40), nullable=True)
    coherence_findings_total: Mapped[int] = mapped_column(Integer, nullable=False, default=0)

    ingested_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("project_id", "run_uid", name="uq_requirement_audit_run"),
        CheckConstraint(
            "run_status IN ('pending','running','completed','completed_with_warnings','failed')",
            name="chk_requirement_audit_run_status",
        ),
        Index("idx_requirement_audit_run_project", "project_id", "ingested_at"),
    )

    records: Mapped[list["RequirementAuditRecord"]] = relationship(
        back_populates="run", cascade="all, delete-orphan"
    )
    coherence_findings: Mapped[list["RequirementCoherenceFinding"]] = relationship(
        back_populates="run", cascade="all, delete-orphan"
    )


class RequirementAuditRecord(Base):
    __tablename__ = "requirement_audit_record"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    run_id: Mapped[int] = mapped_column(ForeignKey("requirement_audit_run.id", ondelete="CASCADE"))
    requirement_id: Mapped[int | None] = mapped_column(
        ForeignKey("requirement.id", ondelete="SET NULL"), nullable=True
    )
    requirement_uid: Mapped[str | None] = mapped_column(String(255), nullable=True)
    requirement_content_hash: Mapped[str | None] = mapped_column(String(64), nullable=True)

    decision_status: Mapped[str] = mapped_column(String(40), nullable=False)
    lifecycle_status: Mapped[str] = mapped_column(String(40), nullable=False, default="CoherenceChecked")
    requirement_type: Mapped[str | None] = mapped_column(String(120), nullable=True)
    validation_type: Mapped[str | None] = mapped_column(String(40), nullable=True)
    applies: Mapped[bool] = mapped_column(Boolean, nullable=False, default=True)

    rule_applied: Mapped[str | None] = mapped_column(String(120), nullable=True)
    decision_reason: Mapped[str | None] = mapped_column(Text, nullable=True)
    confidence: Mapped[float | None] = mapped_column(Numeric(6, 5), nullable=True)
    direct_evidence_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)
    supporting_evidence_count: Mapped[int] = mapped_column(Integer, nullable=False, default=0)

    source_provenance: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    semantic_ir: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    evidence_policy: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    candidate_funnel: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    coherence_finding_ids: Mapped[list[Any]] = mapped_column(JSONB, nullable=False, default=list)

    next_best_action: Mapped[str | None] = mapped_column(Text, nullable=True)
    record_hash: Mapped[str | None] = mapped_column(String(64), nullable=True)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("run_id", "requirement_uid", name="uq_requirement_audit_record"),
        CheckConstraint(
            "decision_status IN ('Compliant','NonCompliant','NeedsReview','InsufficientData','NotApplicable','Indeterminate')",
            name="chk_requirement_audit_decision_status",
        ),
        Index("idx_requirement_audit_record_run", "run_id"),
        Index("idx_requirement_audit_record_requirement", "requirement_id"),
    )

    run: Mapped["RequirementAuditRun"] = relationship(back_populates="records")
    review_decisions: Mapped[list["RequirementReviewDecision"]] = relationship(
        back_populates="audit_record", cascade="all, delete-orphan"
    )


class RequirementCoherenceFinding(Base):
    __tablename__ = "requirement_coherence_finding"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    run_id: Mapped[int] = mapped_column(ForeignKey("requirement_audit_run.id", ondelete="CASCADE"))
    finding_uid: Mapped[str] = mapped_column(String(255), nullable=False)
    finding_type: Mapped[str] = mapped_column(String(50), nullable=False)
    severity: Mapped[str] = mapped_column(String(20), nullable=False)
    requirement_type: Mapped[str | None] = mapped_column(String(120), nullable=True)
    status: Mapped[str] = mapped_column(String(30), nullable=False, default="open")
    rationale: Mapped[str | None] = mapped_column(Text, nullable=True)
    primary_requirement: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    related_requirement: Mapped[dict[str, Any] | None] = mapped_column(JSONB, nullable=True)
    normalized_values: Mapped[dict[str, Any]] = mapped_column(JSONB, nullable=False, default=dict)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("run_id", "finding_uid", name="uq_requirement_coherence_finding"),
        Index("idx_requirement_coherence_finding_run", "run_id", "severity"),
    )

    run: Mapped["RequirementAuditRun"] = relationship(back_populates="coherence_findings")


class RequirementReviewDecision(Base):
    __tablename__ = "requirement_review_decision"

    id: Mapped[int] = mapped_column(BigInteger, primary_key=True)
    audit_record_id: Mapped[int] = mapped_column(
        ForeignKey("requirement_audit_record.id", ondelete="CASCADE")
    )
    reviewer_user_id: Mapped[int | None] = mapped_column(
        ForeignKey("app_user.id", ondelete="SET NULL"), nullable=True
    )
    reviewer_name: Mapped[str | None] = mapped_column(String(255), nullable=True)
    action: Mapped[str] = mapped_column(String(30), nullable=False)
    previous_status: Mapped[str | None] = mapped_column(String(40), nullable=True)
    resulting_status: Mapped[str | None] = mapped_column(String(40), nullable=True)
    reason: Mapped[str] = mapped_column(Text, nullable=False)
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        CheckConstraint(
            "action IN ('accept','reject','override','request_changes','lock','supersede')",
            name="chk_requirement_review_action",
        ),
        Index("idx_requirement_review_decision_record", "audit_record_id", "created_at"),
    )

    audit_record: Mapped["RequirementAuditRecord"] = relationship(back_populates="review_decisions")


# ---------------------------------------------------------------------------
# Tenant & project authorization (Pending Work Register Item 8)
#
# `organization` is the tenant boundary. `membership` grants a user access to an
# organization (and, transitively, its projects); `project_membership` grants
# access to a single project without org-wide access. Authorization is evaluated
# in app/authz.py.
# ---------------------------------------------------------------------------

_MEMBERSHIP_ROLES = "role IN ('owner','admin','member','viewer')"


class Membership(Base):
    __tablename__ = "membership"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("app_user.id", ondelete="CASCADE"))
    organization_id: Mapped[int] = mapped_column(ForeignKey("organization.id", ondelete="CASCADE"))
    role: Mapped[str] = mapped_column(String(50), nullable=False, default="member")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("user_id", "organization_id", name="uq_membership_user_org"),
        CheckConstraint(_MEMBERSHIP_ROLES, name="chk_membership_role"),
        Index("idx_membership_user", "user_id"),
        Index("idx_membership_org", "organization_id"),
    )


class ProjectMembership(Base):
    __tablename__ = "project_membership"

    id: Mapped[int] = mapped_column(Integer, primary_key=True)
    user_id: Mapped[int] = mapped_column(ForeignKey("app_user.id", ondelete="CASCADE"))
    project_id: Mapped[int] = mapped_column(ForeignKey("project.id", ondelete="CASCADE"))
    role: Mapped[str] = mapped_column(String(50), nullable=False, default="member")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())

    __table_args__ = (
        UniqueConstraint("user_id", "project_id", name="uq_project_membership_user_project"),
        CheckConstraint(_MEMBERSHIP_ROLES, name="chk_project_membership_role"),
        Index("idx_project_membership_user", "user_id"),
        Index("idx_project_membership_project", "project_id"),
    )
