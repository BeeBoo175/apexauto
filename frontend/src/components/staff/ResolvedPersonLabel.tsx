"use client";

import { usePersonPreview } from "@/hooks/use-person-preview";
import { PersonLabel } from "./PersonLabel";

// For records that carry only a person's id and text identity: the picture arrives from the shared
// person read, and the identity text shows meanwhile.
export function ResolvedPersonLabel({
  id,
  identity,
  linked = true,
}: {
  id: string;
  identity: string;
  linked?: boolean;
}) {
  const person = usePersonPreview(id, true);

  return (
    <PersonLabel
      id={id}
      name={person?.name ?? identity}
      identity={identity}
      pictureId={person?.pictureId ?? null}
      linked={linked}
    />
  );
}
