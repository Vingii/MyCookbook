import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import IngredientSummaryDialog from '../IngredientSummaryDialog.vue'
import { plannerApi } from '../../api/planner'
import { homeAssistantApi } from '../../api/homeAssistant'
import { useAuthStore } from '../../stores/auth'
import { VDatePicker } from 'vuetify/components'
import type { IngredientSummaryDto } from '../../api/types'
import { useUiStore } from '../../stores/ui'

// MC-77: shopping list of the planned recipes.
vi.mock('../../api/planner', () => ({
  plannerApi: { getIngredients: vi.fn() },
}))
vi.mock('../../api/homeAssistant', () => ({
  homeAssistantApi: { status: vi.fn(), sendShoppingList: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const getIngredients = vi.mocked(plannerApi.getIngredients)
const haStatus = vi.mocked(homeAssistantApi.status)
const haSend = vi.mocked(homeAssistantApi.sendShoppingList)

const items: IngredientSummaryDto[] = [
  { name: 'Vepřové', totals: [{ quantity: 1000, unit: 'g' }], otherAmounts: [], recipes: ['Gyros'] },
  { name: 'cibule', totals: [{ quantity: 1.5, unit: 'ks' }], otherAmounts: ['velká'], recipes: ['Gyros', 'Guláš'] },
  { name: 'Sůl', totals: [], otherAmounts: [], recipes: ['Guláš'] },
]

function mountDialog(props: Record<string, unknown> = {}) {
  return mount(IngredientSummaryDialog, {
    props: { modelValue: true, ...props },
    // VDialog needs visualViewport, which the test DOM lacks; render its content inline instead.
    global: { plugins: [vuetify], stubs: {
      VDialog: { template: '<div><slot /></div>' },
      // Render the date pickers inline instead of in a popup.
      VMenu: { template: '<div><slot name="activator" :props="{}" /><slot /></div>' },
    } },
  })
}

type Wrapper = ReturnType<typeof mountDialog>
const rows = (w: Wrapper) => w.findAll('[data-test="item"]')
const copyButton = (w: Wrapper) => w.find('[data-test="copy"]')
const pickers = (w: Wrapper) => w.findAllComponents(VDatePicker)

enableAutoUnmount(afterEach)

describe('IngredientSummaryDialog', () => {
  let writeText: ReturnType<typeof vi.fn>

  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    getIngredients.mockReset()
    getIngredients.mockResolvedValue(items)
    haStatus.mockReset()
    haStatus.mockResolvedValue({ configured: false })
    haSend.mockReset()
    haSend.mockResolvedValue(undefined as never)
    vi.useFakeTimers({ toFake: ['Date'] })
    vi.setSystemTime(new Date(2026, 10, 15, 0, 30))
    writeText = vi.fn().mockResolvedValue(undefined)
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true })
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('loads next week by default, for the viewed user (Czech week starts on Monday)', async () => {
    useUiStore().locale = 'cs'
    mountDialog({ user: 'anna', shareToken: 'tok' })
    await flushPromises()

    // Today is Sunday 2026-11-15.
    expect(getIngredients).toHaveBeenCalledTimes(1)
    expect(getIngredients).toHaveBeenCalledWith('2026-11-16', '2026-11-22', 'anna', 'tok')
  })

  it('in English the default week and the pickers start on Sunday', async () => {
    const w = mountDialog()
    await flushPromises()

    expect(getIngredients).toHaveBeenCalledWith('2026-11-22', '2026-11-28', undefined, undefined)
    expect(pickers(w).map((p) => p.props('firstDayOfWeek'))).toEqual([0, 0])
  })

  it('Czech date pickers start on Monday', async () => {
    useUiStore().locale = 'cs'
    const w = mountDialog()
    await flushPromises()

    expect(pickers(w).map((p) => p.props('firstDayOfWeek'))).toEqual([1, 1])
  })

  it('shows the dates in the UI language', async () => {
    useUiStore().locale = 'cs'
    const w = mountDialog()
    await flushPromises()

    expect((w.find('[data-test="from"] input').element as HTMLInputElement).value).toBe(
      new Date(2026, 10, 16).toLocaleDateString('cs-CZ', { weekday: 'short', day: 'numeric', month: 'numeric', year: 'numeric' }))
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

    pickers(w)[1]!.vm.$emit('update:modelValue', new Date(2026, 10, 30))
    await flushPromises()

    expect(getIngredients).toHaveBeenLastCalledWith('2026-11-22', '2026-11-30', undefined, undefined)
  })

  it('moving the start past the end moves the end along', async () => {
    const w = mountDialog()
    await flushPromises()

    pickers(w)[0]!.vm.$emit('update:modelValue', new Date(2026, 11, 5))
    await flushPromises()

    expect(getIngredients).toHaveBeenLastCalledWith('2026-12-05', '2026-12-05', undefined, undefined)
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

  it('has no Home Assistant button when no webhook is set', async () => {
    const w = mountDialog()
    await flushPromises()

    expect(w.find('[data-test="send-ha"]').exists()).toBe(false)
  })

  it('does not ask guests for the Home Assistant setting', async () => {
    useAuthStore().isGuest = true
    mountDialog()
    await flushPromises()

    expect(haStatus).not.toHaveBeenCalled()
  })

  it('sends the items not ticked off to Home Assistant', async () => {
    haStatus.mockResolvedValue({ configured: true })
    const w = mountDialog()
    await flushPromises()

    await rows(w)[1]!.trigger('click')
    await w.find('[data-test="send-ha"]').trigger('click')
    await flushPromises()

    expect(haSend).toHaveBeenCalledWith([
      { name: 'cibule', amount: '1.5 ks + velká', recipes: ['Gyros', 'Guláš'] },
      { name: 'Vepřové', amount: '1000 g', recipes: ['Gyros'] },
    ])
    expect(w.find('[data-test="ha-message"]').text()).toBe('Sent 2 items to Home Assistant.')
  })

  it('a read-only API token session can still send to Home Assistant', async () => {
    useAuthStore().isReadOnly = true
    haStatus.mockResolvedValue({ configured: true })
    const w = mountDialog()
    await flushPromises()

    await w.find('[data-test="send-ha"]').trigger('click')
    await flushPromises()

    expect(haSend).toHaveBeenCalled()
  })

  it('reports a Home Assistant failure', async () => {
    haStatus.mockResolvedValue({ configured: true })
    haSend.mockRejectedValue(new Error('502'))
    const w = mountDialog()
    await flushPromises()

    await w.find('[data-test="send-ha"]').trigger('click')
    await flushPromises()

    expect(w.find('[data-test="ha-message"]').text()).toBe('Home Assistant could not be reached.')
  })

  it('emits close from the close button', async () => {
    const w = mountDialog()
    await flushPromises()

    await w.find('button[aria-label="Close"]').trigger('click')

    expect(w.emitted('update:modelValue')).toEqual([[false]])
  })
})
