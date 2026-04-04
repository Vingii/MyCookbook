import { Given, Then } from '../fixtures/world'

// Matches both "Given I am on..." and "Then I am on..." — playwright-bdd shares step
// patterns across Given/When/Then keywords, so only one definition is needed.
Given('I am on the recipe browser page', async ({ page }) => {
  await page.goto('/')
  await page.locator('.recipe-table').waitFor()
})

Then('I am on the detail page for {string}', async ({ page }, name: string) => {
  await page.waitForURL(/\/recipe\/[0-9a-f-]{36}$/)
  await page.getByRole('heading', { level: 1, name }).waitFor()
})
