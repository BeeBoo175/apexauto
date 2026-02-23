import { isUuid } from "@/lib/auth/contracts";
import { apiError } from "@/lib/server/http/api";
import { relayIntake } from "@/lib/intake/routes";
import { HttpError } from "@/lib/http";
import { parseDetail } from "@/lib/staff/parse";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  if (!isUuid(id)) {
    return apiError(new HttpError(400, "Provide a valid appointment reference."));
  }
  return relayIntake(request, `/api/staff/intake/appointments/${id}`, parseDetail, true);
}
