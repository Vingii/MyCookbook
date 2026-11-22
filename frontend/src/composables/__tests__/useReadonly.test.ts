import { describe, it, expect, beforeEach } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useReadonly } from '../useReadonly'
import { useAuthStore } from '../../stores/auth'

describe('useReadonly', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useAuthStore().username = 'devuser'
  })

  it('your own recipes are editable', () => {
    expect(useReadonly().readonly.value).toBe(false)
  })

  it('guests are read-only', () => {
    useAuthStore().isGuest = true
    expect(useReadonly().readonly.value).toBe(true)
  })

  // MC-78: signed in with an API token (Home Assistant dashboard link).
  it('API token sessions are read-only', () => {
    useAuthStore().isReadOnly = true
    expect(useReadonly().readonly.value).toBe(true)
  })
})
