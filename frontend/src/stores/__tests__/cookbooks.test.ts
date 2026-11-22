import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useCookbooksStore } from '../cookbooks'
import { useUiStore } from '../ui'
import { cookbooksApi } from '../../api/cookbooks'
import type { CookbookDto } from '../../api/types'

vi.mock('../../api/cookbooks', () => ({
  cookbooksApi: { getAll: vi.fn() },
}))

const getAll = vi.mocked(cookbooksApi.getAll)
const home: CookbookDto = { id: 1, name: '', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: true, recipeCount: 3 }
const baking: CookbookDto = { id: 2, name: 'Baking', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: false, recipeCount: 1 }

describe('useCookbooksStore (MC-80)', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    getAll.mockReset().mockResolvedValue([home, baking])
  })

  it('names the unnamed default cookbook in the current language', () => {
    const store = useCookbooksStore()
    expect(store.displayName(home)).toBe('My recipes')
    expect(store.displayName(baking)).toBe('Baking')
    useUiStore().locale = 'cs'
    expect(store.displayName(home)).toBe('Moje recepty')
    expect(store.displayName(null)).toBe('')
  })

  it('labels recipe counts', () => {
    expect(useCookbooksStore().recipeCountLabel(home)).toBe('Recipes: 3')
  })

  it('puts recipes without a cookbook in the default one', async () => {
    const store = useCookbooksStore()
    await store.fetchAll()
    expect(store.ofRecipe({ cookbookId: null })?.id).toBe(1)
    expect(store.ofRecipe({ cookbookId: 2 })?.id).toBe(2)
    expect(store.ofRecipe({ cookbookId: 99 })?.id).toBe(1)
  })

  it('loads only once with ensureLoaded', async () => {
    const store = useCookbooksStore()
    await store.ensureLoaded()
    await store.ensureLoaded()
    expect(getAll).toHaveBeenCalledTimes(1)
  })

  it('targets the active cookbook for new recipes only when there are several', async () => {
    localStorage.setItem('activeCookbook', '2')
    expect(await useCookbooksStore().targetForNewRecipe()).toBe(2)

    setActivePinia(createPinia())
    getAll.mockResolvedValue([home])
    expect(await useCookbooksStore().targetForNewRecipe()).toBeUndefined()
  })

  it('targets the default cookbook when cookbooks cannot be loaded', async () => {
    getAll.mockRejectedValue(new Error('500'))
    expect(await useCookbooksStore().targetForNewRecipe()).toBeUndefined()
  })

  it('still switches when storage is unavailable', async () => {
    const store = useCookbooksStore()
    await store.fetchAll()
    const setItem = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked')
    })
    store.setActive(2)
    expect(store.active?.id).toBe(2)
    setItem.mockRestore()
  })
})
