// Pages keep what they show in the address, so a reload or a shared link returns to the same view: filters, page,
// sort, the open record and its tab. Empty values leave the address, so a default view has a clean address.
export function replaceUrlParams(values: Record<string, string>) {
  const url = new URL(window.location.href);
  for (const [key, value] of Object.entries(values)) {
    if (value) {
      url.searchParams.set(key, value);
    } else {
      url.searchParams.delete(key);
    }
  }
  window.history.replaceState(null, "", url);
}
