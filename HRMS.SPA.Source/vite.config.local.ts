/**
 * vite.config.local.ts — Standalone dev config for local (non-Replit) environments.
 *
 * Use this when developing outside Replit (Visual Studio, VS Code, etc.):
 *
 *   npm run dev:local
 *   npm run build:local
 *
 * This config does NOT require PORT or BASE_PATH environment variables.
 * It also excludes Replit-specific plugins that are only needed in the
 * Replit hosted environment.
 */
import path from 'path';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';

export default defineConfig({
  base: '/',
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, 'src'),
      '@workspace/api-client-react': path.resolve(__dirname, 'src/api-client/index.ts'),
    },
    dedupe: ['react', 'react-dom'],
  },
  server: {
    port: 3000,
    host: '0.0.0.0',
    open: true,
    // Mirrors production's nginx reverse-proxy: in production, / and /api share
    // one origin (see docker-compose.yml + nginx/nginx.conf.template), so the SPA's
    // relative fetch('/api/...') calls (AuthContext's CSRF-seed/logout calls) and
    // the API_BASE_URL-based client both resolve to the same host. Locally, proxy
    // /api to the .NET API so that topology is preserved without editing app code.
    proxy: {
      '/api': {
        target: process.env.VITE_API_BASE_URL ?? 'https://localhost:5100',
        changeOrigin: true,
        secure: false,
      },
      '/health': {
        target: process.env.VITE_API_BASE_URL ?? 'https://localhost:5100',
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    rollupOptions: {
      output: {
        // Split vendor code into separate chunks for better caching
        manualChunks: {
          'vendor-react':  ['react', 'react-dom'],
          'vendor-query':  ['@tanstack/react-query'],
          'vendor-charts': ['recharts'],
          'vendor-icons':  ['lucide-react'],
          'vendor-ui':     ['@radix-ui/react-dialog', '@radix-ui/react-dropdown-menu',
                            '@radix-ui/react-tabs', '@radix-ui/react-select'],
        },
      },
    },
  },
  preview: {
    port: 4173,
    host: '0.0.0.0',
  },
});
