import type { NextConfig } from "next";
import path from "path";
import { loadEnvConfig } from "@next/env";

if (process.env.NODE_ENV !== "production") {
  // Reload at the monorepo root after Next.js has processed the frontend directory.
  loadEnvConfig(
    path.resolve(process.cwd(), ".."),
    process.env.NODE_ENV === "development",
    {
      info: console.info,
      error(message: string) {
        throw new Error(message);
      },
    },
    true,
  );
}

const apiBaseUrl = process.env.API_BASE_URL;

const appBaseUrl = process.env.APP_BASE_URL;

if (
  !appBaseUrl ||
  new URL(appBaseUrl).origin !== appBaseUrl ||
  !["http:", "https:"].includes(new URL(appBaseUrl).protocol)
) {
  throw new Error("APP_BASE_URL must be an absolute HTTP(S) origin.");
}

if (!apiBaseUrl?.trim()) {
  throw new Error("API_BASE_URL is required in every environment.");
}

let parsedApiUrl: URL;

try {
  parsedApiUrl = new URL(apiBaseUrl);
} catch {
  throw new Error("API_BASE_URL must be an absolute HTTP(S) origin.");
}

if (
  !["http:", "https:"].includes(parsedApiUrl.protocol) ||
  parsedApiUrl.username ||
  parsedApiUrl.password ||
  parsedApiUrl.pathname !== "/" ||
  parsedApiUrl.search ||
  parsedApiUrl.hash
) {
  throw new Error(
    "API_BASE_URL must be an HTTP(S) origin without credentials, a path, query or fragment.",
  );
}

const imageProtocol = parsedApiUrl.protocol === "https:" ? "https" : "http";

const nextConfig: NextConfig = {
  ...(process.env.NEXT_DIST_DIR ? { distDir: process.env.NEXT_DIST_DIR } : {}),
  // Remove X-Powered-By header to avoid fingerprinting the server stack.
  poweredByHeader: false,

  logging: {
    incomingRequests: {
      ignore: [/^\/auth(?:\/|\?)/, /^\/api\/auth(?:\/|\?)/, /^\/dev\/inbox(?:\/|\?|$)/],
    },
  },

  images: {
    // Add a separate exact delivery origin when the storage workflow is implemented.
    remotePatterns: [
      {
        protocol: imageProtocol,
        hostname: parsedApiUrl.hostname,
        port: parsedApiUrl.port,
        pathname: "/**",
      },
    ],
  },

  async headers() {
    return [
      {
        source: "/(.*)",
        headers: [
          // Prevents clickjacking by forbidding the site from being framed.
          { key: "X-Frame-Options", value: "DENY" },
          // Stops browsers from MIME-sniffing a response away from the declared content-type.
          { key: "X-Content-Type-Options", value: "nosniff" },
          // Sends full origin on same-origin requests; only sends origin on cross-origin HTTPS.
          { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
          // Disallows framing from any source via the modern CSP equivalent.
          { key: "Content-Security-Policy", value: "frame-ancestors 'none'" },
          // Enables HTTPS for 1 year; subdomains included.
          { key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" },
        ],
      },
      {
        source: "/account/:path*",
        headers: [
          { key: "Cache-Control", value: "no-store" },
          { key: "X-Robots-Tag", value: "noindex, nofollow, noarchive, nosnippet" },
        ],
      },
      {
        source: "/sell/:rest*",
        headers: [
          { key: "Cache-Control", value: "private, no-store" },
          { key: "X-Robots-Tag", value: "noindex, nofollow, noarchive, nosnippet" },
        ],
      },
      {
        source: "/:path(login|register|verify-email|forgot-password|reset-password)",
        headers: [
          { key: "Cache-Control", value: "no-store" },
          { key: "Referrer-Policy", value: "no-referrer" },
          { key: "X-Robots-Tag", value: "noindex, nofollow, noarchive, nosnippet" },
        ],
      },
      {
        source: "/api/:path*",
        headers: [{ key: "Cache-Control", value: "no-store" }],
      },
      {
        source: "/auth/:path*",
        headers: [
          { key: "Referrer-Policy", value: "no-referrer" },
          { key: "Cache-Control", value: "no-store" },
        ],
      },
      {
        source: "/dev/inbox",
        headers: [
          { key: "Referrer-Policy", value: "no-referrer" },
          { key: "Cache-Control", value: "no-store" },
        ],
      },
    ];
  },
};

export default nextConfig;
