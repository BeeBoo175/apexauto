import { isUuid } from "@/lib/auth/contracts";
import { apiJson, apiRead } from "@/lib/server/http/api";
import { parsePublicDetail } from "@/lib/vehicles/public-detail";

export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isUuid(id)) {
    return apiJson({ status: 404, data: null, message: "Vehicle unavailable.", uncertain: false });
  }
  return apiJson(
    await apiRead(`/api/vehicles/${id}`, (value) => parsePublicDetail(value, id), {
      signal: request.signal,
    }),
  );
}
