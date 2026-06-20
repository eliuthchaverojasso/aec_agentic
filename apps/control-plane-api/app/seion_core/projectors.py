"""Projector construction and audit metrics."""

from __future__ import annotations

from typing import Any

import numpy as np

EPS = 1e-12


def orthonormalize_basis(Q: Any) -> np.ndarray:
    arr = np.asarray(Q, dtype=float)
    if arr.ndim != 2:
        raise ValueError("Q must be a 2D basis matrix")
    orth, _ = np.linalg.qr(arr)
    return orth


def make_projector_from_basis(Q: Any) -> np.ndarray:
    orth = orthonormalize_basis(Q)
    return orth @ orth.T


def random_projector(D: int, r: int, seed: int = 42) -> np.ndarray:
    if D <= 0:
        raise ValueError("D must be positive")
    if r < 0 or r > D:
        raise ValueError("r must satisfy 0 <= r <= D")
    rng = np.random.default_rng(seed)
    return make_projector_from_basis(rng.normal(size=(D, r))) if r else np.zeros((D, D))


def projector_metrics(P: Any, eig_tol: float = 1e-6) -> dict[str, Any]:
    arr = np.asarray(P, dtype=float)
    if arr.ndim != 2 or arr.shape[0] != arr.shape[1]:
        raise ValueError("P must have shape (D, D)")
    idem = arr @ arr - arr
    selfadj = arr.T - arr
    norm_p = max(float(np.linalg.norm(arr)), EPS)
    eigenvalues = np.linalg.eigvalsh((arr + arr.T) / 2.0)
    return {
        "dim": int(arr.shape[0]),
        "idem_abs": float(np.linalg.norm(idem)),
        "idem_rel": float(np.linalg.norm(idem) / norm_p),
        "selfadj_abs": float(np.linalg.norm(selfadj)),
        "selfadj_rel": float(np.linalg.norm(selfadj) / norm_p),
        "rank_trace": float(np.trace(arr)),
        "rank_spectrum": int(np.count_nonzero(np.abs(eigenvalues - 1.0) <= eig_tol)),
        "eigen_min": float(np.min(eigenvalues)),
        "eigen_max": float(np.max(eigenvalues)),
    }
