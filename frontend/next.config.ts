import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  distDir: process.env.NEXT_DIST_DIR ?? ".next",
  // Remove X-Powered-By header to avoid fingerprinting the server stack.
  poweredByHeader: false,

  logging: {
    incomingRequests: {
      ignore: [/^\/auth(?:\/|\?)/, /^\/api\/auth(?:\/|\?)/, /^\/dev\/inbox(?:\/|\?|$)/],
    },
  },

  images: {
    // Only proxy images from the configured API origin. Prevents arbitrary
    // external domains from being served through Next.js Image Optimization.
    remotePatterns: [
      {
        protocol: "http",
        hostname: "localhost",
        port: "5114",
        pathname: "/**",
      },
      {
        protocol: "https",
        hostname: "*.apexauto.my",
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
