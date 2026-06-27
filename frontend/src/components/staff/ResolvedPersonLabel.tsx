"use client";

import { usePersonPreview } from "@/hooks/use-person-preview";
import { PersonLabel } from "./PersonLabel";

// For records that carry only a person's id and text identity: the picture arrives from the shared
// person read, and the identity text shows meanwhile. `deferred` (long lists such as histories) reads nothing
// on its own: the person is read once, when someone hovers, focuses or opens the label, and every other label
// of that person then shows the picture without another request.
export function ResolvedPersonLabel({
  id,
  identity,
  nested = false,
  deferred = false,
}: {
  id: string;
  identity: string;
  nested?: boolean;
  deferred?: boolean;
}) {
  const { person } = usePersonPreview(id, !deferred);

  return (
    <PersonLabel
      id={id}
      name={person?.name ?? identity}
      identity={identity}
      pictureId={person?.pictureId ?? null}
      nested={nested}
    />
  );
}
