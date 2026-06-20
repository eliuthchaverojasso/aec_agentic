"""Structured SEION numerical audits."""

from __future__ import annotations

from typing import Any

import numpy as np

from app.seion_core.products import TernaryProduct
from app.seion_core.projectors import projector_metrics

EPS = 1e-12
DEFAULT_THRESHOLDS = {
    "idem_abs": 1e-8,
    "selfadj_abs": 1e-8,
    "closure_rel": 1e-8,
    "associator_rel": 1e-6,
    "comm_rel": 1e-8,
    "cyclic_rel": 1e-6,
}
FUTURE_AUDITS = [
    "C_beals",
    "D_snapping",
    "E_interscale",
    "F_rigidity",
    "I_reduced_tensor",
    "J_tensor_interscale",
    "K_hosvd",
    "L_gauge_canonicalization",
    "M_persistent_factorization",
]


def _status(metric: float, threshold: float) -> str:
    return "PASS" if metric <= threshold else "WARN"


def _block(block: str, status: str, metrics: dict[str, Any], thresholds: dict[str, Any], warnings: list[str] | None = None, notes: list[str] | None = None) -> dict[str, Any]:
    return {
        "block": block,
        "status": status,
        "metrics": metrics,
        "thresholds": thresholds,
        "warnings": warnings or [],
        "notes": notes or [],
    }


def _sample_array(samples: Any, dim: int | None = None) -> np.ndarray:
    arr = np.asarray(samples, dtype=float)
    if arr.ndim != 2:
        raise ValueError("samples must have shape (N, D)")
    if dim is not None and arr.shape[1] != dim:
        raise ValueError(f"samples must have dimension {dim}")
    return arr


def audit_projector(P: Any, thresholds: dict[str, float] | None = None) -> dict[str, Any]:
    merged = {**DEFAULT_THRESHOLDS, **(thresholds or {})}
    metrics = projector_metrics(P)
    status = "PASS" if metrics["idem_abs"] <= merged["idem_abs"] and metrics["selfadj_abs"] <= merged["selfadj_abs"] else "WARN"
    warnings = []
    if status != "PASS":
        warnings.append("Projector idempotence or self-adjointness defect exceeds threshold.")
    return _block("A_projector", status, metrics, {"idem_abs": merged["idem_abs"], "selfadj_abs": merged["selfadj_abs"]}, warnings)


def audit_commutator(P: Any, Delta: Any, thresholds: dict[str, float] | None = None) -> dict[str, Any]:
    merged = {**DEFAULT_THRESHOLDS, **(thresholds or {})}
    P_arr = np.asarray(P, dtype=float)
    D_arr = np.asarray(Delta, dtype=float)
    if P_arr.shape != D_arr.shape or P_arr.ndim != 2 or P_arr.shape[0] != P_arr.shape[1]:
        raise ValueError("P and Delta must have matching square shapes")
    defect = P_arr @ D_arr - D_arr @ P_arr
    comm_abs = float(np.linalg.norm(defect))
    comm_rel = float(comm_abs / max(np.linalg.norm(D_arr), EPS))
    status = _status(comm_rel, merged["comm_rel"])
    return _block("B_commutator", status, {"comm_abs": comm_abs, "comm_rel": comm_rel}, {"comm_rel": merged["comm_rel"]}, [] if status == "PASS" else ["Commutator defect exceeds threshold."])


def audit_closure(product: TernaryProduct, P: Any, samples: Any, thresholds: dict[str, float] | None = None) -> dict[str, Any]:
    merged = {**DEFAULT_THRESHOLDS, **(thresholds or {})}
    P_arr = np.asarray(P, dtype=float)
    sample_arr = _sample_array(samples, product.dim)
    I_minus_P = np.eye(product.dim) - P_arr
    defects: list[float] = []
    rels: list[float] = []
    for idx in range(max(0, len(sample_arr) - 2)):
        px, py, pz = (P_arr @ sample_arr[idx], P_arr @ sample_arr[idx + 1], P_arr @ sample_arr[idx + 2])
        mu = product.apply(px, py, pz)
        defect = float(np.linalg.norm(I_minus_P @ mu))
        defects.append(defect)
        rels.append(float(defect / max(np.linalg.norm(mu), EPS)))
    metrics = {
        "closure_abs": float(max(defects) if defects else 0.0),
        "closure_rel": float(max(rels) if rels else 0.0),
        "sample_windows": max(0, len(sample_arr) - 2),
    }
    status = _status(metrics["closure_rel"], merged["closure_rel"])
    return _block("G_nary_closure", status, metrics, {"closure_rel": merged["closure_rel"]}, [] if status == "PASS" else ["Projected product is not numerically closed."])


