import { Given, When, Then } from '../fixtures/world'

Given('a recipe named {string} exists', async ({ createRecipe }, name: string) => {
  await createRecipe(name)
})

Then('I see the empty recipes message', async ({ page }) => {
  await page.getByText('No recipes found').waitFor()
})

Then('I see {string} in the recipe list', async ({ page }, name: string) => {
  await page.locator('.recipe-table').getByText(name).waitFor()
})

Then('I do not see {string} in the recipe list', async ({ page }, name: string) => {
  await page.locator('.recipe-table').getByText(name).waitFor({ state: 'hidden' })
})

// createRecipe uses window.prompt() — register the dialog handler before clicking
When('I create a recipe named {string}', async ({ page }, name: string) => {
  page.once('dialog', (dialog) => dialog.accept(name))
  await page.getByRole('button', { name: 'New Recipe' }).click()
  await page.waitForURL(/\/recipe\/[0-9a-f-]{36}$/)
})

When('I click on {string} in the recipe list', async ({ page }, name: string) => {
  await page.locator('.recipe-table').getByRole('row', { name }).click()
})

When('I search for {string}', async ({ page }, text: string) => {
  await page.getByPlaceholder('Search...').fill(text)
  await page.waitForTimeout(300) // 200ms debounce + buffer
})

When('I clear the search', async ({ page }) => {
  // Vuetify's clearable v-text-field renders a button inside .v-field__clearable
  // If this selector breaks, run `npm run codegen` to find the live selector
  await page.locator('.v-field__clearable button').click()
  await page.waitForTimeout(300) // wait for debounce to settle
})

// deleteRecipe uses window.confirm()
When('I delete the current recipe', async ({ page }) => {
  page.once('dialog', (dialog) => dialog.accept())
  await page.getByRole('button', { name: 'Delete' }).click()
})
