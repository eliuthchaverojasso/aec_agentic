"""Minimal deterministic SEION-KGE model."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Any

import numpy as np

from app.seion_core import SEION_KGE_MODEL_VERSION


@dataclass
class SeionKGEConfig:
    num_entities: int
    num_relations: int
    dim: int = 64
    use_ternary: bool = True
    alpha_ternary: float = 0.0
    seed: int = 42
    model_version: str = SEION_KGE_MODEL_VERSION


class SeionKGEModel:
    """DistMult-style scorer with optional simple SEION ternary term."""

    def __init__(
        self,
        num_entities: int,
        num_relations: int,
        dim: int = 64,
        use_ternary: bool = True,
        alpha_ternary: float = 0.0,
        seed: int = 42,
        entity_emb: np.ndarray | None = None,
        relation_emb: np.ndarray | None = None,
        relation_query_emb: np.ndarray | None = None,
    ) -> None:
        if num_entities <= 0 or num_relations <= 0 or dim <= 0:
            raise ValueError("num_entities, num_relations, and dim must be positive")
        self.config = SeionKGEConfig(num_entities, num_relations, dim, use_ternary, alpha_ternary, seed)
        rng = np.random.default_rng(seed)
        scale = 1.0 / max(dim, 1) ** 0.5
        self.entity_emb = entity_emb if entity_emb is not None else rng.normal(0, scale, size=(num_entities, dim))
        self.relation_emb = relation_emb if relation_emb is not None else rng.normal(0, scale, size=(num_relations, dim))
        self.relation_query_emb = relation_query_emb if relation_query_emb is not None else rng.normal(0, scale, size=(num_relations, dim))

    def forward(self, head_ids: Any, rel_ids: Any, tail_ids: Any) -> np.ndarray:
        h = np.asarray(head_ids, dtype=np.int64)
        r = np.asarray(rel_ids, dtype=np.int64)
        t = np.asarray(tail_ids, dtype=np.int64)
        h_emb = self.entity_emb[h]
        r_emb = self.relation_emb[r]
        t_emb = self.entity_emb[t]
        base_score = np.sum(h_emb * r_emb * t_emb, axis=-1)
        if not self.config.use_ternary or self.config.alpha_ternary == 0:
            return base_score
        q_emb = self.relation_query_emb[r]
        ternary = h_emb * r_emb * t_emb
        return base_score + self.config.alpha_ternary * np.sum(ternary * q_emb, axis=-1)

    def score_triples(self, triples: Any) -> np.ndarray:
        arr = np.asarray(triples, dtype=np.int64)
        if arr.ndim != 2 or arr.shape[1] != 3:
            raise ValueError("triples must have shape (N, 3)")
        return self.forward(arr[:, 0], arr[:, 1], arr[:, 2])

    def save(self, path: str) -> None:
        np.savez(
            path,
            entity_emb=self.entity_emb,
            relation_emb=self.relation_emb,
            relation_query_emb=self.relation_query_emb,
            config=np.array([self.config.num_entities, self.config.num_relations, self.config.dim, int(self.config.use_ternary), self.config.alpha_ternary, self.config.seed], dtype=float),
        )

    @classmethod
    def load(cls, path: str) -> "SeionKGEModel":
        data = np.load(path, allow_pickle=False)
        config = data["config"]
        return cls(
            num_entities=int(config[0]),
            num_relations=int(config[1]),
            dim=int(config[2]),
            use_ternary=bool(int(config[3])),
            alpha_ternary=float(config[4]),
            seed=int(config[5]),
            entity_emb=data["entity_emb"],
            relation_emb=data["relation_emb"],
            relation_query_emb=data["relation_query_emb"],
        )


def sigmoid(x: np.ndarray) -> np.ndarray:
    return 1.0 / (1.0 + np.exp(-np.clip(x, -50, 50)))


def logistic_loss(scores: np.ndarray, labels: np.ndarray) -> float:
    signed = (2.0 * labels - 1.0) * scores
    return float(np.mean(np.logaddexp(0.0, -signed)))


def negative_sampling(triples: Any, num_entities: int, neg_k: int = 1, seed: int = 42) -> np.ndarray:
    arr = np.asarray(triples, dtype=np.int64)
    if arr.ndim != 2 or arr.shape[1] != 3:
        raise ValueError("triples must have shape (N, 3)")
    rng = np.random.default_rng(seed)
    negatives = np.repeat(arr, neg_k, axis=0).copy()
    negatives[:, 2] = rng.integers(0, num_entities, size=len(negatives))
    same = negatives[:, 2] == np.repeat(arr[:, 2], neg_k)
    negatives[same, 2] = (negatives[same, 2] + 1) % num_entities
    return negatives


def train_one_epoch(model: SeionKGEModel, triples: Any, num_entities: int, neg_k: int = 1, lr: float = 0.05, seed: int = 42) -> float:
    positives = np.asarray(triples, dtype=np.int64)
    negatives = negative_sampling(positives, num_entities, neg_k, seed)
    batch = np.vstack([positives, negatives])
    labels = np.concatenate([np.ones(len(positives)), np.zeros(len(negatives))])
    scores = model.score_triples(batch)
    loss = logistic_loss(scores, labels)
    grad_score = (sigmoid(scores) - labels) / len(labels)

    for (h_id, r_id, t_id), grad in zip(batch, grad_score, strict=False):
        h = model.entity_emb[h_id].copy()
        r = model.relation_emb[r_id].copy()
        t = model.entity_emb[t_id].copy()
        q = model.relation_query_emb[r_id].copy()
        ternary_factor = 1.0 + (model.config.alpha_ternary * q if model.config.use_ternary else 0.0)
        grad_h = grad * r * t * ternary_factor
        grad_r = grad * h * t * ternary_factor
        grad_t = grad * h * r * ternary_factor
        grad_q = grad * model.config.alpha_ternary * (h * r * t) if model.config.use_ternary else 0.0
        model.entity_emb[h_id] -= lr * grad_h
        model.relation_emb[r_id] -= lr * grad_r
        model.entity_emb[t_id] -= lr * grad_t
        if model.config.use_ternary and model.config.alpha_ternary != 0:
            model.relation_query_emb[r_id] -= lr * grad_q
    return loss


def evaluate_raw_mrr_hits(model: SeionKGEModel, triples: Any, hits_ks: tuple[int, ...] = (1, 3, 10)) -> dict[str, float]:
    arr = np.asarray(triples, dtype=np.int64)
    ranks = []
    all_tails = np.arange(model.config.num_entities, dtype=np.int64)
    for head, rel, tail in arr:
        candidates = np.column_stack([
            np.full(model.config.num_entities, head, dtype=np.int64),
            np.full(model.config.num_entities, rel, dtype=np.int64),
            all_tails,
        ])
        scores = model.score_triples(candidates)
        rank = int(1 + np.count_nonzero(scores > scores[tail]))
        ranks.append(rank)
    ranks_arr = np.asarray(ranks, dtype=float)
    metrics = {"mrr": float(np.mean(1.0 / ranks_arr)) if len(ranks_arr) else 0.0}
    for k in hits_ks:
        metrics[f"hits@{k}"] = float(np.mean(ranks_arr <= k)) if len(ranks_arr) else 0.0
    return metrics
