import type { ReactNode } from "react";

export interface CalendarEvent<Data = unknown> {
  id: string;
  title: string;
  startsAt: string;
  endsAt: string;
  label?: string;
  tone?: "info" | "success" | "neutral";
  data?: Data;
}

export interface CalendarProps<Data = unknown> {
  label: string;
  events: readonly CalendarEvent<Data>[];
  visibleDate: string;
  minDate?: string;
  maxDate?: string;
  today: string;
  locale: string;
  timeZone: string;
  disabled?: boolean;
  busy?: boolean;
  confirmed?: boolean;
  fit?: boolean;
  toolbar?: ReactNode;
  onMonthChange: (date: string) => void;
  onEventSelect: (event: CalendarEvent<Data>) => void;
  renderEvent?: (event: CalendarEvent<Data>) => ReactNode;
}
