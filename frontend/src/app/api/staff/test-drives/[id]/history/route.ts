import { relayStaffTestDriveHistory } from "@/lib/test-drives/staff-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayStaffTestDriveHistory(request, id);
}
