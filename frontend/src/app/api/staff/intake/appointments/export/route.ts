import { sessionToken } from "@/lib/server/http/api";
import { apiError } from "@/lib/server/http/api";
import { requireSameOrigin } from "@/lib/server/http/request";
import { relayPrivateCsv } from "@/lib/server/http/csv-export";

export async function GET(request: Request) {
  if (request.method === "HEAD") {
    const response = await exportCsv(new Request(request, { method: "GET" }), false);
    await response.body?.cancel();
    return new Response(null, { status: response.status, headers: response.headers });
  }
  return exportCsv(request, false);
}

export async function POST(request: Request) {
  return exportCsv(request, true);
}

async function exportCsv(request: Request, background: boolean) {
  try {
    if (background) {
      requireSameOrigin(request);
    }
    const input = new URL(request.url).searchParams;
    const query = new URLSearchParams();
    for (const key of ["from", "to", "status", "branchId", "search", "sort", "direction"]) {
      if (input.has(key)) {
        query.set(key, input.get(key)!);
      }
    }
    const suffix = background ? "/background" : "";
    return await relayPrivateCsv(
      request,
      `/api/staff/intake/appointments/export${suffix}?${query}`,
      await sessionToken(),
      {
        deadlineMs: background ? 125000 : 22000,
        maximumBytes: (background ? 64 : 8) * 1024 * 1024,
        filename: "intake-appointments.csv",
      },
    );
  } catch (error) {
    return apiError(error);
  }
}
