import json

import pytest
from langchain_core.messages import AIMessage, HumanMessage, ToolMessage
from langgraph.graph import END

from app.agent import (ChatRequest, ChatTurn, profile_context, route_after_model, route_after_tools,
                       sse, text_of, to_messages)
from app.chunking import chunk_text
from app.fit import interests_to_query, to_fit_score
from app.tools import summarise_recommendations


class TestFitScore:
    def test_maps_range_linearly(self):
        assert to_fit_score(0.60, 0.35, 0.85) == 0.5

    @pytest.mark.parametrize("sim,expected", [(0.1, 0.0), (0.35, 0.0), (0.85, 1.0), (0.99, 1.0)])
    def test_clamps(self, sim, expected):
        assert to_fit_score(sim, 0.35, 0.85) == expected

    def test_rejects_bad_range(self):
        with pytest.raises(ValueError):
            to_fit_score(0.5, 0.8, 0.8)

    def test_query_ignores_blank_interests(self):
        assert interests_to_query(["ai", " ", "cloud "]) == "Master's course covering ai, cloud"


class TestChunking:
    def test_empty_text(self):
        assert chunk_text("   ") == []

    def test_short_text_is_one_chunk(self):
        assert chunk_text("One sentence. Two sentences.") == ["One sentence. Two sentences."]

    def test_chunks_respect_max_size(self):
        text = " ".join(f"Sentence number {i} is here." for i in range(200))
        chunks = chunk_text(text, max_chars=300, overlap=50)
        assert len(chunks) > 1
        assert all(len(c) <= 300 + 50 for c in chunks)

    def test_chunks_overlap(self):
        text = " ".join(f"Fact {i} about fees." for i in range(100))
        a, b = chunk_text(text, max_chars=200, overlap=60)[:2]
        assert a.split()[-1] in b

    def test_hard_splits_very_long_sentence(self):
        chunks = chunk_text("x" * 2000, max_chars=500, overlap=100)
        assert all(len(c) <= 500 for c in chunks)

    def test_invalid_overlap(self):
        with pytest.raises(ValueError):
            chunk_text("abc", max_chars=100, overlap=100)


class TestAgentRouting:
    def test_tool_calls_route_to_tools(self):
        msg = AIMessage(content="", tool_calls=[{"name": "search_sources", "args": {"query": "visa"}, "id": "1"}])
        assert route_after_model({"messages": [msg], "steps": 1, "context": ""}) == "tools"

    def test_plain_answer_ends(self):
        assert route_after_model({"messages": [AIMessage(content="Hi")], "steps": 1, "context": ""}) == END

    def test_step_limit_forces_final(self):
        state = {"messages": [ToolMessage(content="{}", tool_call_id="1")], "steps": 4, "context": ""}
        assert route_after_tools(state, max_steps=4) == "final"
        assert route_after_tools({**state, "steps": 2}, max_steps=4) == "model"


class TestHelpers:
    def test_text_of_handles_strings_and_blocks(self):
        assert text_of("hi") == "hi"
        assert text_of([{"type": "text", "text": "a"}, {"type": "text", "text": "b"}]) == "ab"
        assert text_of(None) == ""

    def test_sse_format(self):
        assert sse("token", {"text": "hi"}) == 'event: token\ndata: {"text": "hi"}\n\n'

    def test_history_becomes_messages(self):
        req = ChatRequest(message="next", history=[ChatTurn(role="user", content="a"),
                                                   ChatTurn(role="assistant", content="b")])
        msgs = to_messages(req)
        assert [type(m) for m in msgs] == [HumanMessage, AIMessage, HumanMessage]

    def test_profile_context(self):
        assert profile_context(None) == ""
        assert '"budgetEur": 30000' in profile_context({"budgetEur": 30000})

    def test_request_limits(self):
        with pytest.raises(ValueError):
            ChatRequest(message="")


def test_summarise_recommendations_keeps_facts_and_sources():
    payload = {"fitAvailable": True, "items": [{
        "course": {"id": 7, "title": "MSc X", "university": "U", "city": "Dublin",
                   "sourceUrl": "https://u.ie/x", "verifiedAt": "2026-10-01"},
        "cost": {"tuition": 20000, "living": 18000, "oneOff": 1000, "total": 39000},
        "tags": ["Eligible", "Affordable"], "reasons": ["ok"],
    }] * 12}
    out = summarise_recommendations(payload)
    assert out["fitAvailable"] is True
    assert len(out["results"]) == 8
    assert out["results"][0]["totalCostEur"] == 39000
    assert out["results"][0]["sourceUrl"] == "https://u.ie/x"
    json.dumps(out)


def test_parse_sources_reads_optional_snapshot(tmp_path):
    from scripts.ingest import parse_sources
    f = tmp_path / "sources.txt"
    f.write_text("# comment\nhttps://a.ie/x\n\nhttps://b.ie/y | snapshots/b.html\n")
    assert parse_sources(f) == [("https://a.ie/x", None), ("https://b.ie/y", tmp_path / "snapshots/b.html")]


def test_fetch_falls_back_to_snapshot_when_blocked(tmp_path):
    import httpx
    from scripts.ingest import fetch
    snap = tmp_path / "b.html"
    snap.write_text("<html><body>saved</body></html>")
    blocked = httpx.Client(transport=httpx.MockTransport(lambda req: httpx.Response(403)))
    assert fetch("https://b.ie/y", snap, blocked) == ("<html><body>saved</body></html>", True)
    try:
        fetch("https://b.ie/y", None, blocked)
        assert False, "expected HTTPStatusError"
    except httpx.HTTPStatusError:
        pass
