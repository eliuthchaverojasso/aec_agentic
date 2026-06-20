from __future__ import annotations

from fastapi import APIRouter, HTTPException, Query

from app.compliance.service import (
    get_corpus,
    get_status,
    import_nec_corpus,
    list_corpora,
    list_rules,
    preview_nec_loader,
)
from app.schemas import (
    ComplianceCorpusOut,
    ComplianceImportOut,
    ComplianceLoaderPreviewOut,
    ComplianceLoaderPreviewRequest,
    ComplianceRuleOut,
    ComplianceStatusOut,
)

router = APIRouter(tags=["compliance"])


@router.get("/api/v1/compliance/status", response_model=ComplianceStatusOut)
def compliance_status() -> ComplianceStatusOut:
    return get_status()


@router.get("/api/v1/compliance/corpora", response_model=list[ComplianceCorpusOut])
def compliance_corpora() -> list[ComplianceCorpusOut]:
    return list_corpora()


@router.get("/api/v1/compliance/corpora/{corpus_id}", response_model=ComplianceCorpusOut)
def compliance_corpus_detail(corpus_id: int) -> ComplianceCorpusOut:
    corpus = get_corpus(corpus_id)
    if corpus is None:
        raise HTTPException(status_code=404, detail="Compliance corpus not found")
    return corpus


@router.post("/api/v1/compliance/corpora/nec/preview", response_model=ComplianceLoaderPreviewOut)
def preview_nec(payload: ComplianceLoaderPreviewRequest) -> ComplianceLoaderPreviewOut:
    return preview_nec_loader(payload)


@router.post("/api/v1/compliance/corpora/nec/import", response_model=ComplianceImportOut)
def import_nec(payload: ComplianceLoaderPreviewRequest) -> ComplianceImportOut:
    return import_nec_corpus(payload)


@router.get("/api/v1/compliance/rules", response_model=list[ComplianceRuleOut])
def compliance_rules(
    corpus_id: int | None = Query(default=None),
    status: str | None = Query(default=None),
) -> list[ComplianceRuleOut]:
    return list_rules(corpus_id=corpus_id, status=status)
