import { relayIntake, intakeError } from "@/lib/intake/routes";
import { parseCancellation } from "@/lib/intake/parse";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/server/http/request";
export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return intakeError(new HttpError(400, "Invalid appointment reference."));
  }
  return relayIntake(
    request,
    `/api/intake/appointments/${id}/cancellation`,
    parseCancellation,
    true,
  );
}
