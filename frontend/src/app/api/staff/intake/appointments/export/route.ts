import { readCurrentActor } from "@/lib/auth/server";
import { actorIdentity } from "@/lib/auth/actor";
import { intakeToken } from "@/lib/intake/server";
import { intakeError } from "@/lib/intake/routes";
import { requestApi } from "@/lib/server/http/upstream";
import { HttpError, readBoundedBytes } from "@/lib/server/http/request";

export async function GET(request: Request) {
  try {
    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(22000)]);
    const current = await readCurrentActor(signal);
    if (!current.actor)
      throw new HttpError(current.status === 401 ? 401 : 502, "Session could not be confirmed.");
    if (request.headers.get("X-Expected-Actor") !== actorIdentity(current.actor)) {
      throw new HttpError(409, "Your account assignment changed. Reload before exporting.");
    }
    const input = new URL(request.url).searchParams;
    const query = new URLSearchParams();
    for (const key of ["from", "to", "status", "branchId", "search", "sort", "direction"]) {
      if (input.has(key)) query.set(key, input.get(key)!);
    }
    const { response, signal: upstreamSignal } = await requestApi(
      `/api/staff/intake/appointments/export?${query}`,
      22000,
      {
        token: await intakeToken(),
        signal,
      },
    );
    if (!response.ok) {
      await response.body?.cancel();
      throw new HttpError(response.status, "CSV export was not completed.");
    }
    if (!response.headers.get("content-type")?.startsWith("text/csv")) {
      await response.body?.cancel();
      throw new Error("Invalid CSV response");
    }
    const bytes = await readBoundedBytes(response.body, upstreamSignal, 8 * 1024 * 1024);
    // Reconfirm the captured assignment after buffering, before releasing private bytes.
    const confirmed = await readCurrentActor(AbortSignal.any([signal]));
    if (!confirmed.actor) {
      throw new HttpError(confirmed.status === 401 ? 401 : 502, "Session could not be confirmed.");
    }
    if (actorIdentity(confirmed.actor) !== actorIdentity(current.actor)) {
      throw new HttpError(409, "Your account assignment changed. Reload before exporting.");
    }
    return new Response(Buffer.from(bytes), {
      headers: {
        "Content-Type": "text/csv; charset=utf-8",
        "Content-Disposition": 'attachment; filename="intake-appointments.csv"',
        "Cache-Control": "private, no-store",
        "X-Content-Type-Options": "nosniff",
        "X-Robots-Tag": "noindex, nofollow",
      },
    });
  } catch (error) {
    return intakeError(error);
  }
}
