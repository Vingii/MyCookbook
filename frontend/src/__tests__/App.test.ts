import { describe, it, expect, beforeEach, vi } from 'vitest'
import { ref } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import App from '../App.vue'
import { useUiStore } from '../stores/ui'

const xs = ref(false)
vi.mock('vuetify', async (importOriginal) => ({
  ...(await importOriginal<typeof import('vuetify')>()),
  useDisplay: () => ({ xs }),
}))

vi.mock('../api/changelog', () => ({
  changelogApi: {
    getVersion: vi.fn().mockResolvedValue('1.0.0'),
    getLastSeen: vi.fn().mockResolvedValue('1.0.0'),
    markAsSeen: vi.fn(),
  },
}))

const vuetify = createVuetify({ components, directives })
const Empty = { template: '<div />' }

async function mountApp() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: ['/', '/planner', '/settings'].map((path) => ({ path, component: Empty })),
  })
  router.push('/')
  await router.isReady()
  const wrapper = mount(App, {
    global: {
      plugins: [vuetify, router],
      stubs: {
        // VMenu needs visualViewport, which the test DOM lacks; render activator and items inline instead.
        VMenu: { template: '<div class="menu-stub"><slot name="activator" :props="{}" /><slot /></div>' },
        FeedbackDialog: true,
        ChangelogDialog: true,
      },
    },
  })
  await flushPromises()
  return wrapper
}

describe('App navbar', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('shows every action directly in the bar on wider screens', async () => {
    xs.value = false
    const wrapper = await mountApp()

    expect(wrapper.find('.menu-stub').exists()).toBe(false)
    expect(wrapper.find('header').text()).toContain('Settings')
    expect(wrapper.find('header').text()).toContain('Logout')
    expect(wrapper.find('header').text()).toContain('EN')
  })

  it('moves secondary actions into an overflow menu on phones', async () => {
    xs.value = true
    const wrapper = await mountApp()

    const menu = wrapper.find('.menu-stub')
    expect(menu.exists()).toBe(true)
    expect(menu.find('[aria-label="Menu"]').exists()).toBe(true)
    for (const label of ['Dark mode', 'Čeština', 'Report a bug', "What's New", 'Settings', 'Logout']) {
      expect(menu.text()).toContain(label)
    }
    // Primary navigation stays in the bar.
    expect(wrapper.find('header').text()).toContain('Planner')
  })

  it('toggles theme and language from the phone menu', async () => {
    xs.value = true
    const ui = useUiStore()
    const wrapper = await mountApp()

    const item = (title: string) =>
      wrapper.findAll('.v-list-item').find((w) => w.text().includes(title))!
    await item('Dark mode').trigger('click')
    await item('Čeština').trigger('click')

    expect(ui.theme).toBe('dark')
    expect(ui.locale).toBe('cs')
    expect(wrapper.find('.menu-stub').text()).toContain('Světlý režim')
    expect(wrapper.find('.menu-stub').text()).toContain('English')
  })
})
