import { describe, expect, it } from "vitest";
import { createSseParser } from "./sse";

describe("createSseParser", () => {
  it("parses complete events", () => {
    const parse = createSseParser();
    expect(parse('event: token\ndata: {"text":"Hi"}\n\n')).toEqual([{ event: "token", data: '{"text":"Hi"}' }]);
  });

  it("keeps partial events until the rest arrives", () => {
    const parse = createSseParser();
    expect(parse("event: tok")).toEqual([]);
    expect(parse('en\ndata: {"text":"a"}\n')).toEqual([]);
    expect(parse("\nevent: done\ndata: {}\n\n")).toEqual([
      { event: "token", data: '{"text":"a"}' },
      { event: "done", data: "{}" },
    ]);
  });

  it("handles CRLF line endings", () => {
    const parse = createSseParser();
    expect(parse("event: done\r\ndata: {}\r\n\r\n")).toEqual([{ event: "done", data: "{}" }]);
  });

  it("defaults the event name to message", () => {
    expect(createSseParser()("data: x\n\n")).toEqual([{ event: "message", data: "x" }]);
  });
});
