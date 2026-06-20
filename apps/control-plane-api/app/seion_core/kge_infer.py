"""Inference helpers for SEION-KGE v0.1 advisory predictions."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import numpy as np

from app.seion_core import SEION_KGE_MODEL_VERSION
from app.seion_core.kge_model import SeionKGEModel


def score_candidates(
    model: SeionKGEModel,
    entity_to_id: dict[str, int],
    relation_to_id: dict[str, int],
    head_uid: str,
    relation: str,
    candidate_tail_uids: list[str],
    top_k: int = 10,
) -> list[dict[str, Any]]:
    if head_uid not in entity_to_id:
        raise KeyError(f"Unknown head UID: {head_uid}")
    if relation not in relation_to_id:
        raise KeyError(f"Unknown relation: {relation}")
    valid_tail_uids = [uid for uid in candidate_tail_uids if uid in entity_to_id]
    triples = np.array([[entity_to_id[head_uid], relation_to_id[relation], entity_to_id[tail]] for tail in valid_tail_uids], dtype=np.int64)
    scores = model.score_triples(triples) if len(triples) else np.array([])
    order = np.argsort(-scores)[:top_k]
    return [
        {
            "head": head_uid,
            "relation": relation,
            "tail": valid_tail_uids[idx],
            "score": float(scores[idx]),
            "rank": rank,
            "model_version": model.config.model_version,
            "source": "seion_kge",
            "metadata": {"advisory": True},
        }
        for rank, idx in enumerate(order, start=1)
    ]


def export_predictions_jsonl(predictions: list[dict[str, Any]], path: str | Path) -> Path:
    target = Path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    with target.open("w", encoding="utf-8") as handle:
        for row in predictions:
            payload = {
                "head": row["head"],
                "relation": row["relation"],
                "tail": row["tail"],
                "score": float(row["score"]),
                "rank": int(row["rank"]),
                "model_version": row.get("model_version", SEION_KGE_MODEL_VERSION),
                "source": row.get("source", "seion_kge"),
                "metadata": {"advisory": True, **row.get("metadata", {})},
            }
            handle.write(json.dumps(payload, sort_keys=True))
            handle.write("\n")
    return target
