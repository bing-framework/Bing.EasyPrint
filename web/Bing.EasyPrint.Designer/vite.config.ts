import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'

declare const process: { env: Record<string, string | undefined> }

export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': { target: process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187', changeOrigin: true },
    },
  },
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts'],
  },
})
