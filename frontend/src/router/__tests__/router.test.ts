import { describe, it, expect } from 'vitest'
import type { RouteLocation } from 'vue-router'
import router from '../index'

describe('router', () => {
  it('redirects old /browser cookbook share links to the recipe browser, keeping the query (MC-71)', () => {
    // resolve() does not follow redirects, and a real navigation would hit the auth guard,
    // so apply the route's own redirect to the resolved location.
    const resolved = router.resolve('/browser?user=anna&shareToken=tok')
    const redirect = resolved.matched[0]!.redirect as (to: RouteLocation) => unknown

    expect(redirect(resolved)).toEqual({ path: '/', query: { user: 'anna', shareToken: 'tok' } })
  })

  it('has a Shared page', () => {
    expect(router.resolve('/shared').matched).toHaveLength(1)
  })
})