def audit_associator(product: TernaryProduct, P: Any, samples: Any, thresholds: dict[str, float] | None = None) -> dict[str, Any]:
    merged = {**DEFAULT_THRESHOLDS, **(thresholds or {})}
    P_arr = np.asarray(P, dtype=float)
    sample_arr = _sample_array(samples, product.dim)
    rows = []
    for idx in range(max(0, len(sample_arr) - 4)):
        vecs = [P_arr @ sample_arr[idx + offset] for offset in range(5)]
        assoc = product.associator(*vecs)
        rows.append(assoc)
    metrics = {
        "associator_abs": float(max((row["associator_abs"] for row in rows), default=0.0)),
        "associator_rel": float(max((row["associator_rel"] for row in rows), default=0.0)),
        "left_mid_abs": float(max((row["left_mid_abs"] for row in rows), default=0.0)),
        "mid_right_abs": float(max((row["mid_right_abs"] for row in rows), default=0.0)),
        "left_right_abs": float(max((row["left_right_abs"] for row in rows), default=0.0)),
        "sample_windows": max(0, len(sample_arr) - 4),
    }
    status = _status(metrics["associator_rel"], merged["associator_rel"])
    return _block("H_nary_associator", status, metrics, {"associator_rel": merged["associator_rel"]}, [] if status == "PASS" else ["Associator defect exceeds threshold."], ["Numerical defect only; no formal theorem is claimed."])


def audit_cyclic(product: TernaryProduct, P: Any, samples: Any, thresholds: dict[str, float] | None = None) -> dict[str, Any]:
    merged = {**DEFAULT_THRESHOLDS, **(thresholds or {})}
    P_arr = np.asarray(P, dtype=float)
    sample_arr = _sample_array(samples, product.dim)
    defects = []
    rels = []
    for idx in range(max(0, len(sample_arr) - 4)):
        vecs = [P_arr @ sample_arr[idx + offset] for offset in range(5)]
        defect = product.cyclic_defect(vecs)
        abs_val = float(np.linalg.norm(defect))
        scale = max(sum(float(np.linalg.norm(v)) for v in vecs), EPS)
        defects.append(abs_val)
        rels.append(float(abs_val / scale))
    metrics = {
        "cyclic_abs": float(max(defects) if defects else 0.0),
        "cyclic_rel": float(max(rels) if rels else 0.0),
        "sample_windows": max(0, len(sample_arr) - 4),
    }
    status = _status(metrics["cyclic_rel"], merged["cyclic_rel"])
    return _block("N_cyclic_law", status, metrics, {"cyclic_rel": merged["cyclic_rel"]}, [] if status == "PASS" else ["Cyclic numerical defect exceeds threshold."], ["Numerical audit only unless a formal theorem is supplied later."])


def run_basic_audit(product: TernaryProduct, P: Any, samples: Any, Delta: Any | None = None) -> dict[str, Any]:
    audits = [
        audit_projector(P),
        audit_closure(product, P, samples),
        audit_associator(product, P, samples),
        audit_cyclic(product, P, samples),
    ]
    if Delta is not None:
        audits.insert(1, audit_commutator(P, Delta))
    for name in FUTURE_AUDITS:
        audits.append(_block(name, "WARN", {}, {}, notes=["not implemented in SEION v0.1"]))
    summary = {
        "pass": sum(1 for item in audits if item["status"] == "PASS"),
        "warn": sum(1 for item in audits if item["status"] == "WARN"),
        "fail": sum(1 for item in audits if item["status"] == "FAIL"),
    }
    return {
        "seion_version": "0.1.0",
        "object": f"TernaryProduct(dim={product.dim}, kernel_type={product.kernel_type})",
        "audits": audits,
        "summary": summary,
    }
