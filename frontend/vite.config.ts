import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vuetify from 'vite-plugin-vuetify'

export default defineConfig({
  plugins: [vue(), vuetify({ autoImport: true })],
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:5000',
      '/Account': 'http://localhost:5000',
    },
  },
  build: {
    outDir: '../MyCookbook/wwwroot',
    emptyOutDir: true,
  },
})
