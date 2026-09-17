import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, import.meta.dirname, "");
  const apiTarget = env.VITE_API_PROXY_TARGET || "http://127.0.0.1:5055";

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        "@": path.resolve(import.meta.dirname, "./src"),
      },
    },
    server: {
      proxy: {
        "/health": { target: apiTarget, changeOrigin: true, secure: true },
        "/api": { target: apiTarget, changeOrigin: true, secure: true },
        "/hubs": { target: apiTarget, changeOrigin: true, secure: true, ws: true },
      },
    },
  };
});
