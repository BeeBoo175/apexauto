"use client";

import { useEffect, useRef } from "react";
import { useToast } from "@/components/ui/Toast";

export function useInitialToast(message: string | null | undefined) {
  const announced = useRef<string | null>(null);
  const { error } = useToast();

  useEffect(() => {
    if (!message || announced.current === message) {
      return;
    }
    announced.current = message;
    error(message);
  }, [message, error]);
}
