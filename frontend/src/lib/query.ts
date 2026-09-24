export type SearchParams = Record<string, string | string[] | undefined>;

export function withQuery(path: string, query: URLSearchParams) {
  return query.size > 0 ? `${path}?${query}` : path;
}

export type ListDirection = "asc" | "desc";

// A list's sort column and direction from its search parameters; an unknown column falls back to the canonical order.
export function listOrderParams(
  text: (key: string) => string,
  sortKeys: readonly string[],
): { sort: string; direction: ListDirection } {
  return {
    sort: sortKeys.includes(text("sort")) ? text("sort") : "",
    direction: text("direction") === "desc" ? "desc" : "asc",
  };
}

// Ascending is the default order, so only a descending direction on a sorted list is carried in the query.
export function isOmittedDirection(key: string, value: unknown, sort: string) {
  return key === "direction" && (value === "asc" || !sort);
}
