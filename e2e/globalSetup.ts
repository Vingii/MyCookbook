import { request } from '@playwright/test'

export default async function globalSetup() {
  const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
  const ctx = await request.newContext({ baseURL })
  try {
    const res = await ctx.get('/api/auth/me')
    if (!res.ok()) {
      throw new Error(
        `App health check failed (${res.status()}). Is the app running?\nRun: podman compose up --build -d`
      )
    }
    // Delete all recipes left over from previous test runs so each suite starts clean
    const listRes = await ctx.get('/api/recipes')
    const recipes: { guid: string }[] = await listRes.json()
    for (const r of recipes) {
      await ctx.delete(`/api/recipes/${r.guid}`)
    }
  } finally {
    await ctx.dispose()
  }
}
