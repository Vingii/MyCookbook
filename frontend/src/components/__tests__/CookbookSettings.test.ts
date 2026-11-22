import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import CookbookSettings from '../CookbookSettings.vue'
import { cookbooksApi } from '../../api/cookbooks'
import { useUiStore } from '../../stores/ui'
import type { CookbookDto } from '../../api/types'

// MC-80: managing cookbooks in Settings.
vi.mock('../../api/cookbooks', () => ({
  cookbooksApi: { getAll: vi.fn(), create: vi.fn(), update: vi.fn(), delete: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const api = vi.mocked(cookbooksApi)

const home: CookbookDto = { id: 1, name: '', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: true, recipeCount: 3 }
const baking: CookbookDto = { id: 2, name: 'Baking', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true, isDefault: false, recipeCount: 0 }

function mountCard() {
  return mount(CookbookSettings, { global: { plugins: [vuetify] } })
}

type Wrapper = ReturnType<typeof mountCard>
const row = (w: Wrapper, id: number) => w.find(`[data-test="cookbook-row-${id}"]`)
const nameInput = (w: Wrapper, id: number) => row(w, id).find('[data-test="cookbook-name"] input')
const deleteButton = (w: Wrapper, id: number) => row(w, id).find('[data-test="cookbook-delete"]')

enableAutoUnmount(afterEach)

describe('CookbookSettings', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    useUiStore().locale = 'en'
    api.getAll.mockReset().mockResolvedValue([home, baking])
    api.create.mockReset().mockResolvedValue({ ...baking, id: 3 })
    api.update.mockReset().mockResolvedValue(undefined as never)
    api.delete.mockReset().mockResolvedValue(undefined as never)
    window.confirm = vi.fn().mockReturnValue(true)
  })

  it('lists cookbooks with names, planner switches and recipe counts', async () => {
    const w = mountCard()
    await flushPromises()

    expect((nameInput(w, 1).element as HTMLInputElement).value).toBe('')
    expect(nameInput(w, 1).attributes('placeholder')).toBe('My recipes')
    expect((nameInput(w, 2).element as HTMLInputElement).value).toBe('Baking')
    expect(row(w, 1).text()).toContain('Recipes: 3')
    expect(row(w, 1).text()).toContain('Show in Planner')
  })

  it('renames a cookbook on Enter, keeping its planner setting', async () => {
    const w = mountCard()
    await flushPromises()

    await nameInput(w, 1).setValue('  Home ')
    await nameInput(w, 1).trigger('keyup.enter')
    await flushPromises()

    expect(api.update).toHaveBeenCalledWith(1, { name: 'Home', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true })
    // Reloaded so the Recipes page switcher shows the new name.
    expect(api.getAll).toHaveBeenCalledTimes(2)
  })

  it('undoes clearing a name instead of saving it', async () => {
    const w = mountCard()
    await flushPromises()

    await nameInput(w, 2).setValue('   ')
    await nameInput(w, 2).trigger('blur')
    await flushPromises()

    expect(api.update).not.toHaveBeenCalled()
    expect((nameInput(w, 2).element as HTMLInputElement).value).toBe('Baking')
  })

  it('hides a cookbook from the planner', async () => {
    const w = mountCard()
    await flushPromises()

    await row(w, 2).find('[data-test="cookbook-planner"] input').setValue(false)
    await flushPromises()

    expect(api.update).toHaveBeenCalledWith(2, { name: 'Baking', showInPlanner: false, showDuration: true, showLastCooked: true, showServings: true, showStepTimeline: true })
  })

  // MC-81
  it('turns off what recipes in a cookbook show, keeping the other settings', async () => {
    const w = mountCard()
    await flushPromises()

    expect(row(w, 2).text()).toContain('Show in recipes:')
    expect(row(w, 2).text()).toContain('Step timeline')
    await row(w, 2).find('[data-test="cookbook-showServings"] input').setValue(false)
    await flushPromises()

    expect(api.update).toHaveBeenCalledWith(2, {
      name: 'Baking', showInPlanner: true, showDuration: true, showLastCooked: true, showServings: false, showStepTimeline: true,
    })
  })

  it('shows which fields are turned off', async () => {
    api.getAll.mockResolvedValue([home, { ...baking, showLastCooked: false }])
    const w = mountCard()
    await flushPromises()

    const checked = (id: number, key: string) =>
      (row(w, id).find(`[data-test="cookbook-${key}"] input`).element as HTMLInputElement).checked
    expect(checked(2, 'showLastCooked')).toBe(false)
    expect(checked(2, 'showDuration')).toBe(true)
    expect(checked(1, 'showLastCooked')).toBe(true)
  })

  it('adds a cookbook', async () => {
    const w = mountCard()
    await flushPromises()

    await w.find('[data-test="cookbook-new-name"] input').setValue('Testing')
    await w.find('[data-test="cookbook-add"]').trigger('click')
    await flushPromises()

    expect(api.create).toHaveBeenCalledWith({ name: 'Testing', showInPlanner: true })
    expect((w.find('[data-test="cookbook-new-name"] input').element as HTMLInputElement).value).toBe('')
  })

  it('deletes only empty cookbooks, and never the last one', async () => {
    const w = mountCard()
    await flushPromises()

    expect(deleteButton(w, 1).attributes('disabled')).toBeDefined()
    expect(deleteButton(w, 1).attributes('title')).toBe('Move or delete its recipes first.')
    expect(deleteButton(w, 2).attributes('disabled')).toBeUndefined()

    await deleteButton(w, 2).trigger('click')
    await flushPromises()
    expect(api.delete).toHaveBeenCalledWith(2)
  })

  it('does not delete when the confirmation is cancelled', async () => {
    window.confirm = vi.fn().mockReturnValue(false)
    const w = mountCard()
    await flushPromises()

    await deleteButton(w, 2).trigger('click')
    await flushPromises()

    expect(api.delete).not.toHaveBeenCalled()
  })

  it('explains why the only cookbook cannot be deleted', async () => {
    api.getAll.mockResolvedValue([{ ...home, recipeCount: 0 }])
    const w = mountCard()
    await flushPromises()

    expect(deleteButton(w, 1).attributes('disabled')).toBeDefined()
    expect(deleteButton(w, 1).attributes('title')).toBe('You need at least one cookbook.')
  })

  it('shows an error when saving fails', async () => {
    api.create.mockRejectedValue(new Error('500'))
    const w = mountCard()
    await flushPromises()

    await w.find('[data-test="cookbook-new-name"] input').setValue('Testing')
    await w.find('[data-test="cookbook-add"]').trigger('click')
    await flushPromises()

    expect(w.find('[data-test="cookbook-error"]').text()).toBe('Failed to save the cookbook.')
  })

  it('shows an error when cookbooks cannot be loaded', async () => {
    api.getAll.mockRejectedValue(new Error('500'))
    const w = mountCard()
    await flushPromises()

    expect(w.find('[data-test="cookbook-error"]').text()).toBe('Failed to load cookbooks.')
  })

  it('is in Czech', async () => {
    useUiStore().locale = 'cs'
    const w = mountCard()
    await flushPromises()

    expect(w.text()).toContain('Kuchařky')
    expect(nameInput(w, 1).attributes('placeholder')).toBe('Moje recepty')
  })
})
