import { test as base } from 'playwright-bdd'
import { request } from '@playwright/test'

const BASE_URL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'

export const test = base.extend<{
  createdGuids: string[]
  createRecipe: (name: string, category?: string) => Promise<string>
}>({
  createdGuids: async ({}, use) => {
    const guids: string[] = []
    await use(guids)
    // cleanup after each scenario
    const ctx = await request.newContext({ baseURL: BASE_URL })
    for (const guid of guids) {
      await ctx.delete(`/api/recipes/${guid}`)
    }
    await ctx.dispose()
  },

  createRecipe: async ({ createdGuids }, use) => {
    const ctx = await request.newContext({ baseURL: BASE_URL })
    await use(async (name: string, category?: string) => {
      const res = await ctx.post('/api/recipes', {
        data: { name, servings: 2, ...(category ? { category } : {}) },
      })
      const recipe = await res.json()
      createdGuids.push(recipe.guid)
      return recipe.guid as string
    })
    await ctx.dispose()
  },
})
