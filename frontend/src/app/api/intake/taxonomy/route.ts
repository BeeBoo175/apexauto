import { relayIntake } from "@/lib/intake/routes";
import { parseTaxonomy } from "@/lib/intake/parse";
export function GET(request: Request) {
  return relayIntake(request, "/api/taxonomy", parseTaxonomy);
}
