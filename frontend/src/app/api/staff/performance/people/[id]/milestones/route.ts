import { relayPerformanceMilestones } from "@/lib/performance/performance-routes";

export async function GET(request: Request, context: { params: Promise<{ id: string }> }) {
  const { id } = await context.params;
  return relayPerformanceMilestones(request, id);
}
