"""Runs the real agent graph with a scripted model: no network, no API keys."""
import asyncio
import json

from langchain_core.language_models.fake_chat_models import GenericFakeChatModel
from langchain_core.messages import AIMessage, AIMessageChunk
from langchain_core.outputs import ChatGenerationChunk
from langchain_core.tools import tool

from app.agent import build_graph


@tool
def search_sources(query: str) -> str:
    """Search official pages."""
    return json.dumps({"results": [{"sourceUrl": "https://example.ie/fees", "text": "Fees are listed here."}]})


class ScriptedModel(GenericFakeChatModel):
    """Plays back scripted replies, including tool calls, through the streaming path.

    The graph streams (stream_mode="messages"), so LangChain calls the model's stream method.
    GenericFakeChatModel only streams text content, so a tool-call-only reply would stream
    nothing ("No generations found in stream"). Real chat models stream tool-call chunks.
    """

    def bind_tools(self, tools, **kwargs):
        return self

    def _stream(self, messages, stop=None, run_manager=None, **kwargs):
        reply = self._generate(messages, stop=stop, run_manager=run_manager, **kwargs).generations[0].message
        yield ChatGenerationChunk(message=AIMessageChunk(
            content=reply.content,
            id=reply.id,
            tool_call_chunks=[
                {"name": c["name"], "args": json.dumps(c["args"]), "id": c["id"], "index": i}
                for i, c in enumerate(reply.tool_calls)
            ],
            chunk_position="last",
        ))


def tool_call(i: int) -> AIMessage:
    return AIMessage(content="", tool_calls=[{"name": "search_sources", "args": {"query": "fees"}, "id": f"c{i}"}])


def run(graph) -> list[list[str]]:
    async def go():
        steps = []
        async for mode, data in graph.astream(
                {"messages": [("user", "What are the fees?")], "steps": 0, "context": ""},
                stream_mode=["messages", "updates"]):
            if mode == "updates":
                steps.append(list(data.keys()))
        return steps
    return asyncio.run(go())


def test_model_calls_tool_then_answers():
    model = ScriptedModel(messages=iter([tool_call(1), AIMessage(content="See https://example.ie/fees")]))
    assert run(build_graph(model, [search_sources], max_steps=4)) == [["model"], ["tools"], ["model"]]


def test_step_limit_stops_tool_loops():
    model = ScriptedModel(messages=iter([tool_call(1), tool_call(2), AIMessage(content="Best answer so far.")]))
    assert run(build_graph(model, [search_sources], max_steps=2)) == [["model"], ["tools"], ["model"], ["tools"], ["final"]]
