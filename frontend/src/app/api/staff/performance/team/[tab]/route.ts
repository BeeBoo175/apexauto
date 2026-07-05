import { relayPerformanceTeam } from "@/lib/performance/performance-routes";

export async function GET(request: Request, context: { params: Promise<{ tab: string }> }) {
  const { tab } = await context.params;
  return relayPerformanceTeam(request, tab);
}
