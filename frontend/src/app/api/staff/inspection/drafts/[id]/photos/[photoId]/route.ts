import { inspectionPhoto } from "@/lib/inspection/photo-routes";

type Context = { params: Promise<{ id: string; photoId: string }> };
export async function PUT(request: Request, context: Context) {
  const { id, photoId } = await context.params;
  return inspectionPhoto(request, id, photoId);
}
export async function GET(request: Request, context: Context) {
  const { id, photoId } = await context.params;
  return inspectionPhoto(request, id, photoId);
}
