import { isUuid } from "@/lib/auth/contracts";
import { intakeError, relayIntake } from "@/lib/intake/routes";
import { HttpError } from "@/lib/server/http/request";
import { parseCheckIn } from "@/lib/staff/parse";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return intakeError(new HttpError(400, "Provide a valid appointment reference."));
  }
  return relayIntake(
    request,
    `/api/staff/intake/appointments/${id}/no-show-correction`,
    parseCheckIn,
    true,
  );
}
