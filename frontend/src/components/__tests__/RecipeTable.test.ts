import { describe, it, expect, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import RecipeTable from '../RecipeTable.vue'
import { useUiStore } from '../../stores/ui'

// MC-81: a cookbook can hide the preparation time and last cooked columns.
const vuetify = createVuetify({ components, directives })
const router = createRouter({ history: createMemoryHistory(), routes: [{ path: '/', component: { template: '<div />' } }] })

function mountTable(props: Record<string, unknown> = {}) {
  return mount(RecipeTable, { props: { recipes: [], ...props }, global: { plugins: [vuetify, router] } })
}

const headers = (w: ReturnType<typeof mountTable>) => w.findAll('th').map((th) => th.text()).filter(Boolean)

describe('RecipeTable columns', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
  })

  it('shows every column by default', () => {
    expect(headers(mountTable())).toEqual(['Name', 'Category', 'Duration', 'Last Cooked'])
  })

  it('leaves out hidden columns', () => {
    expect(headers(mountTable({ showDuration: false }))).toEqual(['Name', 'Category', 'Last Cooked'])
    expect(headers(mountTable({ showLastCooked: false }))).toEqual(['Name', 'Category', 'Duration'])
  })
})
