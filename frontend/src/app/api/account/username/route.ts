import { changeUsername } from "@/lib/profile/username-route";

export async function PUT(request: Request) {
  return changeUsername(request);
}
