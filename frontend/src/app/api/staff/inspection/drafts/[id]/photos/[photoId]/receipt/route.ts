import { relayInspectionResource } from "@/lib/inspection/routes";
import { parsePhoto } from "@/lib/inspection/parse-report";
import { isUuid } from "@/lib/auth/contracts";
import { apiError } from "@/lib/server/http/api";
import { HttpError } from "@/lib/http";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; photoId: string }> },
) {
  const { id, photoId } = await context.params;
  if (!isUuid(photoId)) {
    return apiError(new HttpError(400, "Choose a valid photo ID."));
  }
  return relayInspectionResource(request, id, `drafts/${id}/photos/${photoId}/receipt`, parsePhoto);
}
