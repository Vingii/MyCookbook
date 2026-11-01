import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import RecipeBrowserView from '../RecipeBrowserView.vue'
import { sharedApi } from '../../api/shared'
import { useAuthStore } from '../../stores/auth'

const xs = ref(false)
vi.mock('vuetify', async (importOriginal) => ({
  ...(await importOriginal<typeof import('vuetify')>()),
  useDisplay: () => ({ xs }),
}))

vi.mock('../../api/recipes', () => ({
  recipesApi: {
    getAll: vi.fn().mockResolvedValue([]),
    getAllIngredientNames: vi.fn().mockResolvedValue([]),
  },
}))

vi.mock('../../api/shared', () => ({
  sharedApi: { recordCookbook: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })

async function mountView(path = '/') {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', component: RecipeBrowserView }],
  })
  router.push(path)
  await router.isReady()
  const wrapper = mount(RecipeBrowserView, {
    global: { plugins: [vuetify, router], stubs: { RecipeTable: true } },
  })
  await flushPromises()
  return wrapper
}

const placeholders = (wrapper: Awaited<ReturnType<typeof mountView>>) =>
  wrapper.findAll('input').map((i) => i.attributes('placeholder'))

describe('RecipeBrowserView filters', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('shows all filters and no toggle on wider screens', async () => {
    xs.value = false
    const wrapper = await mountView()

    expect(placeholders(wrapper)).toEqual(['Search...', 'Category...', 'Tag...', 'Ingredient...'])
    expect(wrapper.find('[aria-label="Filters"]').exists()).toBe(false)
  })

  it('shows only search on phones until the filter toggle is pressed', async () => {
    xs.value = true
    const wrapper = await mountView()

    expect(placeholders(wrapper)).toEqual(['Search...'])
    const toggle = wrapper.find('[aria-label="Filters"]')
    expect(toggle.attributes('aria-expanded')).toBe('false')

    await toggle.trigger('click')

    expect(placeholders(wrapper)).toEqual(['Search...', 'Category...', 'Tag...', 'Ingredient...'])
    expect(toggle.attributes('aria-expanded')).toBe('true')
  })

  it('badges the toggle with the number of active filters', async () => {
    xs.value = true
    const wrapper = await mountView()
    const badge = () => wrapper.find('[aria-label="Filters"] .v-badge__badge')
    // VBadge hides itself with an inline display: none rather than unmounting.
    const badgeShown = () => !badge().attributes('style')?.includes('display: none')

    expect(badgeShown()).toBe(false)

    await wrapper.find('[aria-label="Filters"]').trigger('click')
    const [category, tag] = wrapper.findAllComponents({ name: 'VAutocomplete' })
    category!.vm.$emit('update:modelValue', 'Soups')
    tag!.vm.$emit('update:modelValue', 'quick')
    await flushPromises()
    // Collapsing keeps the filters applied, so the badge must stay visible.
    await wrapper.find('[aria-label="Filters"]').trigger('click')

    expect(badgeShown()).toBe(true)
    expect(badge().text()).toBe('2')
  })
})

describe('RecipeBrowserView shared cookbooks (MC-71)', () => {
  function signIn(username: string, isGuest = false) {
    const auth = useAuthStore()
    auth.username = username
    auth.isAuthenticated = true
    auth.isGuest = isGuest
  }

  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    vi.mocked(sharedApi.recordCookbook).mockReset().mockResolvedValue({} as never)
  })

  it('records a cookbook opened through its share link', async () => {
    signIn('adel')
    await mountView('/?user=anna&shareToken=tok')

    expect(sharedApi.recordCookbook).toHaveBeenCalledWith('anna', 'tok')
  })

  it('does not record your own cookbook', async () => {
    signIn('adel')
    await mountView('/')

    expect(sharedApi.recordCookbook).not.toHaveBeenCalled()
  })

  it('does not record for guests', async () => {
    signIn('guest-1', true)
    await mountView('/?user=anna&shareToken=tok')

    expect(sharedApi.recordCookbook).not.toHaveBeenCalled()
  })
})
