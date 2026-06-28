import { relayStaffAccountCommand } from "@/lib/accounts/account-administration-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayStaffAccountCommand(request, id, "transfers");
}
