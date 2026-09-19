import { describe, it, expect } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import StepTextarea from '../StepTextarea.vue'

const vuetify = createVuetify({ components, directives })

const NAMES = ['Baklava', 'Gyros', 'Tzatziki']

function mountTextarea(modelValue = '') {
  const wrapper = mount(StepTextarea, {
    props: {
      modelValue,
      names: NAMES,
      // Mirror a real v-model parent, which writes the value straight back.
      'onUpdate:modelValue': (v: string) => wrapper.setProps({ modelValue: v }),
    },
    global: { plugins: [vuetify] },
  })
  return { wrapper, textarea: wrapper.find('textarea') }
}

/** Simulates typing by setting the value + caret, then firing the input event Vuetify listens to. */
async function type(wrapper: ReturnType<typeof mountTextarea>, value: string, caret = value.length) {
  const el = wrapper.textarea.element as HTMLTextAreaElement
  el.value = value
  el.setSelectionRange(caret, caret)
  await wrapper.textarea.trigger('input')
}

const suggestionTexts = (wrapper: VueWrapper) =>
  wrapper.findAll('.wiki-suggestions .v-list-item-title').map((n) => n.text())

/** `emitted().at(-1)` is not available under the app's TypeScript lib target. */
function lastEmitted(wrapper: VueWrapper, event: string): unknown[] | undefined {
  const events = wrapper.emitted(event)
  return events?.[events.length - 1]
}

describe('StepTextarea', () => {
  it('shows no suggestions for ordinary text', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with tzatziki')
    expect(suggestionTexts(t.wrapper)).toEqual([])
  })

  it('lists every recipe right after [[ is typed', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[')
    expect(suggestionTexts(t.wrapper)).toEqual(NAMES)
  })

  it('narrows the list as the user keeps typing', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tza')
    expect(suggestionTexts(t.wrapper)).toEqual(['Tzatziki'])
  })

  it('hides the list when nothing matches', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[zzz')
    expect(suggestionTexts(t.wrapper)).toEqual([])
  })

  it('completes the link when a suggestion is clicked', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tza')
    await t.wrapper.find('.wiki-suggestions .v-list-item').trigger('mousedown')

    const emitted = lastEmitted(t.wrapper, 'update:modelValue')
    expect(emitted).toEqual(['Serve with [[Tzatziki]]'])
    expect(suggestionTexts(t.wrapper)).toEqual([])
  })

  it('completes the link on Enter instead of submitting the step', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tza')
    await t.textarea.trigger('keydown', { key: 'Enter' })

    expect(lastEmitted(t.wrapper, 'update:modelValue')).toEqual(['Serve with [[Tzatziki]]'])
    expect(t.wrapper.emitted('enter')).toBeUndefined()
  })

  it('moves the caret past the inserted link', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tza')
    await t.textarea.trigger('keydown', { key: 'Enter' })

    const el = t.textarea.element as HTMLTextAreaElement
    expect(el.selectionStart).toBe('Serve with [[Tzatziki]]'.length)
  })

  it('accepts the suggestion highlighted with the arrow keys', async () => {
    const t = mountTextarea()
    await type(t, '[[')
    await t.textarea.trigger('keydown', { key: 'ArrowDown' })
    await t.textarea.trigger('keydown', { key: 'Enter' })

    expect(lastEmitted(t.wrapper, 'update:modelValue')).toEqual(['[[Gyros]]'])
  })

  it('completes the reference half of an existing link without losing the label', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tzatzikem|Tza]].', 26)
    await t.textarea.trigger('keydown', { key: 'Enter' })

    expect(lastEmitted(t.wrapper, 'update:modelValue')).toEqual(['Serve with [[tzatzikem|Tzatziki]].'])
  })

  it('stays quiet while the label of a finished link is edited', async () => {
    const t = mountTextarea()
    await type(t, 'Serve with [[tza|Tzatziki]].', 16)
    expect(suggestionTexts(t.wrapper)).toEqual([])
  })

  it('emits enter when the dropdown is closed', async () => {
    const t = mountTextarea()
    await type(t, 'Grill the meat.')
    await t.textarea.trigger('keydown', { key: 'Enter' })

    expect(t.wrapper.emitted('enter')).toHaveLength(1)
  })

  it('leaves shift+enter alone so descriptions can be multi-line', async () => {
    const t = mountTextarea()
    await type(t, 'Grill the meat.')
    await t.textarea.trigger('keydown', { key: 'Enter', shiftKey: true })

    expect(t.wrapper.emitted('enter')).toBeUndefined()
  })

  it('closes the dropdown on Escape without completing', async () => {
    const t = mountTextarea()
    await type(t, '[[tza')
    await t.textarea.trigger('keydown', { key: 'Escape' })

    expect(suggestionTexts(t.wrapper)).toEqual([])
    expect(lastEmitted(t.wrapper, 'update:modelValue')).toEqual(['[[tza'])
  })

  it('closes the dropdown and emits blur when focus leaves', async () => {
    const t = mountTextarea()
    await type(t, '[[tza')
    await t.textarea.trigger('blur')

    expect(suggestionTexts(t.wrapper)).toEqual([])
    expect(t.wrapper.emitted('blur')).toHaveLength(1)
  })
})
