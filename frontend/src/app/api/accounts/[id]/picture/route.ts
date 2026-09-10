import { downloadPicture } from "@/lib/profile/picture-routes";

type Context = { params: Promise<{ id: string }> };

export async function GET(request: Request, context: Context) {
  return downloadPicture(request, (await context.params).id);
}
