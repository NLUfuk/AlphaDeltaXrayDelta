import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      // API dev proxy — backend HTTPS default (launchSettings https profile). Avoids CORS in dev.
      '/api': {
        target: 'https://localhost:7084',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
