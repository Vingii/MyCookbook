import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useReadonly } from '../useReadonly'
import { useAuthStore } from '../../stores/auth'

const query: Record<string, string> = {}
vi.mock('vue-router', () => ({ useRoute: () => ({ query }) }))

describe('useReadonly', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    for (const k of Object.keys(query)) delete query[k]
    useAuthStore().username = 'devuser'
  })

  it('own cookbook is editable', () => {
    expect(useReadonly().readonly.value).toBe(false)
  })

  it('someone else\'s cookbook is read-only', () => {
    query.user = 'anna'
    expect(useReadonly().readonly.value).toBe(true)
  })

  it('guests are read-only', () => {
    useAuthStore().isGuest = true
    expect(useReadonly().readonly.value).toBe(true)
  })

  // MC-78: signed in with an API token (Home Assistant dashboard link).
  it('API token sessions are read-only, also in their own cookbook', () => {
    useAuthStore().isReadOnly = true
    expect(useReadonly().readonly.value).toBe(true)
  })
})
