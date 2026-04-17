import { relayIntake } from "@/lib/intake/routes";
import { apiError } from "@/lib/server/http/api";
import { parseCancellation } from "@/lib/intake/parse";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Invalid appointment reference."));
  }
  return relayIntake(
    request,
    `/api/intake/appointments/${id}/cancellations`,
    parseCancellation,
    true,
  );
}
