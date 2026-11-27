import json

from app.grounding import cited_urls, normalise, source_urls, verify

UCD = "https://www.ucd.ie/global/study-at-ucd/scholarshipsfinances/livingcosts"


def test_normalise_strips_punctuation_and_trailing_slash():
    assert normalise("HTTPS://WWW.UCD.IE/Global/).") == "https://www.ucd.ie/Global"


def test_cited_urls_handles_parentheses_and_odd_brackets():
    text = f"Median rent is €1,200 (Source: {UCD}/). Also see 【{UCD}】."
    assert cited_urls(text) == [UCD]


def test_source_urls_reads_nested_tool_json():
    out = json.dumps({"results": [{"sourceUrl": UCD + "/", "text": "..."}, {"title": "x"}]})
    assert source_urls(out) == {UCD}


def test_source_urls_ignores_non_json():
    assert source_urls("not json") == set()


def test_answer_citing_only_retrieved_pages_is_grounded():
    v = verify(f"Rent is about €1,200 (Source: {UCD}).", {UCD}, used_tools=True)
    assert v["grounded"] and v["verified"] == [UCD] and v["unverified"] == []


def test_link_not_returned_by_tools_is_flagged():
    v = verify("See https://example.com/visa-guide for visas.", {UCD}, used_tools=True)
    assert not v["grounded"]
    assert v["unverified"] == ["https://example.com/visa-guide"]


def test_answer_without_tools_is_never_grounded():
    v = verify(f"Rent is €1,200 ({UCD}).", set(), used_tools=False)
    assert not v["grounded"]
