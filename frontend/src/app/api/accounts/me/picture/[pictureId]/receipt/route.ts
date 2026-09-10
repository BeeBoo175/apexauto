import { ownPictureReceipt } from "@/lib/profile/picture-routes";

export async function GET(request: Request, context: { params: Promise<{ pictureId: string }> }) {
  return ownPictureReceipt(request, (await context.params).pictureId);
}
