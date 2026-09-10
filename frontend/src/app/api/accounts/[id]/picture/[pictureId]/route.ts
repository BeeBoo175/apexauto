import { removePicture } from "@/lib/profile/picture-routes";

type Context = { params: Promise<{ id: string; pictureId: string }> };

export async function DELETE(request: Request, context: Context) {
  const { id, pictureId } = await context.params;
  return removePicture(request, id, pictureId);
}
