import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import StepList from '../StepList.vue'
import type { StepDto } from '../../api/types'

// MC-81: a cookbook can turn off the step timeline.
const vuetify = createVuetify({ components, directives })
const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }] })

const steps: StepDto[] = [
  { id: 1, description: 'Chop the onion', order: 1, durationSeconds: 120, stepType: 'Active' },
  { id: 2, description: 'Simmer', order: 2, durationSeconds: 1800, stepType: 'Passive' },
]

function mountList(props: Record<string, unknown> = {}) {
  return mount(StepList, { props: { guid: 'g', steps, readonly: true, ...props }, global: { plugins: [vuetify, router] } })
}

describe('StepList timeline', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('shows the timeline bar with durations by default', () => {
    const w = mountList()
    expect(w.findAll('.step-bar')).toHaveLength(2)
    expect(w.text()).toContain('2m')
  })

  it('shows plain numbered steps without the timeline', () => {
    const w = mountList({ timeline: false })
    expect(w.findAll('.step-bar')).toHaveLength(0)
    expect(w.text()).toContain('1.')
    expect(w.text()).toContain('Simmer')
    expect(w.text()).not.toContain('2m')
  })
})
