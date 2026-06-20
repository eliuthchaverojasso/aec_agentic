"""SEION v0.1 mathematical core tests."""

from __future__ import annotations

import json

import numpy as np

from app.seion_core.audits import audit_associator, audit_closure, audit_cyclic, run_basic_audit
from app.seion_core.products import TernaryProduct
from app.seion_core.projectors import make_projector_from_basis, projector_metrics, random_projector
from app.seion_core.reports import load_audit_report, write_audit_report


def test_ternary_product_shape():
    product = TernaryProduct(dim=3)
    out = product.apply(np.array([1, 2, 3]), np.array([2, 3, 4]), np.array([3, 4, 5]))
    assert out.shape == (3,)
    assert product.kernel_type == "simple_structured_kernel"


def test_projector_idempotence():
    projector = random_projector(5, 2, seed=7)
    metrics = projector_metrics(projector)
    assert metrics["idem_abs"] < 1e-10


def test_projector_selfadjoint():
    projector = make_projector_from_basis(np.eye(4)[:, :2])
    metrics = projector_metrics(projector)
    assert metrics["selfadj_abs"] == 0


def test_closure_audit_synthetic_pass_for_identity_projector():
    product = TernaryProduct(dim=4)
    samples = np.ones((5, 4))
    report = audit_closure(product, np.eye(4), samples)
    assert report["block"] == "G_nary_closure"
    assert report["status"] == "PASS"
    assert report["metrics"]["closure_abs"] == 0


def test_associator_audit_returns_metrics():
    product = TernaryProduct(dim=3)
    samples = np.arange(18, dtype=float).reshape(6, 3) / 10
    report = audit_associator(product, np.eye(3), samples)
    assert report["block"] == "H_nary_associator"
    assert "associator_abs" in report["metrics"]


def test_cyclic_audit_returns_metrics():
    product = TernaryProduct(dim=3)
    samples = np.ones((5, 3))
    report = audit_cyclic(product, np.eye(3), samples)
    assert report["block"] == "N_cyclic_law"
    assert "cyclic_abs" in report["metrics"]


def test_report_is_json_serializable(tmp_path):
    product = TernaryProduct(dim=2)
    report = run_basic_audit(product, np.eye(2), np.ones((5, 2)))
    json.dumps(report)
    path = write_audit_report(report, tmp_path / "audit.json")
    assert load_audit_report(path)["seion_version"] == "0.1.0"
