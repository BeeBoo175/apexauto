export class HttpError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function readBoundedJson(
  body: ReadableStream<Uint8Array> | null,
  signal: AbortSignal,
  maximumBytes: number,
): Promise<unknown> {
  const bytes = await readBoundedBytes(body, signal, maximumBytes);
  return JSON.parse(new TextDecoder().decode(bytes));
}

export async function readBoundedBytes(
  body: ReadableStream<Uint8Array> | null,
  signal: AbortSignal,
  maximumBytes: number,
): Promise<Uint8Array> {
  if (!body) {
    throw new HttpError(400, "A JSON body is required.");
  }

  const reader = body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  const cancel = () => {
    void reader.cancel().catch(() => {});
  };
  signal.addEventListener("abort", cancel, { once: true });

  try {
    signal.throwIfAborted();

    while (true) {
      const chunk = await reader.read();
      signal.throwIfAborted();
      if (chunk.done) {
        break;
      }

      size += chunk.value.byteLength;
      if (size > maximumBytes) {
        throw new HttpError(413, "The request or response is too large.");
      }
      chunks.push(chunk.value);
    }

    const bytes = new Uint8Array(size);
    let offset = 0;
    for (const chunk of chunks) {
      bytes.set(chunk, offset);
      offset += chunk.byteLength;
    }
    return bytes;
  } finally {
    // Cancellation must also release a stalled read before another request can recover.
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    signal.removeEventListener("abort", cancel);
  }
}
