"""Fetch official pages, chunk, embed and store them for retrieval.

Usage (from ai/):  python -m scripts.ingest ../data/sources.txt
Unchanged pages (same hash) are skipped. Changed pages are re-chunked and reported,
so the matching course rows can be re-verified by a human.

A line may name a saved copy of the page:   <url> | snapshots/<file>.html
(path relative to the sources file). Some official sites block automated clients; the
snapshot is used only when the live fetch fails, and citations still point to <url>.
Exits non-zero if any page could not be ingested from either source.
"""
import hashlib
import sys
from pathlib import Path

import httpx
from bs4 import BeautifulSoup

from app.chunking import chunk_text
from app.db import get_pool
from app.embeddings import embed_passages

USER_AGENT = "CompassBot/0.1 (student project; course research)"


def page_text(html: str) -> tuple[str, str]:
    soup = BeautifulSoup(html, "html.parser")
    for tag in soup(["script", "style", "nav", "header", "footer", "noscript", "form"]):
        tag.decompose()
    title = (soup.title.string or "").strip() if soup.title else ""
    main = soup.find("main") or soup.body or soup
    return title, main.get_text("\n")


def parse_sources(path: Path) -> list[tuple[str, Path | None]]:
    """Return (url, snapshot path or None) for each non-comment line."""
    out = []
    for line in path.read_text().splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        url, _, snap = (p.strip() for p in line.partition("|"))
        out.append((url, path.parent / snap if snap else None))
    return out


def fetch(url: str, snapshot: Path | None, client: httpx.Client) -> tuple[str, bool]:
    """Return (html, from_snapshot). Live page first; saved snapshot if the fetch fails."""
    try:
        resp = client.get(url)
        resp.raise_for_status()
        return resp.text, False
    except httpx.HTTPError:
        if snapshot and snapshot.is_file():
            return snapshot.read_text(errors="replace"), True
        raise


def ingest(url: str, client: httpx.Client, snapshot: Path | None = None) -> str:
    html, from_snapshot = fetch(url, snapshot, client)
    title, text = page_text(html)
    page_hash = hashlib.sha256(text.encode()).hexdigest()

    mark = "*" if from_snapshot else ""

    with get_pool().connection() as conn:
        row = conn.execute("SELECT page_hash FROM documents WHERE source_url = %s LIMIT 1", (url,)).fetchone()
        if row and row[0] == page_hash:
            return "unchanged" + mark

        chunks = chunk_text(text)
        vectors = embed_passages(chunks) if chunks else []
        with conn.transaction():
            conn.execute("DELETE FROM documents WHERE source_url = %s", (url,))
            for i, (chunk, vec) in enumerate(zip(chunks, vectors)):
                conn.execute(
                    "INSERT INTO documents (source_url, title, chunk_index, content, embedding, page_hash) "
                    "VALUES (%s, %s, %s, %s, %s, %s)",
                    (url, title or url, i, chunk, vec, page_hash))
    return ("changed" if row else "new") + mark


def describe(ex: httpx.HTTPError) -> str:
    if isinstance(ex, httpx.HTTPStatusError):
        return f"HTTP {ex.response.status_code}"
    return type(ex).__name__


def main(path: str) -> None:
    sources = parse_sources(Path(path))
    changed, failed, snapshotted = [], [], []
    with httpx.Client(headers={"User-Agent": USER_AGENT}, timeout=20, follow_redirects=True) as client:
        for url, snapshot in sources:
            try:
                status = ingest(url, client, snapshot)
            except httpx.HTTPError as ex:
                hint = "" if snapshot else "; save the page and add '| snapshots/<file>.html'"
                print(f"{'failed':10} {url} ({describe(ex)}{hint})")
                failed.append(url)
                continue
            print(f"{status:10} {url}")
            if status.endswith("*"):
                snapshotted.append(url)
            if status.startswith("changed"):
                changed.append(url)
    if snapshotted:
        print("\n* live fetch blocked; ingested from saved snapshot (refresh it by hand):")
        print("\n".join(snapshotted))
    if changed:
        print("\nRe-verify courses sourced from these pages:")
        print("\n".join(changed))
    print(f"\n{len(sources) - len(failed)}/{len(sources)} pages ingested")
    if failed:
        sys.exit(1)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "../data/sources.txt")
