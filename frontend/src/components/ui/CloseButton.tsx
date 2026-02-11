import { Button, type ButtonProps } from "./Button";

export function CloseButton({
  label = "Close dialog",
  size = "compact",
  disabled = false,
  onClick,
}: {
  label?: string;
  size?: "compact" | "md";
  disabled?: boolean;
  onClick: ButtonProps["onClick"];
}) {
  return (
    <Button
      type="button"
      variant="ghost"
      size={size}
      aria-label={label}
      disabled={disabled}
      onClick={onClick}
    >
      Close
    </Button>
  );
}
