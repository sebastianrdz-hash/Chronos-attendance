import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import fs from 'node:fs'
import path from 'node:path'
import { defineConfig } from 'vite'

const urlApi = process.env.VITE_API_PROXY ?? 'http://localhost:5080'

// Los emite scripts/certificados-dev.ps1. Si no existen, el servidor sigue arrancando
// en HTTP plano: un clon recién bajado no debe romperse por no haberlos generado.
const carpetaCertificados = path.resolve(import.meta.dirname, '../certificados')
const rutaLlave = path.join(carpetaCertificados, 'chronos-dev-key.pem')
const rutaCertificado = path.join(carpetaCertificados, 'chronos-dev.pem')
const hayCertificados = fs.existsSync(rutaLlave) && fs.existsSync(rutaCertificado)

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // Escuchar en todas las interfaces permite abrir la aplicación desde un celular
    // de la misma red, que es la única forma de probar de verdad la cámara y WebAuthn.
    host: true,
    // La cámara y WebAuthn exigen contexto seguro. El navegador se lo concede a
    // localhost aunque vaya en HTTP, pero no a una IP de red local: ahí hace falta
    // HTTPS de verdad, con un certificado en el que el dispositivo confíe.
    https: hayCertificados
      ? { key: fs.readFileSync(rutaLlave), cert: fs.readFileSync(rutaCertificado) }
      : undefined,
    // El proxy deja al cliente en el mismo origen que la API durante el desarrollo,
    // igual que en un despliegue detrás de un reverse proxy. Como el navegador solo
    // habla con Vite, el origen que ve —y que WebAuthn usará como RP ID— es este,
    // no el puerto de la API.
    proxy: {
      '/api': { target: urlApi, changeOrigin: true },
      '/health': { target: urlApi, changeOrigin: true },
    },
  },
})
