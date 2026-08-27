import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { defineConfig } from 'vite'

const urlApi = process.env.VITE_API_PROXY ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // El proxy deja al cliente en el mismo origen que la API durante el desarrollo,
    // igual que en un despliegue detrás de un reverse proxy.
    proxy: {
      '/api': { target: urlApi, changeOrigin: true },
      '/health': { target: urlApi, changeOrigin: true },
    },
  },
})
