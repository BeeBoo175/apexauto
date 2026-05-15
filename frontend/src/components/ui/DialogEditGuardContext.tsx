"use client";

import { createContext } from "react";
import type { InteractionGuard } from "@/lib/interaction-guard";

export const DialogEditGuardContext = createContext<InteractionGuard | null>(null);
