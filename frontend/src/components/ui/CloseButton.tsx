import { Button, type ButtonProps } from "./Button";
import { Icon } from "./Icon";

export function CloseButton({
  label = "Close dialog",
  ...props
}: Omit<ButtonProps, "children" | "size"> & {
  label?: string;
}) {
  return (
    <Button type="button" variant="ghost" {...props} size="icon" aria-label={label} title={label}>
      <Icon name="close" />
    </Button>
  );
}
