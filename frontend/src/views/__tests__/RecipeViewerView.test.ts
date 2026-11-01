import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import RecipeViewerView from '../RecipeViewerView.vue'
import { recipesApi } from '../../api/recipes'
import { useAuthStore } from '../../stores/auth'
import type { RecipeDto } from '../../api/types'

vi.mock('../../api/recipes', () => ({
  recipesApi: { getById: vi.fn(), clone: vi.fn() },
}))
vi.mock('../../api/tags', () => ({
  tagsApi: { getAll: vi.fn().mockResolvedValue([]) },
  categoriesApi: { getAll: vi.fn().mockResolvedValue([]) },
}))
vi.mock('../../composables/useIngredientHighlighter', () => ({
  getHighlightWords: vi.fn().mockResolvedValue(new Set()),
}))

const vuetify = createVuetify({ components, directives })
const getById = vi.mocked(recipesApi.getById)
const clone = vi.mocked(recipesApi.clone)
const GUID = '4aa97588-e66d-4c9e-8fe8-9ed70ea6ef7e'

const gyros = {
  guid: GUID, name: 'Gyros', tags: [], ingredients: [], steps: [], links: [], usedIn: [],
} as unknown as RecipeDto

/** Mounts the viewer at <paramref name="path"/> with the given signed-in user. */
async function mountView(path: string, user: { username: string; isGuest?: boolean }) {
  const auth = useAuthStore()
  auth.username = user.username
  auth.isAuthenticated = true
  auth.isGuest = user.isGuest ?? false
  auth.loaded = true

  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/recipe/:guid', component: RecipeViewerView }],
  })
  router.push(path)
  await router.isReady()
  const wrapper = mount(RecipeViewerView, {
    global: { plugins: [vuetify, router], stubs: { IngredientList: true, StepList: true } },
  })
  await flushPromises()
  return Object.assign(wrapper, { router })
}

const buttonTexts = (wrapper: Awaited<ReturnType<typeof mountView>>) =>
  wrapper.findAll('button').map((b) => b.text()).filter(Boolean)

enableAutoUnmount(afterEach)

describe('RecipeViewerView clone (MC-70)', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    getById.mockReset().mockResolvedValue(gyros)
    clone.mockReset()
  })

  it('offers owner actions, not "clone to my recipes", on your own recipe', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(buttonTexts(wrapper)).toEqual(expect.arrayContaining(['Share', 'Clone', 'Delete']))
    expect(buttonTexts(wrapper)).not.toContain('Clone to my recipes')
  })

  it('offers only "clone to my recipes" on a recipe someone shared with you', async () => {
    const wrapper = await mountView(`/recipe/${GUID}?user=michal&shareToken=abc`, { username: 'adel' })

    expect(buttonTexts(wrapper)).toContain('Clone to my recipes')
    expect(buttonTexts(wrapper)).not.toContain('Delete')
  })

  it('opens your own copy after cloning a shared recipe', async () => {
    clone.mockResolvedValue({ ...gyros, guid: 'new-guid' })
    const wrapper = await mountView(`/recipe/${GUID}?user=michal&shareToken=abc`, { username: 'adel' })

    await wrapper.findAll('button').find((b) => b.text() === 'Clone to my recipes')!.trigger('click')
    await flushPromises()

    expect(clone).toHaveBeenCalledWith(GUID)
    // No ?user= or ?shareToken=: the copy is in your own cookbook. The view does not await the push.
    await vi.waitFor(() => expect(wrapper.router.currentRoute.value.fullPath).toBe('/recipe/new-guid'))
  })

  it('offers no clone to guests, who have no cookbook', async () => {
    const wrapper = await mountView(`/recipe/${GUID}?user=michal&shareToken=abc`, { username: 'guest-1', isGuest: true })

    expect(buttonTexts(wrapper)).not.toContain('Clone to my recipes')
  })
})
