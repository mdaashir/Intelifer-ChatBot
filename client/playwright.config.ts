import { defineConfig, devices } from '@playwright/test';

const apiUrl = 'http://localhost:5080';
const clientUrl = 'http://localhost:4200';

export default defineConfig({
  testDir: './e2e',
  // The tests share a database and create accounts, so they run one at a time.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 1 : 0,
  reporter: [['list']],
  use: {
    baseURL: clientUrl,
    trace: 'on-first-retry',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],

  // Both halves of the stack are started here. The API needs SQL Server on :1433,
  // which is what 'docker compose up -d sqlserver' provides.
  webServer: [
    {
      command: 'dotnet run --project ../server/src/Intellifer.Chatbot.Web',
      url: `${apiUrl}/openapi/v1.json`,
      reuseExistingServer: !process.env['CI'],
      timeout: 240_000,
      stdout: 'ignore',
    },
    {
      command: 'npm start',
      url: clientUrl,
      reuseExistingServer: !process.env['CI'],
      timeout: 240_000,
    },
  ],
});
