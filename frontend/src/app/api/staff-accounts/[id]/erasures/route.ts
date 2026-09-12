import { relayErasure } from "@/lib/accounts/account-erasure-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayErasure(request, "staff", id);
}
