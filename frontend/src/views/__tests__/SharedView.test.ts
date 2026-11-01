import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import SharedView from '../SharedView.vue'
import { sharedApi, type SharedItemDto } from '../../api/shared'

vi.mock('../../api/shared', () => ({
  sharedApi: { getAll: vi.fn(), setPinned: vi.fn(), remove: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const getAll = vi.mocked(sharedApi.getAll)

function item(overrides: Partial<SharedItemDto>): SharedItemDto {
  return {
    id: 1, ownerName: 'Anna', recipeGuid: 'g1', recipeName: 'Gyros',
    lastOpened: '2026-10-30T10:00:00Z', pinned: false, available: true, ...overrides,
  }
}

const gyros = item({ id: 1, recipeGuid: 'g1', recipeName: 'Gyros', lastOpened: '2026-10-31T10:00:00Z' })
const kure = item({ id: 2, recipeGuid: 'g2', recipeName: 'Kuře na paprice', ownerName: 'Petr', lastOpened: '2026-10-30T10:00:00Z' })
const hummus = item({ id: 3, recipeGuid: 'g3', recipeName: 'Hummus', lastOpened: '2026-10-29T10:00:00Z' })

async function mountView() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/:pathMatch(.*)*', component: SharedView }],
  })
  router.push('/shared')
  await router.isReady()
  const wrapper = mount(SharedView, { global: { plugins: [vuetify, router] } })
  await flushPromises()
  return wrapper
}

const titles = (wrapper: Awaited<ReturnType<typeof mountView>>) =>
  wrapper.findAll('.v-list-item-title').map((t) => t.text())

enableAutoUnmount(afterEach)

describe('SharedView (MC-71)', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    vi.mocked(sharedApi.setPinned).mockReset().mockResolvedValue({} as never)
    vi.mocked(sharedApi.remove).mockReset().mockResolvedValue({} as never)
  })

  it('explains the page when nothing has been shared yet', async () => {
    getAll.mockResolvedValue([])
    const wrapper = await mountView()

    expect(wrapper.text()).toContain('Recipes others share with you will appear here once you open them.')
  })

  it('lists shared recipes with their owner and links to open them', async () => {
    getAll.mockResolvedValue([gyros, kure])
    const wrapper = await mountView()

    expect(titles(wrapper)).toEqual(['Gyros', 'Kuře na paprice'])
    expect(wrapper.text()).toContain('From Anna')
    expect(wrapper.text()).toContain('From Petr')
    const links = wrapper.findAll('a.v-list-item').map((a) => a.attributes('href'))
    expect(links).toEqual(['/recipe/shared/g1', '/recipe/shared/g2'])
  })

  it('shows deleted recipes as unavailable, without a link', async () => {
    getAll.mockResolvedValue([item({ id: 5, recipeName: null, available: false }), gyros])
    const wrapper = await mountView()

    expect(titles(wrapper)).toEqual(['No longer available', 'Gyros'])
    expect(wrapper.findAll('a.v-list-item').map((a) => a.attributes('href'))).toEqual(['/recipe/shared/g1'])
  })

  it('separates pinned items from recently opened ones', async () => {
    getAll.mockResolvedValue([{ ...kure, pinned: true }, gyros, hummus])
    const wrapper = await mountView()

    const headings = wrapper.findAll('h2').map((h) => h.text())
    expect(headings).toEqual(['Pinned', 'Recently opened'])
    expect(titles(wrapper)).toEqual(['Kuře na paprice', 'Gyros', 'Hummus'])
  })

  it('searches by recipe and owner name, ignoring case and diacritics', async () => {
    getAll.mockResolvedValue([gyros, kure, hummus])
    const wrapper = await mountView()
    const input = wrapper.find('input')

    await input.setValue('KURE')
    expect(titles(wrapper)).toEqual(['Kuře na paprice'])

    await input.setValue('petr')
    expect(titles(wrapper)).toEqual(['Kuře na paprice'])

    await input.setValue('nothing like this')
    expect(titles(wrapper)).toEqual([])
    expect(wrapper.text()).toContain('Nothing matches your search.')
  })

  it('pins an item and moves it to the top', async () => {
    getAll.mockResolvedValue([gyros, kure])
    const wrapper = await mountView()

    await wrapper.findAll('[aria-label="Pin"]')[1]!.trigger('click')
    await flushPromises()

    expect(sharedApi.setPinned).toHaveBeenCalledWith(2, true)
    expect(wrapper.findAll('h2').map((h) => h.text())).toEqual(['Pinned', 'Recently opened'])
    expect(titles(wrapper)).toEqual(['Kuře na paprice', 'Gyros'])
    expect(wrapper.findAll('[aria-label="Unpin"]')).toHaveLength(1)
  })

  it('removes an item from the list', async () => {
    getAll.mockResolvedValue([gyros, kure])
    const wrapper = await mountView()

    await wrapper.findAll('[aria-label="Remove from list"]')[0]!.trigger('click')
    await flushPromises()

    expect(sharedApi.remove).toHaveBeenCalledWith(1)
    expect(titles(wrapper)).toEqual(['Kuře na paprice'])
  })
})
