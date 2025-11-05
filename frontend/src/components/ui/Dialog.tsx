"use client";

import React, { useEffect } from "react";
import { Button } from "./Button";

export interface DialogProps {
  open: boolean;
  onClose: () => void;
  title: string;
  description?: string;
  children: React.ReactNode;
}

export const Dialog: React.FC<DialogProps> = ({ open, onClose, title, description, children }) => {
  useEffect(() => {
    if (open) {
      document.body.style.overflow = "hidden";
    } else {
      document.body.style.overflow = "";
    }
    return () => {
      document.body.style.overflow = "";
    };
  }, [open]);

  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-graphite/60 backdrop-blur-xs">
      <div
        className="w-full max-w-lg bg-paper border border-line rounded-sm p-6 shadow-xl relative"
        role="dialog"
        aria-modal="true"
      >
        <div className="flex items-start justify-between border-b border-soft-line pb-4 mb-4">
          <div>
            <h2 className="text-base font-bold uppercase tracking-wider text-graphite">{title}</h2>
            {description && <p className="text-xs text-muted mt-1">{description}</p>}
          </div>
          <Button variant="ghost" size="sm" onClick={onClose} aria-label="Close dialog">
            [X]
          </Button>
        </div>
        <div>{children}</div>
      </div>
    </div>
  );
};
