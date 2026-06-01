import { relayTestDriveSlots } from "@/lib/test-drives/customer-server";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayTestDriveSlots(request, id);
}
