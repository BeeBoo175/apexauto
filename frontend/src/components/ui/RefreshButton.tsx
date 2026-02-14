import { useState } from "react";
import { Button } from "./Button";
import { Icon } from "./Icon";

export function RefreshButton({
  label,
  busy,
  disabled,
  onClick,
}: {
  label: string;
  busy: boolean;
  disabled: boolean;
  onClick: () => void;
}) {
  const [acknowledging, setAcknowledging] = useState(busy);
  if (busy && !acknowledging) {
    setAcknowledging(true);
  }
  return (
    <Button
      size="icon"
      variant="ghost"
      aria-label={label}
      title={busy ? "Refreshing…" : label}
      aria-busy={busy}
      className="disabled:opacity-100"
      disabled={disabled || busy}
      onClick={() => {
        setAcknowledging(true);
        onClick();
      }}
      onAnimationIteration={() => {
        // Finish the current turn even when an unchanged response arrives immediately.
        if (!busy) {
          setAcknowledging(false);
        }
      }}
    >
      <Icon name="refresh" spinning={busy || acknowledging} />
    </Button>
  );
}
