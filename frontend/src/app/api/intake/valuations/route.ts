import { relayIntake } from "@/lib/intake/routes";
import { parseQuote } from "@/lib/intake/parse";
export function POST(request: Request) {
  return relayIntake(request, "/api/valuations", parseQuote);
}
