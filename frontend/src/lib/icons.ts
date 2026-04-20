export const iconFiles = {
  car: "car.svg",
  engine: "engine.svg",
  armchair: "armchair.svg",
  tool: "tool.svg",
  inspection: "shield-search.svg",
  calendar: "calendar.svg",
  table: "table.svg",
  previous: "chevron-left.svg",
  next: "chevron-right.svg",
  refresh: "refresh.svg",
  search: "search.svg",
  close: "close.svg",
  down: "chevron-down.svg",
  filter: "filter.svg",
  "table-filter": "bootstrap-funnel.svg",
  "table-sort-ascending": "bootstrap-arrow-up.svg",
  "table-sort-descending": "bootstrap-arrow-down.svg",
} as const;

export type IconName = keyof typeof iconFiles;
