import { readCurrentActor } from "@/lib/auth/server";
import { siteAccountLinks } from "@/lib/auth/site-account";
import { apiJson } from "@/lib/server/http/api";

// The account links a static page asks for after load. No identity or token is returned.
export async function GET(request: Request) {
  const current = await readCurrentActor(request.signal).catch(() => ({
    status: 502,
    actor: null,
  }));
  return apiJson({
    status: 200,
    data: siteAccountLinks(current),
    message: "",
    uncertain: false,
  });
}
