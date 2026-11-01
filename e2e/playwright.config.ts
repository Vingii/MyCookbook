import { defineConfig } from '@playwright/test'
import { defineBddConfig } from 'playwright-bdd'
import { E2E_HEADERS } from './fixtures/user'

const testDir = defineBddConfig({
  features: 'features/**/*.feature',
  steps: ['steps/**/*.ts', 'fixtures/world.ts'],
})

export default defineConfig({
  testDir,
  globalSetup: './globalSetup.ts',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    extraHTTPHeaders: E2E_HEADERS,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  workers: 1, // tests share one PostgreSQL DB as "e2e-user" — parallelism causes interference
  reporter: [['html', { outputFolder: 'playwright-report', open: 'never' }], ['list']],
})
