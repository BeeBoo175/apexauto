import { useState } from "react";

// Tracks one photograph for the shared shimmering placeholder: loading until it loads or fails. A photograph that
// settled before hydration never fires its events, so the ref reads the element's own state.
export function usePhotoLoading(url: string | null, onFailure?: () => void) {
  const [settled, setSettled] = useState<string | null>(null);

  return {
    loading: url !== null && settled !== url,
    imageProps: {
      ref: (element: HTMLImageElement | null) => {
        if (url === null || settled === url || !element?.complete) {
          return;
        }
        setSettled(url);
        if (element.naturalWidth === 0) {
          onFailure?.();
        }
      },
      onLoad: () => setSettled(url),
      onError: () => {
        setSettled(url);
        onFailure?.();
      },
    },
  };
}
