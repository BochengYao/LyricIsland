import type { NextConfig } from "next";

const isEsaStaticExport = process.env.ESA_STATIC_EXPORT === "1";

const nextConfig: NextConfig = {
  ...(isEsaStaticExport
    ? {
        output: "export" as const,
        trailingSlash: true
      }
    : {}),
  images: {
    unoptimized: true
  },
  turbopack: {
    rules: {
      "**/*.{tsx,jsx}": {
        loaders: [
          {
            loader: "@locator/webpack-loader",
            options: {
              env: "development"
            }
          }
        ]
      }
    }
  }
};

export default nextConfig;
