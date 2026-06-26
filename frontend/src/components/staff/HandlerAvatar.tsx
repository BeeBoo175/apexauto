import { PersonLabel } from "./PersonLabel";

// A handler column shows the avatar beside the name; sorting, search and CSV keep using the text
// identity, which stays available to assistive technology and text lookups.
export function HandlerAvatar({
  id,
  identity,
  name,
  pictureId,
}: {
  id: string;
  identity: string;
  name: string | null;
  pictureId: string | null;
}) {
  return (
    <>
      <PersonLabel id={id} name={name ?? identity} identity={identity} pictureId={pictureId} />
      <span className="sr-only">{identity}</span>
    </>
  );
}
