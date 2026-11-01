import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import { AxiosError, AxiosHeaders } from 'axios'
import RecipeImportDialog from '../RecipeImportDialog.vue'
import { recipeImportApi, type RecipeDraft, type RecipeImportPreview } from '../../api/recipeImport'
import { useUiStore } from '../../stores/ui'

vi.mock('../../api/recipeImport', () => ({
  recipeImportApi: { preview: vi.fn(), save: vi.fn() },
}))

const vuetify = createVuetify({ components, directives })
const preview = vi.mocked(recipeImportApi.preview)
const save = vi.mocked(recipeImportApi.save)

function draft(name: string, overrides: Partial<RecipeDraft> = {}): RecipeDraft {
  return {
    name, category: 'Hlavní jídla', servings: 4, duration: 150, tags: ['tradiční'],
    ingredients: [{ name: 'cibule', amount: '3 ks' }],
    steps: [{ description: 'Cibuli osmahněte.', durationMinutes: 15, stepType: 'Active' }],
    ...overrides,
  }
}

const result: RecipeImportPreview = {
  sourceUrl: 'https://recipes.example/gulas',
  variants: [
    { key: 'suggested', recipe: draft('Hovězí guláš') },
    { key: 'original', recipe: draft('Beef goulash', { ingredients: [{ name: 'onions', amount: '3' }], tags: [] }) },
  ],
}

function axiosError(status: number, code?: string) {
  return new AxiosError('failed', String(status), undefined, undefined, {
    status, statusText: '', headers: {}, config: { headers: new AxiosHeaders() }, data: code ? { code } : undefined,
  })
}

function mountDialog() {
  return mount(RecipeImportDialog, {
    props: { modelValue: true },
    // VDialog needs visualViewport, which the test DOM lacks; render its content inline instead.
    global: { plugins: [vuetify], stubs: { VDialog: { template: '<div><slot /></div>' } } },
  })
}

type Wrapper = ReturnType<typeof mountDialog>
const button = (w: Wrapper, text: string) => w.findAll('button').find((b) => b.text() === text)!

async function loadLink(w: Wrapper, url = 'https://recipes.example/gulas') {
  await w.find('input').setValue(url)
  await button(w, 'Read recipe').trigger('click')
  await flushPromises()
}

enableAutoUnmount(afterEach)

describe('RecipeImportDialog (MC-69)', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
    preview.mockReset()
    save.mockReset()
  })

  it('reads the link in the app language and shows the adapted version first', async () => {
    useUiStore().setLocale('cs')
    preview.mockResolvedValue(result)
    const w = mountDialog()

    await w.find('input').setValue('  https://recipes.example/gulas  ')
    await button(w, 'Načíst recept').trigger('click')
    await flushPromises()

    expect(preview).toHaveBeenCalledWith('https://recipes.example/gulas', 'cs')
    expect(w.find('h2').text()).toBe('Hovězí guláš')
    expect(w.text()).toContain('3 ks cibule')
    expect(w.text()).toContain('Cibuli osmahněte.')
    expect(w.text()).toContain('tradiční')
    expect(w.text()).toContain('https://recipes.example/gulas')
  })

  it('switches to the original version', async () => {
    preview.mockResolvedValue(result)
    const w = mountDialog()
    await loadLink(w)

    await button(w, 'Original').trigger('click')

    expect(w.find('h2').text()).toBe('Beef goulash')
    expect(w.text()).toContain('3 onions')
    expect(w.text()).toContain('As written on the page.')
  })

  it('saves the chosen version and reports the new recipe', async () => {
    preview.mockResolvedValue(result)
    save.mockResolvedValue({ guid: 'new-guid' } as never)
    const w = mountDialog()
    await loadLink(w)
    await button(w, 'Original').trigger('click')

    await button(w, 'Save recipe').trigger('click')
    await flushPromises()

    expect(save).toHaveBeenCalledWith(result.variants[1]!.recipe)
    expect(w.emitted('created')).toEqual([['new-guid']])
    expect(w.emitted('update:modelValue')).toEqual([[false]])
  })

  it.each([
    ['invalid_url', 422, 'Enter a full web address starting with https://.'],
    ['unreachable', 422, 'The page could not be opened. Check the link.'],
    ['not_html', 422, 'This link is not a web page.'],
    ['no_recipe', 422, 'No recipe was found on this page.'],
    ['ai_failed', 422, 'Reading the recipe failed. Please try again.'],
    [undefined, 504, 'Reading the recipe failed. Please try again.'],
  ])('explains a %s error', async (code, status, message) => {
    preview.mockRejectedValue(axiosError(status, code))
    const w = mountDialog()

    await loadLink(w)

    expect(w.text()).toContain(message)
    expect(w.find('h2').exists()).toBe(false)
  })

  it('goes back to enter a different link', async () => {
    preview.mockResolvedValue(result)
    const w = mountDialog()
    await loadLink(w)

    await button(w, 'Different link').trigger('click')

    expect(w.find('h2').exists()).toBe(false)
    expect(w.find('input').exists()).toBe(true)
  })

  it('does not read an empty link', async () => {
    const w = mountDialog()

    expect(button(w, 'Read recipe').attributes('disabled')).toBeDefined()
    await w.find('input').trigger('keyup.enter')
    await flushPromises()

    expect(preview).not.toHaveBeenCalled()
  })
})
