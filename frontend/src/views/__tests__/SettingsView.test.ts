import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import { AxiosError, AxiosHeaders } from 'axios'
import SettingsView from '../SettingsView.vue'
import { homeAssistantApi } from '../../api/homeAssistant'
import { useUiStore } from '../../stores/ui'
import { useAuthStore } from '../../stores/auth'
import { authApi } from '../../api/auth'

// MC-77: Home Assistant webhook settings.
vi.mock('../../api/auth', () => ({
  authApi: { getShareToken: vi.fn().mockResolvedValue({ token: null }) },
}))
vi.mock('../../api/homeAssistant', () => ({
  homeAssistantApi: { get: vi.fn(), save: vi.fn(), remove: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const haGet = vi.mocked(homeAssistantApi.get)
const haSave = vi.mocked(homeAssistantApi.save)
const haRemove = vi.mocked(homeAssistantApi.remove)
const url = 'http://ha.local:8123/api/webhook/mycookbook-abc'

function mountView() {
  return mount(SettingsView, { global: { plugins: [vuetify] } })
}

type Wrapper = ReturnType<typeof mountView>
const urlInput = (w: Wrapper) => w.find('[data-test="ha-url"] input')

enableAutoUnmount(afterEach)

describe('SettingsView – Home Assistant', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    vi.mocked(authApi.getShareToken).mockClear()
    haGet.mockReset().mockResolvedValue({ webhookUrl: null })
    haSave.mockReset().mockResolvedValue(undefined as never)
    haRemove.mockReset().mockResolvedValue(undefined as never)
  })

  it('read-only API token: shows a notice instead of the settings and loads nothing', async () => {
    useAuthStore().isReadOnly = true
    const w = mountView()
    await flushPromises()

    expect(w.find('[data-test="read-only"]').text()).toContain('API token')
    expect(w.find('[data-test="ha-card"]').exists()).toBe(false)
    expect(haGet).not.toHaveBeenCalled()
    expect(vi.mocked(authApi.getShareToken)).not.toHaveBeenCalled()
  })

  it('shows the saved webhook URL', async () => {
    haGet.mockResolvedValue({ webhookUrl: url })
    const w = mountView()
    await flushPromises()

    expect((urlInput(w).element as HTMLInputElement).value).toBe(url)
    expect(w.find('[data-test="ha-remove"]').exists()).toBe(true)
  })

  it('saves a webhook URL', async () => {
    const w = mountView()
    await flushPromises()

    await urlInput(w).setValue(` ${url} `)
    await w.find('[data-test="ha-save"]').trigger('click')
    await flushPromises()

    expect(haSave).toHaveBeenCalledWith(url)
    expect(w.find('[data-test="ha-save"]').text()).toBe('Saved.')
    expect(w.find('[data-test="ha-remove"]').exists()).toBe(true)
  })

  it('explains a rejected URL', async () => {
    haSave.mockRejectedValue(new AxiosError('bad', '400', undefined, undefined, {
      status: 400, statusText: '', headers: {}, config: { headers: new AxiosHeaders() }, data: {},
    }))
    const w = mountView()
    await flushPromises()

    await urlInput(w).setValue('http://ha.local/api/services')
    await w.find('[data-test="ha-save"]').trigger('click')
    await flushPromises()

    expect(w.find('[data-test="ha-card"]').text()).toContain('Enter a Home Assistant webhook URL')
  })

  it('removes the webhook URL', async () => {
    haGet.mockResolvedValue({ webhookUrl: url })
    const w = mountView()
    await flushPromises()

    await w.find('[data-test="ha-remove"]').trigger('click')
    await flushPromises()

    expect(haRemove).toHaveBeenCalled()
    expect((urlInput(w).element as HTMLInputElement).value).toBe('')
    expect(w.find('[data-test="ha-remove"]').exists()).toBe(false)
  })

  it('puts the webhook ID from the URL into the automation YAML', async () => {
    haGet.mockResolvedValue({ webhookUrl: url })
    const w = mountView()
    await flushPromises()

    const yaml = (w.vm as unknown as { haYaml: string }).haYaml
    expect(yaml).toContain('webhook_id: mycookbook-abc')
    expect(yaml).toContain(`for_each: "{{ trigger.json['items'] }}"`)
    expect(yaml).toContain('item: "{{ repeat.item.summary }}"')
  })

  it('suggests a random webhook ID when none is set', async () => {
    const w = mountView()
    await flushPromises()

    expect((w.vm as unknown as { haYaml: string }).haYaml).toMatch(/webhook_id: mycookbook-[0-9a-f]{24}\n/)
  })
})
