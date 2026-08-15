import { CustomerAvatar } from "@/components/staff/CustomerAvatar";
import { ResolvedPersonLabel } from "@/components/staff/ResolvedPersonLabel";
import type { ListingWorkspace } from "@/lib/vehicles/listing-workspace";
import { ListingPendingDeposit } from "./ListingPendingDeposit";

// Who has the car: its buyer and the agent handling the sale, a customer asked to confirm a deposit, or no one.
export function ListingHeldBy({
  snapshot,
  onChanged,
}: {
  snapshot: ListingWorkspace;
  onChanged: () => void;
}) {
  const { hold, pendingDeposit } = snapshot;
  if (!hold && pendingDeposit) {
    return <ListingPendingDeposit pending={pendingDeposit} onChanged={onChanged} />;
  }
  if (!hold) {
    return "Not on hold";
  }
  return (
    <>
      <CustomerAvatar id={hold.buyerId} name={hold.buyer} />
      {hold.agent && <HoldAgent agent={hold.agent} agentId={hold.agentId} />}
    </>
  );
}

function HoldAgent({ agent, agentId }: { agent: string; agentId: string | null }) {
  return (
    <>
      {" · handled by "}
      {agentId ? <ResolvedPersonLabel id={agentId} identity={agent} deferred /> : agent}
    </>
  );
}
