"""CLI and helpers for training SEION-KGE v0.1."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from app.seion_core.kge_data import build_kge_dataset, split_triples
from app.seion_core.kge_model import SeionKGEModel, evaluate_raw_mrr_hits, train_one_epoch


def save_artifacts(model: SeionKGEModel, dataset, out_dir: str | Path, metrics: dict[str, float]) -> dict[str, Path]:
    target = Path(out_dir)
    target.mkdir(parents=True, exist_ok=True)
    model_path = target / "model.npz"
    model.save(str(model_path))
    paths = {
        "model": model_path,
        "entity_to_id": target / "entity_to_id.json",
        "relation_to_id": target / "relation_to_id.json",
        "config": target / "config.json",
        "metrics": target / "metrics.json",
    }
    paths["entity_to_id"].write_text(json.dumps(dataset.entity_to_id, indent=2, sort_keys=True), encoding="utf-8")
    paths["relation_to_id"].write_text(json.dumps(dataset.relation_to_id, indent=2, sort_keys=True), encoding="utf-8")
    paths["config"].write_text(json.dumps(model.config.__dict__, indent=2, sort_keys=True), encoding="utf-8")
    paths["metrics"].write_text(json.dumps(metrics, indent=2, sort_keys=True), encoding="utf-8")
    return paths


def train_from_files(
    entities: str | Path,
    triples: str | Path,
    out: str | Path,
    dim: int = 64,
    epochs: int = 5,
    neg_k: int = 16,
    alpha_ternary: float = 0.0,
    seed: int = 42,
    lr: float = 0.05,
) -> dict[str, float]:
    dataset = build_kge_dataset(entities, triples)
    train, valid, test = split_triples(dataset.triples, seed=seed)
    eval_triples = valid if len(valid) else train
    model = SeionKGEModel(
        num_entities=len(dataset.entity_to_id),
        num_relations=len(dataset.relation_to_id),
        dim=dim,
        use_ternary=True,
        alpha_ternary=alpha_ternary,
        seed=seed,
    )
    loss = 0.0
    for epoch in range(epochs):
        loss = train_one_epoch(model, train, len(dataset.entity_to_id), neg_k=neg_k, lr=lr, seed=seed + epoch)
    metrics = {"loss": loss, **evaluate_raw_mrr_hits(model, eval_triples)}
    if len(test):
        metrics.update({f"test_{key}": value for key, value in evaluate_raw_mrr_hits(model, test).items()})
    save_artifacts(model, dataset, out, metrics)
    return metrics


def main() -> None:
    parser = argparse.ArgumentParser(description="Train SEION-KGE v0.1 advisory model")
    parser.add_argument("--entities", required=True)
    parser.add_argument("--triples", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--dim", type=int, default=64)
    parser.add_argument("--epochs", type=int, default=5)
    parser.add_argument("--neg-k", type=int, default=16)
    parser.add_argument("--alpha-ternary", type=float, default=0.0)
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--lr", type=float, default=0.05)
    args = parser.parse_args()
    metrics = train_from_files(args.entities, args.triples, args.out, args.dim, args.epochs, args.neg_k, args.alpha_ternary, args.seed, args.lr)
    print(json.dumps(metrics, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
