import { relayStaffTestDriveCommand } from "@/lib/test-drives/staff-command-routes";

export async function POST(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayStaffTestDriveCommand(request, id, "assignments");
}
