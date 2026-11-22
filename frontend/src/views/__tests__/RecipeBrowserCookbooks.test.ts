import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import RecipeBrowserView from '../RecipeBrowserView.vue'
import { recipesApi } from '../../api/recipes'
import { cookbooksApi } from '../../api/cookbooks'
import { useUiStore } from '../../stores/ui'
import { useAuthStore } from '../../stores/auth'
import type { CookbookDto } from '../../api/types'

// MC-80: the Recipes page shows one cookbook at a time when there are several.
vi.mock('../../api/recipes', () => ({
  recipesApi: {
    getAll: vi.fn(),
    getAllIngredientNames: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
  },
}))
vi.mock('../../api/cookbooks', () => ({
  cookbooksApi: { getAll: vi.fn() },
}))
vi.mock('../../api/recipeImport', () => ({
  recipeImportApi: { isEnabled: vi.fn().mockResolvedValue(false) },
}))

const vuetify = createVuetify({ components, directives })
const getAll = vi.mocked(recipesApi.getAll)
const getCookbooks = vi.mocked(cookbooksApi.getAll)

const home: CookbookDto = { id: 1, name: '', showInPlanner: true, isDefault: true, recipeCount: 3 }
const baking: CookbookDto = { id: 2, name: 'Baking', showInPlanner: false, isDefault: false, recipeCount: 1 }

async function mountView(path = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', component: RecipeBrowserView },
      { path: '/recipe/:guid', component: { template: '<div />' } },
      { path: '/settings', component: { template: '<div />' } },
    ],
  })
  router.push(path)
  await router.isReady()
  const wrapper = mount(RecipeBrowserView, {
    global: { plugins: [vuetify, router], stubs: {
        RecipeTable: true,
        RecipeImportDialog: true,
        // VMenu needs visualViewport, which the test DOM lacks; render the activator and items inline.
        VMenu: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
      },
    },
  })
  await flushPromises()
  return wrapper
}

const lastCookbookParam = () => getAll.mock.lastCall?.[0]?.cookbook

enableAutoUnmount(afterEach)

describe('RecipeBrowserView cookbooks (MC-80)', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    getAll.mockReset().mockResolvedValue([])
    getCookbooks.mockReset()
  })

  it('looks as before with a single cookbook: "Recipes" title and every recipe', async () => {
    getCookbooks.mockResolvedValue([home])
    const wrapper = await mountView()

    expect(wrapper.find('h1').text()).toBe('Recipes')
    expect(wrapper.find('[data-test="cookbook-switcher"]').exists()).toBe(false)
    expect(lastCookbookParam()).toBeUndefined()
  })

  it('shows the default cookbook as "My recipes" and lists only its recipes', async () => {
    getCookbooks.mockResolvedValue([home, baking])
    const wrapper = await mountView()

    expect(wrapper.find('[data-test="cookbook-switcher"]').text()).toBe('My recipes')
    expect(lastCookbookParam()).toBe(1)
  })

  it('remembers the chosen cookbook', async () => {
    localStorage.setItem('activeCookbook', '2')
    getCookbooks.mockResolvedValue([home, baking])
    const wrapper = await mountView()

    expect(wrapper.find('[data-test="cookbook-switcher"]').text()).toBe('Baking')
    expect(lastCookbookParam()).toBe(2)
  })

  it('falls back to the default when the remembered cookbook was deleted', async () => {
    localStorage.setItem('activeCookbook', '99')
    getCookbooks.mockResolvedValue([home, baking])
    await mountView()

    expect(lastCookbookParam()).toBe(1)
    expect(localStorage.getItem('activeCookbook')).toBe('1')
  })

  it('switches cookbooks from the title menu', async () => {
    getCookbooks.mockResolvedValue([home, baking])
    const wrapper = await mountView()

    await wrapper.find('[data-test="cookbook-2"]').trigger('click')
    await flushPromises()

    expect(lastCookbookParam()).toBe(2)
    expect(wrapper.find('[data-test="cookbook-switcher"]').text()).toBe('Baking')
    expect(localStorage.getItem('activeCookbook')).toBe('2')
  })

  it('creates new recipes in the cookbook being shown', async () => {
    localStorage.setItem('activeCookbook', '2')
    getCookbooks.mockResolvedValue([home, baking])
    vi.mocked(recipesApi.create).mockResolvedValue({ guid: 'new-guid' } as never)
    window.prompt = vi.fn().mockReturnValue('Bread')
    const wrapper = await mountView()

    await wrapper.findAll('button').find((b) => b.text().includes('New Recipe'))!.trigger('click')
    await flushPromises()

    expect(recipesApi.create).toHaveBeenCalledWith({ name: 'Bread', servings: 2, cookbookId: 2 })
  })

  it('still lists recipes when cookbooks fail to load', async () => {
    getCookbooks.mockRejectedValue(new Error('500'))
    const wrapper = await mountView()

    expect(wrapper.find('h1').text()).toBe('Recipes')
    expect(getAll).toHaveBeenCalled()
    expect(lastCookbookParam()).toBeUndefined()
  })

  it('does not load your cookbooks when viewing someone else\'s recipes', async () => {
    getCookbooks.mockResolvedValue([home, baking])
    useAuthStore().username = 'adel'
    await mountView('/?user=anna&shareToken=abc')

    expect(getCookbooks).not.toHaveBeenCalled()
    expect(lastCookbookParam()).toBeUndefined()
  })
})
