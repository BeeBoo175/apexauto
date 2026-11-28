// The readiness report may be read from the browser by the portfolio only. Origins come from configuration, are
// compared exactly, and anything else gets no CORS header at all.

export function configuredOrigins(value: string | undefined) {
  return (value ?? "")
    .split(",")
    .map((item) => item.trim())
    .filter((item) => {
      try {
        return (
          item.length > 0 &&
          new URL(item).origin === item &&
          /^https?:$/.test(new URL(item).protocol)
        );
      } catch {
        return false;
      }
    });
}

export function allowedOrigin(requestOrigin: string | null, value: string | undefined) {
  if (!requestOrigin) {
    return null;
  }
  return configuredOrigins(value).includes(requestOrigin) ? requestOrigin : null;
}
