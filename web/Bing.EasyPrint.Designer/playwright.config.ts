import { defineConfig } from '@playwright/test'

export default defineConfig({
  testDir: './e2e',
  fullyParallel: false,
  reporter: 'list',
  use: {
    baseURL: `http://127.0.0.1:${process.env.EASYPRINT_DESIGNER_WEB_PORT ?? '5173'}`,
    browserName: 'chromium',
    viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure',
  },
  webServer: [
    {
      command: 'dotnet run --no-launch-profile --project ../../tools/DesignerHost/DesignerHost.csproj',
      url: `${process.env.EASYPRINT_DESIGNER_API_URL ?? 'http://127.0.0.1:5187'}/api/designer/health`,
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
    },
    {
      command: `npm run dev -- --port ${process.env.EASYPRINT_DESIGNER_WEB_PORT ?? '5173'} --strictPort`,
      url: `http://127.0.0.1:${process.env.EASYPRINT_DESIGNER_WEB_PORT ?? '5173'}`,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
})
