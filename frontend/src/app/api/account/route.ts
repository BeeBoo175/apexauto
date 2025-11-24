import { authFailure, authJson, upstreamDenial } from "@/lib/auth/http";
import { readOwnAccount } from "@/lib/auth/server";
export async function GET(request: Request) {
  try {
    const response = await readOwnAccount(request.signal);

    if (!response.account) {
      return upstreamDenial(response.status);
    }

    return authJson(response.account);
  } catch (error) {
    return authFailure(error);
  }
}
