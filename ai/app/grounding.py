"""Citation check: every link in an answer must come from what the tools actually returned this turn.

The LLM is told to cite only tool sources, but instructions are not guarantees. This check runs after
the answer is complete and tells the student whether each cited page was really retrieved.
"""
import json
import re
from typing import Any

_URL = re.compile(r"https?://[^\s<>\"'()\[\]{}【】,]+")
_TRAILING = ".,;:!?)]}】"


def normalise(url: str) -> str:
    url = url.strip().rstrip(_TRAILING).rstrip("/")
    scheme, _, rest = url.partition("://")
    host, sep, path = rest.partition("/")
    return f"{scheme.lower()}://{host.lower()}{sep}{path}"


def cited_urls(answer: str) -> list[str]:
    seen, out = set(), []
    for m in _URL.findall(answer):
        n = normalise(m)
        if n not in seen:
            seen.add(n)
            out.append(n)
    return out


def source_urls(tool_output: str) -> set[str]:
    """All values of keys named sourceUrl in a tool's JSON output."""
    try:
        data = json.loads(tool_output)
    except (TypeError, json.JSONDecodeError):
        return set()
    found: set[str] = set()

    def walk(v: Any) -> None:
        if isinstance(v, dict):
            for k, x in v.items():
                if k == "sourceUrl" and isinstance(x, str):
                    found.add(normalise(x))
                else:
                    walk(x)
        elif isinstance(v, list):
            for x in v:
                walk(x)

    walk(data)
    return found


def verify(answer: str, retrieved: set[str], used_tools: bool) -> dict:
    cited = cited_urls(answer)
    verified = [u for u in cited if u in retrieved]
    unverified = [u for u in cited if u not in retrieved]
    return {
        "usedTools": used_tools,
        "verified": verified,
        "unverified": unverified,
        "grounded": used_tools and bool(verified) and not unverified,
    }
