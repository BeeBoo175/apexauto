import { relayIntake } from "@/lib/intake/routes";
import { apiError } from "@/lib/server/http/api";
import { parseSlots } from "@/lib/intake/parse";
import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
export function GET(request: Request) {
  const query = new URL(request.url).searchParams;
  const branchId = query.get("branchId");
  if (!isUuid(branchId)) {
    return apiError(new HttpError(400, "Choose a branch."));
  }
  return relayIntake(
    request,
    `/api/branches/${branchId}/inspection-slots?date=${encodeURIComponent(query.get("date") ?? "")}`,
    parseSlots,
  );
}
