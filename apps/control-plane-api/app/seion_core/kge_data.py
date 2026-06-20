"""JSONL data loading for SEION-KGE v0.1."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np


@dataclass(frozen=True)
class KGEDataset:
    entity_to_id: dict[str, int]
    relation_to_id: dict[str, int]
    triples: np.ndarray
    id_to_entity: list[str]
    id_to_relation: list[str]
    entities: dict[str, dict[str, Any]]
    raw_triples: list[dict[str, Any]]


def _read_jsonl(path: str | Path) -> list[dict[str, Any]]:
    rows = []
    with Path(path).open("r", encoding="utf-8") as handle:
        for line_no, line in enumerate(handle, start=1):
            stripped = line.strip()
            if not stripped:
                continue
            try:
                row = json.loads(stripped)
            except json.JSONDecodeError as exc:
                raise ValueError(f"Invalid JSONL at {path}:{line_no}: {exc}") from exc
            if not isinstance(row, dict):
                raise ValueError(f"JSONL row at {path}:{line_no} must be an object")
            rows.append(row)
    return rows


def load_entities(path: str | Path) -> dict[str, dict[str, Any]]:
    entities: dict[str, dict[str, Any]] = {}
    for row in _read_jsonl(path):
        uid = row.get("uid") or row.get("id")
        if not uid:
            raise ValueError("Entity row missing uid")
        entities[str(uid)] = row
    return entities


def _triple_value(row: dict[str, Any], *keys: str) -> str | None:
    for key in keys:
        value = row.get(key)
        if value is not None and str(value) != "":
            return str(value)
    return None


def load_triples(path: str | Path) -> list[dict[str, Any]]:
    triples = []
    for row in _read_jsonl(path):
        head = _triple_value(row, "head", "h", "head_uid")
        relation = _triple_value(row, "relation", "r", "rel")
        tail = _triple_value(row, "tail", "t", "tail_uid")
        if not head or not relation or not tail:
            raise ValueError("Triple row missing head/relation/tail")
        triples.append({"head": head, "relation": relation, "tail": tail, "source": row.get("source"), "metadata": row.get("metadata", {})})
    return triples


def build_kge_dataset(entities_path: str | Path, triples_path: str | Path) -> KGEDataset:
    entities = load_entities(entities_path)
    raw_triples = load_triples(triples_path)
    entity_uids = set(entities)
    for triple in raw_triples:
        entity_uids.add(triple["head"])
        entity_uids.add(triple["tail"])
    id_to_entity = sorted(entity_uids)
    entity_to_id = {uid: idx for idx, uid in enumerate(id_to_entity)}
    id_to_relation = sorted({triple["relation"] for triple in raw_triples})
    relation_to_id = {uid: idx for idx, uid in enumerate(id_to_relation)}
    triples = np.array(
        [[entity_to_id[row["head"]], relation_to_id[row["relation"]], entity_to_id[row["tail"]]] for row in raw_triples],
        dtype=np.int64,
    )
    return KGEDataset(entity_to_id, relation_to_id, triples, id_to_entity, id_to_relation, entities, raw_triples)


def split_triples(triples: np.ndarray, valid_ratio: float = 0.1, test_ratio: float = 0.1, seed: int = 42) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    arr = np.asarray(triples, dtype=np.int64)
    if arr.ndim != 2 or arr.shape[1] != 3:
        raise ValueError("triples must have shape (N, 3)")
    if valid_ratio < 0 or test_ratio < 0 or valid_ratio + test_ratio >= 1:
        raise ValueError("valid_ratio and test_ratio must be non-negative and sum to less than 1")
    rng = np.random.default_rng(seed)
    indices = np.arange(len(arr))
    rng.shuffle(indices)
    n_test = int(round(len(arr) * test_ratio))
    n_valid = int(round(len(arr) * valid_ratio))
    test_idx = indices[:n_test]
    valid_idx = indices[n_test : n_test + n_valid]
    train_idx = indices[n_test + n_valid :]
    return arr[train_idx], arr[valid_idx], arr[test_idx]
