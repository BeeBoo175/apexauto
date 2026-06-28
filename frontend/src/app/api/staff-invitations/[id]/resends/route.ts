import { relayInvitationCommand } from "@/lib/accounts/staff-invitations-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayInvitationCommand(request, id, "resends");
}
