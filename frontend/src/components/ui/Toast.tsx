"use client";

import React, { createContext, useContext, useState, useCallback, useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import type { IconName } from "@/lib/icons";
import { Icon } from "./Icon";
import styles from "./Toast.module.css";

export type ToastType = "success" | "error" | "warning" | "info";

export interface ToastMessage {
  id: string;
  type: ToastType;
  message: string;
}

interface ToastContextValue {
  toast: (message: string, type?: ToastType) => void;
  success: (message: string) => void;
  error: (message: string) => void;
  warning: (message: string) => void;
  info: (message: string) => void;
}

const ToastContext = createContext<ToastContextValue | undefined>(undefined);

const toastIcons: Record<ToastType, IconName> = {
  success: "check",
  error: "alert-circle",
  warning: "alert-triangle",
  info: "info-circle",
};

// A failure stays long enough to read and act on; the others confirm something the person just did.
const toastDurations: Record<ToastType, number> = {
  success: 4000,
  info: 4000,
  warning: 6000,
  error: 8000,
};

export const ToastProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((message: string, type: ToastType = "info") => {
    const id = Math.random().toString(36).substring(2, 9);
    setToasts((prev) => [...prev, { id, type, message }]);
  }, []);

  const dismiss = useCallback((id: string) => {
    setToasts((prev) => prev.filter((toast) => toast.id !== id));
  }, []);

  const value = {
    toast: addToast,
    success: useCallback((msg: string) => addToast(msg, "success"), [addToast]),
    error: useCallback((msg: string) => addToast(msg, "error"), [addToast]),
    warning: useCallback((msg: string) => addToast(msg, "warning"), [addToast]),
    info: useCallback((msg: string) => addToast(msg, "info"), [addToast]),
  };

  return (
    <ToastContext.Provider value={value}>
      {children}
      <ToastViewport>
        {toasts.map((toast) => (
          <ToastItem key={toast.id} toast={toast} onDismiss={dismiss} />
        ))}
      </ToastViewport>
    </ToastContext.Provider>
  );
};

// One notification: it closes itself after its time, waits while the pointer or keyboard focus is on it, and can be
// dismissed at once.
function ToastItem({ toast, onDismiss }: { toast: ToastMessage; onDismiss: (id: string) => void }) {
  const [held, setHeld] = useState(false);
  const remaining = useRef(toastDurations[toast.type]);

  useEffect(() => {
    if (held) {
      return;
    }

    const started = Date.now();
    const timer = window.setTimeout(() => onDismiss(toast.id), remaining.current);
    return () => {
      window.clearTimeout(timer);
      remaining.current = Math.max(remaining.current - (Date.now() - started), 1000);
    };
  }, [held, onDismiss, toast.id]);

  return (
    <div
      role={toast.type === "error" ? "alert" : "status"}
      className={`${styles.toast} ${styles[toast.type]}`}
      onMouseEnter={() => setHeld(true)}
      onMouseLeave={() => setHeld(false)}
      onFocus={() => setHeld(true)}
      onBlur={() => setHeld(false)}
    >
      <span className={styles.icon}>
        <Icon name={toastIcons[toast.type]} />
      </span>
      <p className={styles.message}>{toast.message}</p>
      <button
        type="button"
        className={styles.dismiss}
        aria-label="Dismiss notification"
        onClick={() => onDismiss(toast.id)}
      >
        <Icon name="close" />
      </button>
    </div>
  );
}

function ToastViewport({ children }: { children: React.ReactNode }) {
  const [host, setHost] = useState<HTMLElement | null>(null);

  useEffect(() => {
    function updateHost() {
      const dialogs = document.querySelectorAll<HTMLDialogElement>("dialog[open]");
      setHost(dialogs.item(dialogs.length - 1) ?? document.body);
    }
    updateHost();
    document.addEventListener("apexauto-dialog-change", updateHost);
    return () => {
      document.removeEventListener("apexauto-dialog-change", updateHost);
    };
  }, []);

  const viewport = (
    <div aria-label="Notifications" className={styles.viewport}>
      {children}
    </div>
  );
  return host ? createPortal(viewport, host) : viewport;
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return context;
}
