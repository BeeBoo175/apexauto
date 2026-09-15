import { relayRetentionHoldChange, relayRetentionHolds } from "@/lib/settings/settings-routes";

export function GET(request: Request) {
  return relayRetentionHolds(request);
}

export function PUT(request: Request) {
  return relayRetentionHoldChange(request);
}
