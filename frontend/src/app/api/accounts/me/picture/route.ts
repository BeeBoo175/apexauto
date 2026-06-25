import { uploadOwnPicture } from "@/lib/account/picture-routes";

export async function PUT(request: Request) {
  return uploadOwnPicture(request);
}
