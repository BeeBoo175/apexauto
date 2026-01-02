import "server-only";

export class HttpError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}

// Bound unbuffered payloads before collecting their bytes in memory.
export async function readBoundedJson(
  body: ReadableStream<Uint8Array> | null,
  signal: AbortSignal,
  maximumBytes: number,
): Promise<unknown> {
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

    return JSON.parse(Buffer.concat(chunks).toString("utf8"));
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    signal.removeEventListener("abort", cancel);
  }
}

// Defense-in-depth CSRF guard verifying exact origin and blocking cross-site callers.
export function requireSameOrigin(request: Request) {
  const configured = process.env.APP_BASE_URL;

  if (!configured) {
    throw new Error("APP_BASE_URL is required.");
  }

  if (
    request.headers.get("origin") !== new URL(configured).origin ||
    request.headers.get("sec-fetch-site") === "cross-site"
  ) {
    throw new HttpError(403, "This action requires a same-origin request.");
  }
}
