import { defineConfig, devices } from '@playwright/test';
import * as path from 'path';

const KMILA_WEB_DIR = path.resolve(__dirname, '../../App/Kmila/Kmila.Web');
const PORT = Number(process.env.KMILA_WEB_PORT ?? 5297);
const baseURL = `http://localhost:${PORT}`;

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: [['list'], ['html', { open: 'never' }]],

  use: {
    baseURL,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    ignoreHTTPSErrors: true,
  },

  // Auto-start Kmila.Web. dotnet's `--framework net9.0` is required even
  // though the csproj lists a single TargetFramework — without it dotnet
  // errors with "multiple target frameworks." Cold build can take ~60-90s,
  // so the timeout is generous.
  webServer: {
    command: `dotnet run --project "${KMILA_WEB_DIR}" --framework net9.0 --launch-profile http`,
    url: baseURL,
    reuseExistingServer: !process.env.CI,
    timeout: 180_000,
    stdout: 'pipe',
    stderr: 'pipe',
  },

  projects: [
    {
      name: 'chromium-desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    {
      name: 'chromium-mobile',
      use: { ...devices['Pixel 7'] },
      testMatch: /.*\.mobile\.spec\.ts/,
    },
  ],
});
