import "server-only";

import { createReadStream } from "node:fs";
import { mkdtemp, open, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { Readable } from "node:stream";
import { readCurrentActor } from "@/lib/auth/server";
import { actorIdentity } from "@/lib/auth/actor";
import { HttpError } from "./request";
import { requestApi } from "./upstream";

export async function relayPrivateCsv(
  request: Request,
  path: string,
  token: string | undefined,
  options: { deadlineMs: number; maximumBytes: number; filename: string },
) {
  const signal = AbortSignal.any([request.signal, AbortSignal.timeout(options.deadlineMs)]);
  const current = await readCurrentActor(signal);
  if (!current.actor) {
    throw new HttpError(current.status === 401 ? 401 : 502, "Session could not be confirmed.");
  }
  if (request.headers.get("X-Expected-Actor") !== actorIdentity(current.actor)) {
    throw new HttpError(409, "Your account assignment changed. Reload before exporting.");
  }

  const { response, signal: upstreamSignal } = await requestApi(path, options.deadlineMs, {
    token,
    signal,
    method: request.method,
    expectedActor: actorIdentity(current.actor),
  });
  if (!response.ok || !response.headers.get("content-type")?.startsWith("text/csv")) {
    await response.body?.cancel();
    throw new HttpError(response.ok ? 502 : response.status, "CSV export was not completed.");
  }

  let directory: string;
  try {
    directory = await mkdtemp(join(tmpdir(), "apexauto-export-"));
  } catch (error) {
    await response.body?.cancel();
    throw error;
  }
  const filename = join(directory, "download.csv");
  let handedOff = false;
  try {
    const bytes = await bufferCsvFile(response, filename, upstreamSignal, options.maximumBytes);
    // Finish the bounded file and recheck the assignment before releasing any private bytes.
    const confirmed = await readCurrentActor(AbortSignal.any([signal]));
    if (!confirmed.actor) {
      throw new HttpError(confirmed.status === 401 ? 401 : 502, "Session could not be confirmed.");
    }
    if (actorIdentity(confirmed.actor) !== actorIdentity(current.actor)) {
      throw new HttpError(409, "Your account assignment changed. Reload before exporting.");
    }
    signal.throwIfAborted();
    const stream = createReadStream(filename, { signal });
    stream.once("close", () => {
      void rm(directory, { recursive: true, force: true }).catch(() => {
        console.error("Temporary CSV cleanup failed.");
      });
    });
    const download = new Response(Readable.toWeb(stream) as ReadableStream, {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Length": String(bytes),
        "Content-Disposition": `attachment; filename="${options.filename}"`,
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
    handedOff = true;
    return download;
  } finally {
    if (!handedOff) {
      await rm(directory, { recursive: true, force: true });
    }
  }
}

async function bufferCsvFile(
  response: Response,
  filename: string,
  signal: AbortSignal,
  maximumBytes: number,
) {
  if (!response.body) {
    throw new HttpError(502, "The CSV response could not be confirmed.");
  }
  const file = await open(filename, "wx", 0o600).catch(async (error) => {
    await response.body?.cancel();
    throw error;
  });
  const reader = response.body.getReader();
  const cancel = () => void reader.cancel().catch(() => {});
  signal.addEventListener("abort", cancel, { once: true });
  let size = 0;
  try {
    signal.throwIfAborted();
    while (true) {
      const chunk = await reader.read();
      signal.throwIfAborted();
      if (chunk.done) {
        return size;
      }
      size += chunk.value.byteLength;
      if (size > maximumBytes) {
        throw new HttpError(413, "The export exceeds the download size limit. Narrow the filters.");
      }
      await file.writeFile(chunk.value);
    }
  } finally {
    await reader.cancel().catch(() => {});
    reader.releaseLock();
    signal.removeEventListener("abort", cancel);
    await file.close();
  }
}
