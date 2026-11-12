// Reads a Server-Sent-Events body (`event: name` / `data: json`, as D3sk.AgentCli's Wire.SseFrame
// and D3SKHAND's hub write them) and hands each frame on as (name, data).

/** Splits SSE text into frames; returns the unfinished tail to prepend to the next chunk. */
export function parseSse(buffer, onFrame) {
  let rest = buffer.replace(/\r\n?/g, "\n");
  let boundary;
  while ((boundary = rest.indexOf("\n\n")) >= 0) {
    const chunk = rest.slice(0, boundary);
    rest = rest.slice(boundary + 2);
    let name = "message";
    const data = [];
    for (const line of chunk.split("\n")) {
      if (line.startsWith("event:")) name = line.slice(6).trim();
      else if (line.startsWith("data:")) data.push(line.slice(5).replace(/^ /, ""));
    }
    if (!data.length) continue;
    let parsed;
    try {
      parsed = JSON.parse(data.join("\n"));
    } catch {
      continue;
    }
    onFrame(name, parsed);
  }
  return rest;
}

/** Feeds every frame of a fetch Response body to `onFrame`; resolves when the stream ends. */
export async function readSse(response, onFrame) {
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";
  for (;;) {
    const { value, done } = await reader.read();
    if (done) break;
    buffer = parseSse(buffer + decoder.decode(value, { stream: true }), onFrame);
  }
  parseSse(`${buffer + decoder.decode()}\n\n`, onFrame);
}
