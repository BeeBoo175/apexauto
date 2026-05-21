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
