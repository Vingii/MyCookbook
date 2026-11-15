import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import IngredientSummaryDialog from '../IngredientSummaryDialog.vue'
import { plannerApi } from '../../api/planner'
import type { IngredientSummaryDto } from '../../api/types'
import { useUiStore } from '../../stores/ui'

// MC-77: shopping list of the planned recipes.
vi.mock('../../api/planner', () => ({
  plannerApi: { getIngredients: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const getIngredients = vi.mocked(plannerApi.getIngredients)

const items: IngredientSummaryDto[] = [
  { name: 'Vepřové', totals: [{ quantity: 1000, unit: 'g' }], otherAmounts: [], recipes: ['Gyros'] },
  { name: 'cibule', totals: [{ quantity: 1.5, unit: 'ks' }], otherAmounts: ['velká'], recipes: ['Gyros', 'Guláš'] },
  { name: 'Sůl', totals: [], otherAmounts: [], recipes: ['Guláš'] },
]

function mountDialog(props: Record<string, unknown> = {}) {
  return mount(IngredientSummaryDialog, {
    props: { modelValue: true, ...props },
    // VDialog needs visualViewport, which the test DOM lacks; render its content inline instead.
    global: { plugins: [vuetify], stubs: { VDialog: { template: '<div><slot /></div>' } } },
  })
}

type Wrapper = ReturnType<typeof mountDialog>
const rows = (w: Wrapper) => w.findAll('[data-test="item"]')
const copyButton = (w: Wrapper) => w.findAll('button').find((b) => b.text() === 'Copy' || b.text() === 'Kopírovat')!

enableAutoUnmount(afterEach)

describe('IngredientSummaryDialog', () => {
  let writeText: ReturnType<typeof vi.fn>

  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    getIngredients.mockReset()
    getIngredients.mockResolvedValue(items)
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 15, 0, 30))
    writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('loads the next seven days by default, for the viewed user', async () => {
    mountDialog({ user: 'anna', shareToken: 'tok' })
    await flushPromises()

    expect(getIngredients).toHaveBeenCalledWith('2026-11-15', '2026-11-21', 'anna', 'tok')
  })

  it('lists ingredients alphabetically with amounts and recipes', async () => {
    const w = mountDialog()
    await flushPromises()

    const texts = rows(w).map((r) => r.text())
    expect(texts).toHaveLength(3)
    expect(texts[0]).toContain('cibule')
    expect(texts[0]).toContain('1.5 ks + velká')
    expect(texts[0]).toContain('Gyros, Guláš')
    expect(texts[1]).toContain('Sůl')
    expect(texts[2]).toContain('Vepřové')
    expect(texts[2]).toContain('1000 g')
  })

  it('formats numbers for Czech', async () => {
    useUiStore().locale = 'cs'
    const w = mountDialog()
    await flushPromises()

    expect(rows(w)[0]!.text()).toContain('1,5 ks')
  })

  it('reloads when the date range changes', async () => {
    const w = mountDialog()
    await flushPromises()

    await w.find('[data-test="to"] input').setValue('2026-11-30')
    await flushPromises()

    expect(getIngredients).toHaveBeenLastCalledWith('2026-11-15', '2026-11-30', undefined, undefined)
  })

  it('does not load when the end is before the start', async () => {
    const w = mountDialog()
    await flushPromises()
    getIngredients.mockClear()

    await w.find('[data-test="to"] input').setValue('2026-11-10')
    await flushPromises()

    expect(getIngredients).not.toHaveBeenCalled()
    expect(w.find('[data-test="empty"]').exists()).toBe(true)
  })

  it('shows a message when nothing is planned', async () => {
    getIngredients.mockResolvedValue([])
    const w = mountDialog()
    await flushPromises()

    expect(w.find('[data-test="empty"]').text()).toBe('No recipes are planned to cook in these days.')
    expect(copyButton(w).attributes('disabled')).toBeDefined()
  })

  it('copies the items not ticked off as text', async () => {
    const w = mountDialog()
    await flushPromises()

    await rows(w)[1]!.trigger('click')
    await copyButton(w).trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('cibule: 1.5 ks + velká\nVepřové: 1000 g')
    expect(w.text()).toContain('Copied!')
  })

  it('ticking an item twice puts it back on the list', async () => {
    const w = mountDialog()
    await flushPromises()

    await rows(w)[1]!.trigger('click')
    await rows(w)[1]!.trigger('click')
    await copyButton(w).trigger('click')
    await flushPromises()

    expect(writeText).toHaveBeenCalledWith('cibule: 1.5 ks + velká\nSůl\nVepřové: 1000 g')
  })

  it('emits close from the close button', async () => {
    const w = mountDialog()
    await flushPromises()

    await w.find('button[aria-label="Close"]').trigger('click')

    expect(w.emitted('update:modelValue')).toEqual([[false]])
  })
})
