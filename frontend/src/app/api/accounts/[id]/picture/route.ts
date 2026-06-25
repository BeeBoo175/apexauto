import { downloadPicture, removePicture } from "@/lib/account/picture-routes";

type Context = { params: Promise<{ id: string }> };

export async function GET(request: Request, context: Context) {
  return downloadPicture(request, (await context.params).id);
}

export async function DELETE(request: Request, context: Context) {
  return removePicture(request, (await context.params).id);
}
