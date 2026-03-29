import { listingPhoto } from "@/lib/vehicles/photo-routes";

type Context = { params: Promise<{ id: string; photoId: string }> };

export async function GET(request: Request, context: Context) {
  const { id, photoId } = await context.params;
  return listingPhoto(request, id, photoId, "download");
}

export async function PUT(request: Request, context: Context) {
  const { id, photoId } = await context.params;
  return listingPhoto(request, id, photoId, "upload");
}
