import { changeUsername } from "@/lib/account/username-route";

export async function PUT(request: Request) {
  return changeUsername(request);
}
