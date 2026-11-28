import { useState } from "react";

// A card that browses photos keeps each loaded one in place, so going back shows it at once. A photo that has
// not loaded yet shows the placeholder straight away, shimmering while it loads and still when it failed.
export function useKeptPhotos(url: string | null, pending: boolean) {
  const [loaded, setLoaded] = useState<string[]>([]);
  const [failedAt, setFailedAt] = useState<Record<string, number>>({});
  const ready = url !== null && loaded.includes(url);

  function markLoaded(photo: string) {
    if (!loaded.includes(photo)) {
      setLoaded((current) => (current.includes(photo) ? current : [...current, photo]));
    }
  }

  function markFailed(photo: string, version: number) {
    setFailedAt((current) => (photo in current ? current : { ...current, [photo]: version }));
  }

  return {
    photos: url === null || ready ? loaded : [...loaded, url],
    shownUrl: ready ? url : null,
    loading: url === null ? pending : !ready && !(url in failedAt),
    failedAt,
    markLoaded,
    markFailed,
  };
}
