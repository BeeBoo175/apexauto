import { relaySaleVehiclePhoto } from "@/lib/sales/routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; photoId: string }> },
) {
  const { id, photoId } = await context.params;
  return relaySaleVehiclePhoto(request, id, photoId);
}
