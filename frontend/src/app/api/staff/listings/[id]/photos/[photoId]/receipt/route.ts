import { listingPhoto } from "@/lib/vehicles/listing-photo-routes";

export async function GET(
  request: Request,
  context: { params: Promise<{ id: string; photoId: string }> },
) {
  const { id, photoId } = await context.params;
  return listingPhoto(request, id, photoId, "receipt");
}
