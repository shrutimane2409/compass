"""Synthetic traffic against the live API, to exercise monitoring, latency and rate limiting.

Every request carries "X-Synthetic: 1", so its usage events are tagged and excluded from real metrics.
Remove them afterwards with:  DELETE FROM usage_events WHERE (props->>'synthetic')::boolean;

    python -m scripts.simulate_traffic https://compass-api-38m9.onrender.com --students 40
    python -m scripts.simulate_traffic <api> --students 20 --burst      # ignore pacing, test 429 handling

Never submits "Report an error" (that would open GitHub issues). Chat is capped because it spends LLM quota.
"""
import argparse
import asyncio
import random
import statistics
import time
import uuid
from collections import Counter, defaultdict

import httpx

BACKGROUNDS = ["computer-science", "information-technology", "electronics", "electrical", "mechanical",
               "mathematics", "commerce", "science", "arts"]
INTERESTS = ["machine learning", "data science", "cloud", "cybersecurity", "software engineering", "AI",
             "business analytics", "NLP", "computer vision", "distributed systems"]
PRIORITIES = ["Balanced", "BestFit", "LowestCost"]
QUESTIONS = [
    "Which of my courses is cheapest overall including living costs?",
    "What IELTS score do I need for my top course?",
    "Can I work part-time while studying in Ireland?",
    "Compare my top two courses.",
]


def random_profile(rng: random.Random) -> dict:
    english = None if rng.random() < 0.15 else rng.choice([
        {"test": "Ielts", "score": rng.choice([6.0, 6.5, 7.0, 7.5])},
        {"test": "Pte", "score": rng.choice([55, 60, 65, 70])},
        {"test": "Toefl", "score": rng.choice([80, 90, 100])},
    ])
    return {
        "gradePercent": round(rng.uniform(52, 92), 1),
        "background": rng.choices(BACKGROUNDS, weights=[30, 20, 10, 5, 5, 8, 10, 7, 5])[0],
        "english": english,
        "workYears": rng.choice([0, 0, 1, 2, 3, 5]),
        "budgetEur": rng.choice([18000, 22000, 26000, 30000, 35000, 45000]),
        "interests": rng.sample(INTERESTS, rng.randint(1, 3)),
    }


class Stats:
    def __init__(self) -> None:
        self.latency: dict[str, list[float]] = defaultdict(list)
        self.status: dict[str, Counter] = defaultdict(Counter)
        self.fit_unavailable = 0
        self.errors: Counter = Counter()

    def record(self, name: str, started: float, status: int | str) -> None:
        self.latency[name].append((time.perf_counter() - started) * 1000)
        self.status[name][status] += 1

    def report(self, wall: float) -> None:
        total = sum(sum(c.values()) for c in self.status.values())
        print(f"\n{total} requests in {wall:.1f}s ({total / wall:.1f} req/s)\n")
        print(f"{'endpoint':<14}{'n':>5}{'p50 ms':>9}{'p95 ms':>9}{'max ms':>9}  status codes")
        for name, values in self.latency.items():
            values = sorted(values)
            p95 = values[min(len(values) - 1, int(0.95 * len(values)))]
            codes = ", ".join(f"{k}:{v}" for k, v in sorted(self.status[name].items(), key=str))
            print(f"{name:<14}{len(values):>5}{statistics.median(values):>9.0f}{p95:>9.0f}{values[-1]:>9.0f}  {codes}")
        if self.fit_unavailable:
            print(f"\nfitAvailable=false on {self.fit_unavailable} search(es): AI service degraded, rules-only ranking served.")
        for err, n in self.errors.most_common(5):
            print(f"error x{n}: {err}")


async def student(client: httpx.AsyncClient, rng: random.Random, stats: Stats, chats_left: list[int], pace: float) -> None:
    sid = str(uuid.uuid4())
    headers = {"X-Session-Id": sid, "X-Synthetic": "1"}
    profile = random_profile(rng)

    for _ in range(rng.randint(1, 3)):  # first search, then "what-if" edits
        priority = rng.choice(PRIORITIES)
        started = time.perf_counter()
        try:
            r = await client.post("/api/recommendations", json={"profile": profile, "priority": priority}, headers=headers)
            stats.record("search", started, r.status_code)
            if r.status_code == 200 and not r.json().get("fitAvailable", True):
                stats.fit_unavailable += 1
        except httpx.HTTPError as ex:
            stats.record("search", started, type(ex).__name__)
            stats.errors[repr(ex)[:120]] += 1
        profile["budgetEur"] = max(15000, profile["budgetEur"] + rng.choice([-4000, 0, 5000]))
        await asyncio.sleep(pace * rng.uniform(0.5, 1.5))

    if chats_left[0] > 0 and rng.random() < 0.3:
        chats_left[0] -= 1
        started = time.perf_counter()
        try:
            async with client.stream("POST", "/api/chat", headers={**headers, "Accept": "text/event-stream"},
                                     json={"message": rng.choice(QUESTIONS), "history": [], "profile": profile}) as r:
                async for _ in r.aiter_bytes():
                    pass
                stats.record("chat", started, r.status_code)
        except httpx.HTTPError as ex:
            stats.record("chat", started, type(ex).__name__)
            stats.errors[repr(ex)[:120]] += 1

    if rng.random() < 0.4:
        started = time.perf_counter()
        r = await client.post("/api/events/helpful", headers=headers,
                              json={"helpful": rng.random() < 0.75, "context": rng.choice(["shortlist", "chat"])})
        stats.record("helpful", started, r.status_code)


async def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("api")
    ap.add_argument("--students", type=int, default=30)
    ap.add_argument("--concurrency", type=int, default=4)
    ap.add_argument("--max-chats", type=int, default=5)
    ap.add_argument("--burst", action="store_true", help="no pacing: expect 429s from the rate limiter")
    ap.add_argument("--seed", type=int, default=7)
    args = ap.parse_args()

    rng = random.Random(args.seed)
    stats = Stats()
    chats_left = [args.max_chats]
    pace = 0.0 if args.burst else 4.0  # ~4 s between a student's actions keeps us under 60 req/min per IP
    sem = asyncio.Semaphore(args.concurrency)

    async with httpx.AsyncClient(base_url=args.api.rstrip("/"), timeout=120) as client:
        started = time.perf_counter()
        r = await client.get("/health/ready")
        print(f"health/ready {r.status_code} in {(time.perf_counter() - started):.1f}s (includes cold start)")

        async def run_one(i: int) -> None:
            async with sem:
                await student(client, random.Random(rng.random() + i), stats, chats_left, pace)

        wall = time.perf_counter()
        await asyncio.gather(*(run_one(i) for i in range(args.students)))
        stats.report(time.perf_counter() - wall)


if __name__ == "__main__":
    asyncio.run(main())
