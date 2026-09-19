<template>
  <div ref="wrapper" class="wiki-textarea">
    <v-textarea
      :model-value="modelValue"
      :placeholder="placeholder"
      density="compact"
      hide-details
      variant="outlined"
      auto-grow
      rows="2"
      @update:model-value="onInput"
      @keydown="onKeydown"
      @keyup="onKeyup"
      @click="refreshSuggestions"
      @blur="onBlur"
    />
    <v-card v-if="suggestions.length" class="wiki-suggestions" elevation="8">
      <v-list density="compact" class="py-1">
        <v-list-item
          v-for="(name, i) in suggestions"
          :key="name"
          :active="i === activeIndex"
          @mousedown.prevent="accept(name)"
        >
          <v-list-item-title>{{ name }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { findLinkQuery, applyLinkSuggestion, suggestRecipeNames, type LinkQuery } from '../composables/useRecipeLinks'

const props = defineProps<{ modelValue: string; names: string[]; placeholder?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string]; enter: []; blur: [] }>()

const wrapper = ref<HTMLElement | null>(null)
const suggestions = ref<string[]>([])
const activeIndex = ref(0)
/** The text the open suggestions were computed from, so completing does not depend on a re-render. */
let pending: { range: LinkQuery; text: string } | null = null

function inputEl(): HTMLTextAreaElement | null {
  return wrapper.value?.querySelector('textarea') ?? null
}

function closeSuggestions() {
  suggestions.value = []
  pending = null
}

/** Opens the recipe-name dropdown whenever the caret sits inside an unclosed `[[`. */
function refreshSuggestions() {
  const el = inputEl()
  if (!el) return closeSuggestions()

  const found = findLinkQuery(el.value, el.selectionStart)
  if (!found) return closeSuggestions()

  const matches = suggestRecipeNames(props.names, found.query)
  if (!matches.length) return closeSuggestions()

  pending = { range: found, text: el.value }
  suggestions.value = matches
  activeIndex.value = 0
}

function onInput(value: string) {
  emit('update:modelValue', value)
  refreshSuggestions()
}

function onKeydown(e: KeyboardEvent) {
  if (suggestions.value.length) {
    if (e.key === 'ArrowDown') {
      e.preventDefault()
      activeIndex.value = (activeIndex.value + 1) % suggestions.value.length
      return
    }
    if (e.key === 'ArrowUp') {
      e.preventDefault()
      activeIndex.value = (activeIndex.value - 1 + suggestions.value.length) % suggestions.value.length
      return
    }
    if (e.key === 'Enter' || e.key === 'Tab') {
      e.preventDefault()
      const chosen = suggestions.value[activeIndex.value]
      if (chosen) accept(chosen)
      return
    }
    if (e.key === 'Escape') {
      e.preventDefault()
      e.stopPropagation()
      closeSuggestions()
      return
    }
  }

  if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.altKey && !e.metaKey) {
    e.preventDefault()
    emit('enter')
  }
}

function onKeyup(e: KeyboardEvent) {
  // Re-evaluate after the caret has actually moved. Up/Down are handled above while the
  // dropdown is open, so re-running here would reset the highlighted suggestion.
  const caretMoved = e.key === 'ArrowLeft' || e.key === 'ArrowRight' || e.key === 'Home' || e.key === 'End'
  const verticalMove = (e.key === 'ArrowUp' || e.key === 'ArrowDown') && !suggestions.value.length
  if (caretMoved || verticalMove) refreshSuggestions()
}

function accept(name: string) {
  const el = inputEl()
  if (!pending || !el) return

  const result = applyLinkSuggestion(pending.text, pending.range, name)
  closeSuggestions()
  // Write straight to the element so the caret lands correctly without waiting for a re-render.
  el.value = result.text
  el.setSelectionRange(result.caret, result.caret)
  el.focus()
  emit('update:modelValue', result.text)
}

function onBlur() {
  closeSuggestions()
  emit('blur')
}

defineExpose({ focus: () => inputEl()?.focus() })
</script>

<style scoped>
.wiki-textarea {
  position: relative;
}
.wiki-suggestions {
  position: absolute;
  top: 100%;
  left: 0;
  z-index: 10;
  min-width: 220px;
  max-width: 100%;
  max-height: 240px;
  overflow-y: auto;
}
</style>
