import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import SharedRecipeView from '../SharedRecipeView.vue'
import { recipesApi } from '../../api/recipes'
import type { RecipeDto } from '../../api/types'

vi.mock('../../api/recipes', () => ({
  recipesApi: { getShared: vi.fn() },
}))
vi.mock('../../composables/useIngredientHighlighter', () => ({
  getHighlightWords: vi.fn().mockResolvedValue(new Set()),
}))

const vuetify = createVuetify({ components, directives })
const getShared = vi.mocked(recipesApi.getShared)
const GUID = '4aa97588-e66d-4c9e-8fe8-9ed70ea6ef7e'

function recipe(name: string): RecipeDto {
  return { guid: GUID, name, tags: [], ingredients: [], steps: [], links: [] } as unknown as RecipeDto
}

async function mountView() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/recipe/shared/:guid', component: SharedRecipeView }],
  })
  router.push(`/recipe/shared/${GUID}`)
  await router.isReady()
  const wrapper = mount(SharedRecipeView, {
    global: { plugins: [vuetify, router], stubs: { IngredientList: true, StepList: true } },
  })
  await flushPromises()
  return wrapper
}

/** Fires visibilitychange with the given state (happy-dom does not report "visible" by default). */
function setVisibility(state: DocumentVisibilityState) {
  Object.defineProperty(document, 'visibilityState', { value: state, configurable: true })
  document.dispatchEvent(new Event('visibilitychange'))
}

/** A pageshow event as fired when the browser restores the page from its back/forward cache. */
function pageShow(persisted: boolean) {
  window.dispatchEvent(Object.assign(new Event('pageshow'), { persisted }))
}

// The view registers window/document listeners, so every test's component must be unmounted.
enableAutoUnmount(afterEach)

describe('SharedRecipeView', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    getShared.mockReset()
  })

  it('reloads the recipe when the browser restores the page from cache', async () => {
    getShared.mockResolvedValueOnce(recipe('Gyros')).mockResolvedValueOnce(recipe('Gyros v2'))
    const wrapper = await mountView()
    expect(wrapper.find('h1').text()).toBe('Gyros')

    pageShow(true)
    await flushPromises()

    expect(getShared).toHaveBeenCalledTimes(2)
    expect(wrapper.find('h1').text()).toBe('Gyros v2')
  })

  it('does not reload on a normal page load', async () => {
    getShared.mockResolvedValue(recipe('Gyros'))
    await mountView()

    pageShow(false)
    await flushPromises()

    expect(getShared).toHaveBeenCalledTimes(1)
  })

  it('reloads the recipe when the tab becomes visible again', async () => {
    getShared.mockResolvedValueOnce(recipe('Gyros')).mockResolvedValueOnce(recipe('Gyros v2'))
    const wrapper = await mountView()

    setVisibility('hidden')
    await flushPromises()
    expect(getShared).toHaveBeenCalledTimes(1)

    setVisibility('visible')
    await flushPromises()

    expect(wrapper.find('h1').text()).toBe('Gyros v2')
  })

  it('keeps showing the recipe if a refresh fails', async () => {
    getShared.mockResolvedValueOnce(recipe('Gyros')).mockRejectedValueOnce(new Error('offline'))
    const wrapper = await mountView()

    pageShow(true)
    await flushPromises()

    expect(wrapper.find('h1').text()).toBe('Gyros')
  })

  it('stops listening after leaving the page', async () => {
    getShared.mockResolvedValue(recipe('Gyros'))
    const wrapper = await mountView()
    wrapper.unmount()

    pageShow(true)
    setVisibility('visible')
    await flushPromises()

    expect(getShared).toHaveBeenCalledTimes(1)
  })
})
