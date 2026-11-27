import { useState } from "react";

// A card that browses photos keeps each loaded one in place, so going back shows it at once, and keeps the
// current photo on screen until the next has loaded. A photo that failed leaves the placeholder instead.
export function useKeptPhotos(url: string | null, pending: boolean) {
  const [loaded, setLoaded] = useState<string[]>([]);
  const [failedAt, setFailedAt] = useState<Record<string, number>>({});
  const [lastShown, setLastShown] = useState<string | null>(null);
  const ready = url !== null && loaded.includes(url);

  if (ready && lastShown !== url) {
    setLastShown(url);
  }

  function shownPhoto() {
    if (ready) {
      return url;
    }
    const waiting = url === null ? pending : !(url in failedAt);
    return waiting ? lastShown : null;
  }

  function markLoaded(photo: string) {
    if (!loaded.includes(photo)) {
      setLoaded((current) => (current.includes(photo) ? current : [...current, photo]));
    }
  }

  function markFailed(photo: string, version: number) {
    setFailedAt((current) => (photo in current ? current : { ...current, [photo]: version }));
  }

  return {
    photos: url === null || loaded.includes(url) ? loaded : [...loaded, url],
    shownUrl: shownPhoto(),
    failedAt,
    markLoaded,
    markFailed,
  };
}
