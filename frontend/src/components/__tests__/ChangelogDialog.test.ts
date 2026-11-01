import { describe, it, expect, beforeEach, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import ChangelogDialog from '../ChangelogDialog.vue'
import { changelogApi } from '../../api/changelog'
import { useUiStore } from '../../stores/ui'

vi.mock('../../api/changelog', () => ({
  changelogApi: { getEntries: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const getEntries = vi.mocked(changelogApi.getEntries)

function mountDialog() {
  return mount(ChangelogDialog, {
    props: { modelValue: false },
    // VDialog needs visualViewport, which jsdom lacks; render its content inline instead.
    global: { plugins: [vuetify], stubs: { VDialog: { template: '<div><slot /></div>' } } },
  })
}

describe('ChangelogDialog', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    getEntries.mockReset()
    getEntries.mockImplementation(async (lang) => [
      { version: '1.0.0', rawHtml: lang === 'cs' ? '<p>První vydání</p>' : '<p>First release</p>' },
    ])
  })

  it('fetches entries in the current locale when opened', async () => {
    useUiStore().setLocale('cs')
    const wrapper = mountDialog()

    await wrapper.setProps({ modelValue: true })
    await flushPromises()

    expect(getEntries).toHaveBeenCalledWith('cs')
    expect(wrapper.text()).toContain('První vydání')
  })

  it('shows the version number without a "v" prefix', async () => {
    const wrapper = mountDialog()

    await wrapper.setProps({ modelValue: true })
    await flushPromises()

    expect(wrapper.text()).toContain('1.0.0')
    expect(wrapper.text()).not.toContain('v1.0.0')
  })

  it('does not refetch when reopened in the same locale', async () => {
    const wrapper = mountDialog()

    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    await wrapper.setProps({ modelValue: false })
    await wrapper.setProps({ modelValue: true })
    await flushPromises()

    expect(getEntries).toHaveBeenCalledTimes(1)
  })

  it('refetches when the locale changes while open', async () => {
    const ui = useUiStore()
    const wrapper = mountDialog()

    await wrapper.setProps({ modelValue: true })
    await flushPromises()
    ui.setLocale('cs')
    await flushPromises()

    expect(getEntries).toHaveBeenNthCalledWith(1, 'en')
    expect(getEntries).toHaveBeenNthCalledWith(2, 'cs')
    expect(wrapper.text()).toContain('První vydání')
  })
})
