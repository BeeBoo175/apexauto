import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";

// The create action of a staff list or queue: an icon with a plus beside the search, named for assistive
// technology and on hover, because the label would crowd the toolbar.
export function AssistedStartButton({
  label,
  disabled = false,
  onClick,
}: {
  label: string;
  disabled?: boolean;
  onClick: () => void;
}) {
  return (
    <Button
      variant="secondary"
      size="icon"
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
    >
      <Icon name="plus" />
    </Button>
  );
}
