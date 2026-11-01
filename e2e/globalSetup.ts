import { request } from '@playwright/test'
import { E2E_HEADERS, E2E_USER } from './fixtures/user'

export default async function globalSetup() {
  const baseURL = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
  const ctx = await request.newContext({ baseURL, extraHTTPHeaders: E2E_HEADERS })
  try {
    const res = await ctx.get('/api/auth/me')
    if (!res.ok()) {
      throw new Error(
        `App health check failed (${res.status()}). Is the app running?\nRun: podman compose up --build -d`
      )
    }
    const me: { username: string } = await res.json()
    if (me.username !== E2E_USER) {
      // Never wipe someone's real cookbook: the server must honour the X-Dev-User header.
      throw new Error(`Expected to run as "${E2E_USER}" but the app signed in as "${me.username}". Refusing to clean up.`)
    }
    // Delete all of the E2E user's recipes left over from previous runs so each suite starts clean
    const listRes = await ctx.get('/api/recipes')
    const recipes: { guid: string }[] = await listRes.json()
    for (const r of recipes) {
      await ctx.delete(`/api/recipes/${r.guid}`)
    }
  } finally {
    await ctx.dispose()
  }
}
