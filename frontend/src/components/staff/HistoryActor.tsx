import { ResolvedPersonLabel } from "./ResolvedPersonLabel";

// Who acted in a history entry: the person label, read only when someone points at it and shared with every
// other label of that person; the plain name for the system.
export function HistoryActor({ id, name }: { id: string | null; name: string }) {
  return id ? <ResolvedPersonLabel id={id} identity={name} deferred /> : <span>{name}</span>;
}
