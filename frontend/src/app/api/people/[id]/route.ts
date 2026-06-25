import { relayPerson } from "@/lib/account/person-route";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  return relayPerson(request, (await context.params).id);
}
