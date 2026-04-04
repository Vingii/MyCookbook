import { Given, Then } from '../fixtures/world'

// Matches both "Given I am on..." and "Then I am on..." — playwright-bdd shares step
// patterns across Given/When/Then keywords, so only one definition is needed.
Given('I am on the recipe browser page', async ({ page }) => {
  await page.goto('/')
  await page.locator('.recipe-table').waitFor()
  // Dismiss any auto-opening overlay (e.g. changelog dialog) that would block clicks
  const scrim = page.locator('.v-overlay__scrim')
  if (await scrim.isVisible()) {
    await page.keyboard.press('Escape')
    await scrim.waitFor({ state: 'hidden' })
  }
})

Then('I am on the detail page for {string}', async ({ page }, name: string) => {
  await page.waitForURL(/\/recipe\/[0-9a-f-]{36}$/)
  await page.getByRole('heading', { level: 1, name }).waitFor()
})
