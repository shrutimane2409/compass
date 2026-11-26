"""Retrieval evaluation: does the expected official page appear in the top-k results?

Usage (from ai/, after ingesting data/sources.txt):  python -m evals.rag_eval [k]
Reports hit@k and MRR (mean reciprocal rank). Exits non-zero if hit@k falls below the threshold.
"""
import json
import sys
from pathlib import Path

from app import retrieval
from app.embeddings import EmbeddingQuotaExhausted

THRESHOLD = 0.8


def rank_of(expected: str, urls: list[str]) -> int | None:
    for i, u in enumerate(urls, start=1):
        if expected in u:
            return i
    return None


def main(k: int = 6) -> None:
    cases = [json.loads(l) for l in Path(__file__).with_name("rag_cases.jsonl").read_text().splitlines() if l.strip()]
    hits, rr = 0, 0.0
    for c in cases:
        try:
            urls = [ch.source_url for ch in retrieval.search(c["question"], k=k)]
        except EmbeddingQuotaExhausted as ex:
            sys.exit(f"\nStopped: {ex}")
        r = rank_of(c["expect_url"], urls)
        hits += r is not None
        rr += 1 / r if r else 0
        print(f"{'HIT ' if r else 'MISS'} rank={r or '-':<2} {c['question']}")
    hit_rate, mrr = hits / len(cases), rr / len(cases)
    print(f"\nhit@{k} = {hit_rate:.2f}   MRR = {mrr:.2f}   ({len(cases)} cases)")
    sys.exit(0 if hit_rate >= THRESHOLD else 1)


if __name__ == "__main__":
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 6)
