import { relayPerformanceSection } from "@/lib/performance/performance-routes";

export async function GET(request: Request, context: { params: Promise<{ section: string }> }) {
  const { section } = await context.params;
  return relayPerformanceSection(request, section);
}
