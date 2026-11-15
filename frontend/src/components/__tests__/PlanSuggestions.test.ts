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
  { date: '2026-11-15', label: 'Sun', planned: true },
  { date: '2026-11-16', label: 'Mon', planned: false },
  { date: '2026-11-17', label: 'Tue', planned: false },
]

const suggestions: PlanSuggestionDto[] = [
  {
    recipeGuid: 'g1', recipeName: 'Gyros', category: 'Řecké', score: 0.9,
    companions: [{ recipeGuid: 't1', recipeName: 'Tzatziki' }, { recipeGuid: 'p1', recipeName: 'Pita' }],
  },
  { recipeGuid: 'g2', recipeName: 'Gulášovka', category: '', score: 0.5, companions: [] },
]

function mountPanel(props: Partial<{ days: typeof days; plansKey: string }> = {}) {
  return mount(PlanSuggestions, {
    props: { days, plansKey: 'a', ...props },
    global: {
      plugins: [vuetify],
      stubs: {
        RouterLink: { template: '<a><slot /></a>' },
        // Render the day menu inline instead of in a popup.
        VMenu: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
      },
    },
  })
}

async function open(w: ReturnType<typeof mountPanel>) {
  await w.find('.v-expansion-panel-title').trigger('click')
  await flushPromises()
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

  it('is collapsed by default and loads nothing until opened', async () => {
    const w = mountPanel()
    await flushPromises()

    expect(getSuggestions).not.toHaveBeenCalled()
    expect(w.find('[data-test="suggestion"]').exists()).toBe(false)

    await open(w)

    expect(getSuggestions).toHaveBeenCalledTimes(1)
    expect(getSuggestions).toHaveBeenCalledWith()
    expect(localStorage.getItem('planner.suggestionsOpen')).toBe('true')
  })

  it('opens right away when it was left open', async () => {
    localStorage.setItem('planner.suggestionsOpen', 'true')
    const w = mountPanel()
    await flushPromises()

    expect(w.findAll('[data-test="suggestion"]')).toHaveLength(2)
  })

  it('shows each suggestion with its side dishes and category', async () => {
    const w = mountPanel()
    await open(w)

    const cards = w.findAll('[data-test="suggestion"]')
    expect(cards[0]!.text()).toContain('Gyros')
    expect(cards[0]!.find('[data-test="suggestion-companions"]').text()).toBe('+ Tzatziki, Pita')
    expect(cards[0]!.text()).toContain('Řecké')
    expect(cards[1]!.find('[data-test="suggestion-companions"]').exists()).toBe(false)
  })

  it('adds a suggestion to the day picked from its menu', async () => {
    const w = mountPanel()
    await open(w)

    const card = w.findAll('[data-test="suggestion"]')[0]!
    expect(card.findAll('[data-test="suggestion-day"]').map((d) => d.text())).toEqual(['Sun', 'Mon', 'Tue'])
    await card.findAll('[data-test="suggestion-day"]')[2]!.trigger('click')

    expect(w.emitted('add')).toEqual([[{ suggestion: suggestions[0], date: '2026-11-17' }]])
  })

  it('emits drag events so a suggestion can be dropped on a day', async () => {
    const w = mountPanel()
    await open(w)

    const card = w.findAll('[data-test="suggestion"]')[0]!
    await card.trigger('dragstart')
    await card.trigger('dragend')

    expect(w.emitted('dragStart')![0]).toEqual([suggestions[0]])
    expect(w.emitted('dragEnd')).toHaveLength(1)
  })

  it('reloads when the plans change while open, not while collapsed', async () => {
    const w = mountPanel()
    await open(w)

    await w.setProps({ plansKey: 'b' })
    await flushPromises()
    expect(getSuggestions).toHaveBeenCalledTimes(2)

    await open(w) // collapse
    await w.setProps({ plansKey: 'c' })
    await flushPromises()
    expect(getSuggestions).toHaveBeenCalledTimes(2)

    await open(w) // reopen: plans changed meanwhile
    expect(getSuggestions).toHaveBeenCalledTimes(3)
  })

  it('does not reload when reopened without changes', async () => {
    const w = mountPanel()
    await open(w)
    await open(w)
    await open(w)

    expect(getSuggestions).toHaveBeenCalledTimes(1)
  })

  it('shows a message when there is nothing to suggest', async () => {
    getSuggestions.mockResolvedValue([])
    const w = mountPanel()
    await open(w)

    expect(w.find('[data-test="suggestions-empty"]').text()).toBe(
      'No suggestions: all your recipes are already planned around the next two weeks.')
  })

  it('shows nothing when suggestions fail to load', async () => {
    getSuggestions.mockRejectedValue(new Error('500'))
    const w = mountPanel()
    await open(w)

    expect(w.findAll('[data-test="suggestion"]')).toHaveLength(0)
  })
})
