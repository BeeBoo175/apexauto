"use client";

import { createContext } from "react";
import type { createInteractionGuard } from "@/lib/interaction-guard";

export const DialogEditGuardContext = createContext<ReturnType<
  typeof createInteractionGuard
> | null>(null);
