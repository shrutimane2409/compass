export interface SseEvent {
  event: string;
  data: string;
}

/**
 * Incremental server-sent-events parser. Feed it text chunks as they arrive;
 * it returns complete events and keeps any partial event for the next chunk.
 */
export function createSseParser() {
  let buffer = "";
  return (chunk: string): SseEvent[] => {
    buffer += chunk.replace(/\r\n/g, "\n");
    const events: SseEvent[] = [];
    let boundary: number;
    while ((boundary = buffer.indexOf("\n\n")) !== -1) {
      const block = buffer.slice(0, boundary);
      buffer = buffer.slice(boundary + 2);
      let event = "message";
      const data: string[] = [];
      for (const line of block.split("\n")) {
        if (line.startsWith("event:")) event = line.slice(6).trim();
        else if (line.startsWith("data:")) data.push(line.slice(5).trimStart());
      }
      if (data.length) events.push({ event, data: data.join("\n") });
    }
    return events;
  };
}
