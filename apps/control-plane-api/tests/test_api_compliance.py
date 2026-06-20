import json
from pathlib import Path

from fastapi.testclient import TestClient

from app.config import settings
from app.main import app


def _write_fixture(root: Path) -> dict[str, str]:
    (root / "ROCHELL ES" / "Compliance").mkdir(parents=True, exist_ok=True)
    blocks = root / "ROCHELL ES" / "Compliance" / "blocks.jsonl"
    edges = root / "ROCHELL ES" / "Compliance" / "edges.csv"
    audit = root / "ROCHELL ES" / "Compliance" / "structure_audit.json"
    gates = root / "ROCHELL ES" / "Compliance" / "research_grade_gates.json"
    blocks.write_text(
        "\n".join(
            [
                json.dumps({"reference_id": "NEC 110.26", "text": "Working space shall be provided."}),
                json.dumps({"reference_id": "NEC 300.4", "text": "Cables shall be protected."}),
            ]
        ),
        encoding="utf-8",
    )
    edges.write_text("src,dst,rel\n1,2,references\n", encoding="utf-8")
    audit.write_text(json.dumps({"health_score": 99.9}), encoding="utf-8")
    gates.write_text(
        json.dumps(
            {
                "gates": [
                    {"name": "structure_integrity", "passed": True},
                    {"name": "residual_contamination", "passed": False, "detail": "1 residual block"},
                ]
            }
        ),
        encoding="utf-8",
    )
    return {
        "blocks_path": "ROCHELL ES/Compliance/blocks.jsonl",
        "edges_path": "ROCHELL ES/Compliance/edges.csv",
        "structure_audit_path": "ROCHELL ES/Compliance/structure_audit.json",
        "gates_path": "ROCHELL ES/Compliance/research_grade_gates.json",
    }


def test_compliance_nec_preview_and_import(tmp_path):
    original_landing = settings.landing_dir
    settings.landing_dir = tmp_path
    payload = {
        "name": "NEC Candidate",
        "code_family": "NEC",
        "edition": "2023",
        "jurisdiction": "US",
        "source_type": "structured_nec",
        **_write_fixture(tmp_path),
    }
    try:
        with TestClient(app) as client:
            preview = client.post("/api/v1/compliance/corpora/nec/preview", json=payload)
            assert preview.status_code == 200
            preview_payload = preview.json()
            assert preview_payload["blocks_count"] == 2
            assert preview_payload["edges_count"] == 1
            assert preview_payload["gate_status"] == "failed"

            imported = client.post("/api/v1/compliance/corpora/nec/import", json=payload)
            assert imported.status_code == 200
            imported_payload = imported.json()
            assert imported_payload["corpus"]["loader_status"] == "candidate_review_required"
            assert imported_payload["review_required"] is True

            status = client.get("/api/v1/compliance/status")
            assert status.status_code == 200
            assert status.json()["corpora_count"] >= 1
    finally:
        settings.landing_dir = original_landing

