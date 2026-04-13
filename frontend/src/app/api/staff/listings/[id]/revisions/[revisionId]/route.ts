import { isUuid } from "@/lib/auth/contracts";
import { HttpError } from "@/lib/http";
import { apiError } from "@/lib/server/http/api";
import { relayListingRead } from "@/lib/vehicles/listing-routes";
import { parseListingRevisionRead } from "@/lib/vehicles/listing-history";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; revisionId: string }> },
) {
  const { id, revisionId } = await context.params;
  if (!isUuid(revisionId)) {
    return apiError(new HttpError(400, "Choose a valid listing revision."));
  }
  return relayListingRead(
    request,
    id,
    `/revisions/${revisionId}`,
    parseListingRevisionRead,
    (value) => value.vehicleId === id && value.revision.id === revisionId,
  );
}
