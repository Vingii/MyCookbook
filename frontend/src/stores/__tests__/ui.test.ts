import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

// useUiStore reads localStorage at ref() initialization time, so tests that verify
// initial-state-from-storage must reset the module and re-import the store.

describe('useUiStore', () => {
  beforeEach(() => {
    vi.resetModules()
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('defaults theme to light', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    expect(store.theme).toBe('light')
  })

  it('toggleTheme switches light to dark', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.toggleTheme()
    expect(store.theme).toBe('dark')
  })

  it('toggleTheme switches dark back to light', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.toggleTheme()
    store.toggleTheme()
    expect(store.theme).toBe('light')
  })

  it('toggleTheme persists the new theme to localStorage', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.toggleTheme()
    expect(localStorage.getItem('theme')).toBe('dark')
  })

  it('loads initial theme from localStorage', async () => {
    localStorage.setItem('theme', 'dark')
    const { useUiStore } = await import('../ui')
    setActivePinia(createPinia())
    const store = useUiStore()
    expect(store.theme).toBe('dark')
  })

  it('setLocale changes the locale', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.setLocale('cs')
    expect(store.locale).toBe('cs')
  })

  it('setLocale persists the locale to localStorage', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.setLocale('cs')
    expect(localStorage.getItem('locale')).toBe('cs')
  })

  it('t computed switches translations when locale changes', async () => {
    const { useUiStore } = await import('../ui')
    const store = useUiStore()
    store.setLocale('en')
    const enRecipes = store.t.recipes
    store.setLocale('cs')
    const csRecipes = store.t.recipes
    // The 'recipes' key should differ between English and Czech
    expect(enRecipes).not.toBe(csRecipes)
  })
})
