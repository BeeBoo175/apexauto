import { relayAccountEdit } from "@/lib/accounts/account-edit-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayAccountEdit(request, id);
}
