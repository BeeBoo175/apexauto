import { ResolvedPersonLabel } from "./ResolvedPersonLabel";

// A customer shown like a handler: the avatar beside the recorded name, opening the shared person profile, which
// shows a customer by name only. The name stays plain text where a read does not carry the account, and
// sorting, search and CSV keep using the text name, which stays available to assistive technology.
export function CustomerAvatar({
  id,
  name,
  nested = false,
}: {
  id: string | null;
  name: string;
  // Inside a button or link the avatar stays a plain element so the row's own control remains valid.
  nested?: boolean;
}) {
  if (!id) {
    return name;
  }
  return (
    <>
      <ResolvedPersonLabel id={id} identity={name} nested={nested} deferred />
      <span className="sr-only">{name}</span>
    </>
  );
}
