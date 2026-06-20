"""SEION v0.1 mathematical and KGE primitives.

This package is advisory software infrastructure only. It does not compute or
approve official EMA readiness, compliance, or evidence.
"""

from app.seion_core.products import TernaryProduct
from app.seion_core.projectors import (
    make_projector_from_basis,
    orthonormalize_basis,
    projector_metrics,
    random_projector,
)

SEION_CORE_VERSION = "0.1.0"
SEION_KGE_MODEL_VERSION = "seion-kge-v0.1.0"

__all__ = [
    "SEION_CORE_VERSION",
    "SEION_KGE_MODEL_VERSION",
    "TernaryProduct",
    "make_projector_from_basis",
    "orthonormalize_basis",
    "projector_metrics",
    "random_projector",
]
