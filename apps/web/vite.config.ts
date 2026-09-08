import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      "@": path.resolve(import.meta.dirname, "./src"),
    },
  },
  server: {
    proxy: {
      "/health": "http://127.0.0.1:5055",
      "/api": "http://127.0.0.1:5055",
      "/hubs": { target: "http://127.0.0.1:5055", ws: true },
    },
  },
});
