import { listingPhoto } from "@/lib/vehicles/photo-routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; photoId: string }> },
) {
  const { id, photoId } = await context.params;
  return listingPhoto(request, id, photoId, "receipt");
}
