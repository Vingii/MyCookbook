import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import PlanSuggestions from '../PlanSuggestions.vue'
import { plannerApi } from '../../api/planner'
import type { PlanSuggestionDto } from '../../api/types'
import { useUiStore } from '../../stores/ui'

// MC-76: planner suggestions panel.
vi.mock('../../api/planner', () => ({
  plannerApi: { getSuggestions: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const getSuggestions = vi.mocked(plannerApi.getSuggestions)

const days = [
  { date: '2026-11-14', label: 'Sat', planned: false },
  { date: '2026-11-15', label: 'Sun', planned: true },
  { date: '2026-11-16', label: 'Mon', planned: true },
  { date: '2026-11-17', label: 'Tue', planned: false },
  { date: '2026-11-18', label: 'Wed', planned: false },
]

const suggestions: PlanSuggestionDto[] = [
  { recipeGuid: 'g1', recipeName: 'Gulášovka', category: 'Polévky', score: 0.9 },
  { recipeGuid: 'g2', recipeName: 'Gyros', category: '', score: 0.5 },
]

function mountPanel(props: Partial<{ days: typeof days; plansKey: string; today: string }> = {}) {
  return mount(PlanSuggestions, {
    props: { days, plansKey: 'a', today: '2026-11-15', ...props },
    global: { plugins: [vuetify], stubs: { RouterLink: { template: '<a><slot /></a>' } } },
  })
}


enableAutoUnmount(afterEach)

describe('PlanSuggestions', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    try { localStorage.clear() } catch { /* ignore */ }
    getSuggestions.mockReset()
    getSuggestions.mockResolvedValue(suggestions)
  })

  it('suggests for the first empty day from today on', async () => {
    mountPanel()
    await flushPromises()

    expect(getSuggestions).toHaveBeenCalledTimes(1)
    expect(getSuggestions).toHaveBeenCalledWith('2026-11-17')
  })

  it('lists suggestions with their category and no explanations', async () => {
    const w = mountPanel()
    await flushPromises()

    const cards = w.findAll('[data-test="suggestion"]').map((s) => s.text())
    expect(cards).toEqual([expect.stringContaining('Gulášovka'), expect.stringContaining('Gyros')])
    expect(cards[0]).toContain('Polévky')
    expect(w.text()).not.toContain('Not cooked')
  })

  it('adds a suggestion to the selected day', async () => {
    const w = mountPanel()
    await flushPromises()

    await w.findAll('[data-test="suggestion-add"]')[1]!.trigger('click')

    expect(w.emitted('add')).toEqual([[{ recipeGuid: 'g2', date: '2026-11-17' }]])
  })

  it('emits drag events so a suggestion can be dropped on a day', async () => {
    const w = mountPanel()
    await flushPromises()

    const card = w.findAll('[data-test="suggestion"]')[0]!
    await card.trigger('dragstart')
    await card.trigger('dragend')

    expect(w.emitted('dragStart')![0]).toEqual([suggestions[0]])
    expect(w.emitted('dragEnd')).toHaveLength(1)
  })

  it('moves on to the next empty day when the plans change', async () => {
    const w = mountPanel()
    await flushPromises()

    await w.setProps({ plansKey: 'b', days: days.map((d) => (d.date === '2026-11-17' ? { ...d, planned: true } : d)) })
    await flushPromises()

    expect(getSuggestions).toHaveBeenLastCalledWith('2026-11-18')
  })

  it('keeps a day the user picked when the plans change', async () => {
    const w = mountPanel()
    await flushPromises()

    w.findComponent({ name: 'VSelect' }).vm.$emit('update:modelValue', '2026-11-14')
    await flushPromises()
    expect(getSuggestions).toHaveBeenLastCalledWith('2026-11-14')

    await w.setProps({ plansKey: 'b', days: days.map((d) => (d.date === '2026-11-17' ? { ...d, planned: true } : d)) })
    await flushPromises()

    expect(getSuggestions).toHaveBeenLastCalledWith('2026-11-14')
  })

  it('shows a message when there is nothing to suggest', async () => {
    getSuggestions.mockResolvedValue([])
    const w = mountPanel()
    await flushPromises()

    expect(w.find('[data-test="suggestions-empty"]').exists()).toBe(true)
  })

  it('shows nothing when suggestions fail to load', async () => {
    getSuggestions.mockRejectedValue(new Error('500'))
    const w = mountPanel()
    await flushPromises()

    expect(w.findAll('[data-test="suggestion"]')).toHaveLength(0)
  })

  it('remembers when the panel was collapsed', async () => {
    const w = mountPanel()
    await flushPromises()

    await w.find('.v-expansion-panel-title').trigger('click')
    await flushPromises()

    expect(localStorage.getItem('planner.suggestionsOpen')).toBe('false')
  })
})
