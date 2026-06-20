"""SEION-KGE v0.1 data/model/inference tests."""

from __future__ import annotations

import json

import numpy as np

from app.seion_core.kge_data import build_kge_dataset, load_entities, load_triples, split_triples
from app.seion_core.kge_infer import export_predictions_jsonl, score_candidates
from app.seion_core.kge_model import SeionKGEModel, evaluate_raw_mrr_hits, logistic_loss, negative_sampling, sigmoid, train_one_epoch
from app.seion_core.kge_train import save_artifacts


def _write_jsonl(path, rows):
    with path.open("w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row))
            handle.write("\n")


def _toy_files(tmp_path):
    entities_path = tmp_path / "entities.jsonl"
    triples_path = tmp_path / "triples.jsonl"
    _write_jsonl(
        entities_path,
        [
            {"uid": "requirement:1", "type": "requirement"},
            {"uid": "element:1", "type": "element"},
            {"uid": "element:2", "type": "element"},
        ],
    )
    _write_jsonl(
        triples_path,
        [
            {"head": "requirement:1", "relation": "should_be_supported_by", "tail": "element:1"},
            {"h": "element:1", "r": "similar_to", "t": "element:2"},
            {"head_uid": "requirement:1", "relation": "related_to_requirement", "tail_uid": "element:2"},
        ],
    )
    return entities_path, triples_path


def test_loads_jsonl_entities(tmp_path):
    entities_path, _ = _toy_files(tmp_path)
    entities = load_entities(entities_path)
    assert "requirement:1" in entities


def test_loads_jsonl_triples(tmp_path):
    _, triples_path = _toy_files(tmp_path)
    triples = load_triples(triples_path)
    assert triples[1]["relation"] == "similar_to"


def test_maps_uids_to_ids(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    assert dataset.triples.shape == (3, 3)
    assert dataset.entity_to_id["requirement:1"] >= 0


def test_split_is_deterministic(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    split_a = split_triples(dataset.triples, valid_ratio=0.2, test_ratio=0.2, seed=99)
    split_b = split_triples(dataset.triples, valid_ratio=0.2, test_ratio=0.2, seed=99)
    assert all(np.array_equal(a, b) for a, b in zip(split_a, split_b, strict=True))


def test_model_scores_batch():
    model = SeionKGEModel(num_entities=4, num_relations=2, dim=8, seed=1)
    scores = model.score_triples(np.array([[0, 0, 1], [1, 1, 2]]))
    assert scores.shape == (2,)


def test_training_reduces_loss_on_toy_graph(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    model = SeionKGEModel(num_entities=len(dataset.entity_to_id), num_relations=len(dataset.relation_to_id), dim=16, seed=2)
    negatives = negative_sampling(dataset.triples, len(dataset.entity_to_id), neg_k=2, seed=2)
    batch = np.vstack([dataset.triples, negatives])
    labels = np.concatenate([np.ones(len(dataset.triples)), np.zeros(len(negatives))])
    before = logistic_loss(model.score_triples(batch), labels)
    for epoch in range(30):
        train_one_epoch(model, dataset.triples, len(dataset.entity_to_id), neg_k=2, lr=0.2, seed=epoch)
    after = logistic_loss(model.score_triples(batch), labels)
    assert after < before


def test_saves_model_artifacts(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    model = SeionKGEModel(num_entities=len(dataset.entity_to_id), num_relations=len(dataset.relation_to_id), dim=4)
    paths = save_artifacts(model, dataset, tmp_path / "out", {"mrr": 1.0, "hits@1": 1.0})
    assert paths["model"].exists()
    assert paths["entity_to_id"].exists()
    assert paths["relation_to_id"].exists()
    assert paths["config"].exists()
    assert paths["metrics"].exists()


def test_evaluation_returns_mrr_hits(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    model = SeionKGEModel(num_entities=len(dataset.entity_to_id), num_relations=len(dataset.relation_to_id), dim=4)
    metrics = evaluate_raw_mrr_hits(model, dataset.triples)
    assert {"mrr", "hits@1", "hits@3", "hits@10"} <= set(metrics)


def test_scores_candidates(tmp_path):
    entities_path, triples_path = _toy_files(tmp_path)
    dataset = build_kge_dataset(entities_path, triples_path)
    model = SeionKGEModel(num_entities=len(dataset.entity_to_id), num_relations=len(dataset.relation_to_id), dim=4)
    rows = score_candidates(
        model,
        dataset.entity_to_id,
        dataset.relation_to_id,
        "requirement:1",
        "should_be_supported_by",
        ["element:1", "element:2"],
        top_k=2,
    )
    assert [row["rank"] for row in rows] == [1, 2]


def test_exports_predictions_jsonl(tmp_path):
    rows = [
        {
            "head": "requirement:1",
            "relation": "should_be_supported_by",
            "tail": "element:1",
            "score": 0.9,
            "rank": 1,
            "model_version": "seion-kge-v0.1.0",
            "source": "seion_kge",
            "metadata": {"advisory": True},
        }
    ]
    path = export_predictions_jsonl(rows, tmp_path / "predictions.jsonl")
    assert path.exists()
    payload = json.loads(path.read_text(encoding="utf-8"))
    assert payload["tail"] == "element:1"


def test_prediction_schema_contains_advisory_metadata(tmp_path):
    path = export_predictions_jsonl(
        [
            {
                "head": "requirement:1",
                "relation": "related_to_requirement",
                "tail": "requirement:2",
                "score": 0.1,
                "rank": 1,
            }
        ],
        tmp_path / "predictions.jsonl",
    )
    payload = json.loads(path.read_text(encoding="utf-8"))
    assert payload["metadata"]["advisory"] is True
