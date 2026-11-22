import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import RecipeViewerView from '../RecipeViewerView.vue'
import StepList from '../../components/StepList.vue'
import { recipesApi } from '../../api/recipes'
import { cookbooksApi } from '../../api/cookbooks'
import { useAuthStore } from '../../stores/auth'
import type { RecipeDto } from '../../api/types'

vi.mock('../../api/recipes', () => ({
  recipesApi: { getById: vi.fn(), clone: vi.fn() },
}))
vi.mock('../../api/cookbooks', () => ({
  cookbooksApi: { getAll: vi.fn() },
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
async function mountView(path: string, user: { username: string; isGuest?: boolean; isReadOnly?: boolean }) {
  const auth = useAuthStore()
  auth.username = user.username
  auth.isAuthenticated = true
  auth.isGuest = user.isGuest ?? false
  auth.isReadOnly = user.isReadOnly ?? false
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

// Only your own recipes open here; recipes others share open on /recipe/shared/{guid} (SharedRecipeView).
describe('RecipeViewerView', () => {
  const twoCookbooks = [
    { id: 1, name: '', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: true, recipeCount: 3 },
    { id: 2, name: 'Baking', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: false, recipeCount: 1 },
  ]

  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    getById.mockReset().mockResolvedValue(gyros)
    clone.mockReset()
    vi.mocked(cookbooksApi.getAll).mockReset().mockResolvedValue([twoCookbooks[0]!])
  })

  it('offers owner actions on your own recipe', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(buttonTexts(wrapper)).toEqual(expect.arrayContaining(['Share', 'Clone', 'Delete']))
  })

  it('opens the copy after cloning, which stays in the same cookbook', async () => {
    clone.mockResolvedValue({ ...gyros, guid: 'new-guid' })
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    await wrapper.findAll('button').find((b) => b.text() === 'Clone')!.trigger('click')
    await flushPromises()

    expect(clone).toHaveBeenCalledWith(GUID)
    // The view does not await the push.
    await vi.waitFor(() => expect(wrapper.router.currentRoute.value.fullPath).toBe('/recipe/new-guid'))
  })

  // MC-78: e.g. the Home Assistant panel.
  it('offers no actions in a read-only API token session', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel', isReadOnly: true })

    expect(buttonTexts(wrapper)).not.toContain('Clone')
    expect(buttonTexts(wrapper)).not.toContain('Share')
    expect(buttonTexts(wrapper)).not.toContain('Delete')
    expect(cookbooksApi.getAll).not.toHaveBeenCalled()
  })

  it('offers no clone to guests, who have no cookbook', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'guest-1', isGuest: true })

    expect(buttonTexts(wrapper)).not.toContain('Clone')
  })

  it('shows the cookbook of your recipe when you have several (MC-80)', async () => {
    vi.mocked(cookbooksApi.getAll).mockResolvedValue(twoCookbooks)
    getById.mockResolvedValue({ ...gyros, cookbookId: 2 })
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(wrapper.find('[data-test="recipe-cookbook-name"]').text()).toContain('Baking')
  })

  it('hides the cookbook with just one (MC-80)', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(wrapper.find('[data-test="recipe-cookbook-name"]').exists()).toBe(false)
  })

  // MC-81: what the recipe's cookbook shows.
  it('shows everything when its cookbook does', async () => {
    getById.mockResolvedValue({ ...gyros, durationText: '0:30', servings: 4, display: { duration: true, lastCooked: true, servings: true, stepTimeline: true } })
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(wrapper.find('[data-test="recipe-duration"]').text()).toContain('0:30')
    expect(wrapper.find('[data-test="recipe-servings"]').text()).toContain('4')
    expect(wrapper.find('[data-test="finish-cooking"]').exists()).toBe(true)
    expect(wrapper.findComponent(StepList).props('timeline')).toBe(true)
  })

  it('hides what its cookbook turns off, also from the edit fields', async () => {
    getById.mockResolvedValue({ ...gyros, durationText: '0:30', servings: 4, display: { duration: false, lastCooked: false, servings: false, stepTimeline: false } })
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(wrapper.find('[data-test="recipe-duration"]').exists()).toBe(false)
    expect(wrapper.find('[data-test="recipe-servings"]').exists()).toBe(false)
    // Finishing only records when it was last cooked.
    expect(wrapper.find('[data-test="finish-cooking"]').exists()).toBe(false)
    expect(wrapper.findComponent(StepList).props('timeline')).toBe(false)

    await wrapper.find('button .mdi-pencil').element.closest('button')!.dispatchEvent(new Event('click'))
    await flushPromises()
    const labels = wrapper.findAll('label').map((l) => l.text())
    expect(labels).toContain('Category')
    expect(labels).not.toContain('Duration (min)')
    expect(labels).not.toContain('Servings')
  })

  it('shows everything for a recipe without display settings', async () => {
    const wrapper = await mountView(`/recipe/${GUID}`, { username: 'adel' })

    expect(wrapper.find('[data-test="recipe-duration"]').exists()).toBe(true)
    expect(wrapper.find('[data-test="finish-cooking"]').exists()).toBe(true)
  })
})
