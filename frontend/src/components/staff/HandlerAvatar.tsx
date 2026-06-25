import { Avatar } from "@/components/ui/Avatar";

// A handler column shows the avatar alone; sorting, search and CSV keep using the text identity,
// which stays available to assistive technology beside the avatar.
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
      <Avatar
        accountId={id}
        name={name ?? identity}
        pictureId={pictureId}
        size="sm"
        detail={identity}
      />
      <span className="sr-only">{identity}</span>
    </>
  );
}
