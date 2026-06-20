"""Tensor helpers for future SEION kernels."""

from __future__ import annotations

from typing import Any

import numpy as np


def validate_dense_ternary_kernel(kernel: Any) -> np.ndarray:
    arr = np.asarray(kernel, dtype=float)
    if arr.ndim != 4 or len(set(arr.shape)) != 1:
        raise ValueError("dense ternary kernel must have shape (D, D, D, D)")
    return arr


def simple_structured_kernel_label() -> str:
    return "simple_structured_kernel"
