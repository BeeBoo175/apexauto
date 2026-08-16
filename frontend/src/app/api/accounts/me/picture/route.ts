import { uploadOwnPicture } from "@/lib/profile/picture-routes";

export async function PUT(request: Request) {
  return uploadOwnPicture(request);
}
