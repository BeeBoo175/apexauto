export const PAGE_SIZES = [10, 25, 50] as const;
export const DEFAULT_PAGE_SIZE = 25;
// A customer's own history lists show a short page; matches Pagination.AccountPageSize on the API.
export const ACCOUNT_PAGE_SIZE = 5;

export function isPageSize(value: unknown): value is number {
  return typeof value === "number" && PAGE_SIZES.some((size) => size === value);
}

export function paginationQuery(
  page: number | string = 1,
  pageSize: number | string = DEFAULT_PAGE_SIZE,
) {
  return new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
}

// A page answers its request only when it echoes the requested page and size.
export function echoesPage<T extends { page: number; pageSize: number }>(
  page: T | null,
  query: URLSearchParams,
) {
  const matches =
    page?.page === Number(query.get("page") ?? 1) &&
    page.pageSize === Number(query.get("pageSize") ?? DEFAULT_PAGE_SIZE);
  return matches ? page : null;
}
