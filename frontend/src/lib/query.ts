export type SearchParams = Record<string, string | string[] | undefined>;

export function withQuery(path: string, query: URLSearchParams) {
  return query.size > 0 ? `${path}?${query}` : path;
}
