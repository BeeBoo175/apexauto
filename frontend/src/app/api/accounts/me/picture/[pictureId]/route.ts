import { uploadOwnPicture } from "@/lib/profile/picture-routes";

export async function PUT(request: Request, context: { params: Promise<{ pictureId: string }> }) {
  return uploadOwnPicture(request, (await context.params).pictureId);
}
