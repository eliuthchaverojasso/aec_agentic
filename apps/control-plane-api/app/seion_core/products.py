"""Numerical ternary products for SEION v0.1."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import numpy as np

EPS = 1e-12


@dataclass
class TernaryProduct:
    """Ternary product ``mu: V x V x V -> V``.

    ``kernel_type='simple_structured_kernel'`` uses ``mu(x,y,z)=x*y*z`` or
    ``W @ (x*y*z)``. It is a deterministic v0.1 fallback, not an E8 tensor.
    A dense kernel with shape ``(D, D, D, D)`` can be supplied for future
    algebraic kernels.
    """

    dim: int | None = None
    kernel: Any | None = None
    W: Any | None = None
    kernel_type: str = "simple_structured_kernel"

    def __post_init__(self) -> None:
        if self.kernel is not None:
            kernel = np.asarray(self.kernel, dtype=float)
            if kernel.ndim != 4 or len(set(kernel.shape)) != 1:
                raise ValueError("kernel must have shape (D, D, D, D)")
            self.kernel = kernel
            self.dim = int(kernel.shape[0])
            self.kernel_type = "dense_kernel"
        elif self.W is not None:
            W = np.asarray(self.W, dtype=float)
            if W.ndim != 2 or W.shape[0] != W.shape[1]:
                raise ValueError("W must have shape (D, D)")
            if self.dim is not None and W.shape[0] != self.dim:
                raise ValueError("W shape does not match dim")
            self.W = W
            self.dim = int(W.shape[0])
            self.kernel_type = "simple_structured_kernel"
        elif self.dim is None:
            raise ValueError("dim is required when kernel and W are not supplied")
        elif self.dim <= 0:
            raise ValueError("dim must be positive")

    def _vector(self, value: Any, name: str) -> np.ndarray:
        arr = np.asarray(value, dtype=float)
        if arr.shape != (self.dim,):
            raise ValueError(f"{name} must have shape ({self.dim},)")
        return arr

    def _matrix(self, value: Any, name: str) -> np.ndarray:
        arr = np.asarray(value, dtype=float)
        if arr.ndim != 2 or arr.shape[1] != self.dim:
            raise ValueError(f"{name} must have shape (N, {self.dim})")
        return arr

    def apply(self, x: Any, y: Any, z: Any) -> np.ndarray:
        x_arr = self._vector(x, "x")
        y_arr = self._vector(y, "y")
        z_arr = self._vector(z, "z")
        if self.kernel is not None:
            return np.einsum("abcd,b,c,d->a", self.kernel, x_arr, y_arr, z_arr)
        raw = x_arr * y_arr * z_arr
        return self.W @ raw if self.W is not None else raw

    def batch_apply(self, X: Any, Y: Any, Z: Any) -> np.ndarray:
        X_arr = self._matrix(X, "X")
        Y_arr = self._matrix(Y, "Y")
        Z_arr = self._matrix(Z, "Z")
        if not (X_arr.shape == Y_arr.shape == Z_arr.shape):
            raise ValueError("X, Y, Z must have matching batch shapes")
        if self.kernel is not None:
            return np.einsum("abcd,nb,nc,nd->na", self.kernel, X_arr, Y_arr, Z_arr)
        raw = X_arr * Y_arr * Z_arr
        return raw @ self.W.T if self.W is not None else raw

    def projected(self, P: Any, x: Any, y: Any, z: Any) -> np.ndarray:
        P_arr = np.asarray(P, dtype=float)
        if P_arr.shape != (self.dim, self.dim):
            raise ValueError(f"P must have shape ({self.dim}, {self.dim})")
        return P_arr @ self.apply(P_arr @ self._vector(x, "x"), P_arr @ self._vector(y, "y"), P_arr @ self._vector(z, "z"))

    def associator(self, a: Any, b: Any, c: Any, d: Any, e: Any) -> dict[str, Any]:
        a_arr = self._vector(a, "a")
        b_arr = self._vector(b, "b")
        c_arr = self._vector(c, "c")
        d_arr = self._vector(d, "d")
        e_arr = self._vector(e, "e")
        left = self.apply(self.apply(a_arr, b_arr, c_arr), d_arr, e_arr)
        mid = self.apply(a_arr, self.apply(b_arr, c_arr, d_arr), e_arr)
        right = self.apply(a_arr, b_arr, self.apply(c_arr, d_arr, e_arr))
        lm = left - mid
        mr = mid - right
        lr = left - right
        scale = max(float(np.linalg.norm(left) + np.linalg.norm(mid) + np.linalg.norm(right)), EPS)
        return {
            "left": left,
            "mid": mid,
            "right": right,
            "left_mid": lm,
            "mid_right": mr,
            "left_right": lr,
            "left_mid_abs": float(np.linalg.norm(lm)),
            "mid_right_abs": float(np.linalg.norm(mr)),
            "left_right_abs": float(np.linalg.norm(lr)),
            "associator_abs": float(max(np.linalg.norm(lm), np.linalg.norm(mr), np.linalg.norm(lr))),
            "associator_rel": float(max(np.linalg.norm(lm), np.linalg.norm(mr), np.linalg.norm(lr)) / scale),
        }

    def cyclic_defect(self, vectors: list[Any]) -> np.ndarray:
        if len(vectors) != 5:
            raise ValueError("cyclic_defect requires exactly five vectors")
        a, b, c, d, e = [self._vector(v, f"v{idx}") for idx, v in enumerate(vectors)]
        terms = [
            self.apply(self.apply(a, b, c), d, e),
            self.apply(self.apply(b, c, d), e, a),
            self.apply(self.apply(c, d, e), a, b),
            self.apply(self.apply(d, e, a), b, c),
            self.apply(self.apply(e, a, b), c, d),
        ]
        return np.sum(terms, axis=0)
