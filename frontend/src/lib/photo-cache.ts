// A stored photograph's address names one picture whose contents never change, so a successful download may stay
// for a day in that browser only, never in a shared cache. Errors and every other response stay no-store.
export const PHOTO_CACHE_CONTROL = "private, max-age=86400";
