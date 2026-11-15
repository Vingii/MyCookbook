<template>
  <v-expansion-panels v-model="openPanel" class="mb-4" data-test="suggestions">
    <v-expansion-panel value="open">
      <v-expansion-panel-title>
        <v-icon icon="mdi-lightbulb-on-outline" size="small" class="mr-2" />
        {{ ui.t.suggestions }}
      </v-expansion-panel-title>
      <v-expansion-panel-text>
        <p class="text-caption text-medium-emphasis mb-2">{{ ui.t.suggestionsHint }}</p>

        <div v-if="loading && suggestions.length === 0" class="d-flex justify-center py-4">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>
        <p v-else-if="suggestions.length === 0" class="text-medium-emphasis py-2" data-test="suggestions-empty">
          {{ ui.t.suggestionsEmpty }}
        </p>
        <v-row v-else dense>
          <v-col v-for="s in suggestions" :key="s.recipeGuid" cols="12" sm="6" md="4" lg="3">
            <v-card
              variant="tonal"
              class="suggestion fill-height"
              draggable="true"
              data-test="suggestion"
              @dragstart="onDragStart($event, s)"
              @dragend="emit('dragEnd')"
            >
              <div class="d-flex align-start pa-2 ga-1">
                <v-icon icon="mdi-drag" size="x-small" class="drag-handle mt-1 text-medium-emphasis" />
                <div class="flex-grow-1 min-width-0">
                  <router-link :to="`/recipe/${s.recipeGuid}`" class="text-body-2 font-weight-medium text-decoration-none">
                    {{ s.recipeName }}
                  </router-link>
                  <div v-if="s.companions.length" class="text-body-2" data-test="suggestion-companions">
                    + {{ s.companions.map((c) => c.recipeName).join(', ') }}
                  </div>
                  <div v-if="s.category" class="text-caption text-medium-emphasis">{{ s.category }}</div>
                </div>
                <v-menu location="bottom end">
                  <template #activator="{ props: menu }">
                    <v-btn
                      v-bind="menu"
                      icon="mdi-plus"
                      size="small"
                      variant="text"
                      :title="ui.t.addToPlanner"
                      :aria-label="ui.t.addToPlanner"
                      data-test="suggestion-add"
                    />
                  </template>
                  <v-list density="compact" data-test="suggestion-days">
                    <v-list-item
                      v-for="day in days"
                      :key="day.date"
                      :title="day.label"
                      :class="{ 'text-medium-emphasis': day.planned }"
                      data-test="suggestion-day"
                      @click="emit('add', { suggestion: s, date: day.date })"
                    />
                  </v-list>
                </v-menu>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </v-expansion-panel-text>
    </v-expansion-panel>
  </v-expansion-panels>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue'
import { plannerApi } from '../api/planner'
import type { PlanSuggestionDto } from '../api/types'
import { useUiStore } from '../stores/ui'

const props = defineProps<{
  /** Days a suggestion can be added to (today and the next 13 days); `planned` marks days with plans. */
  days: { date: string; label: string; planned: boolean }[]
  /** Changes whenever the plans change, so the suggestions are refreshed. */
  plansKey: string
}>()
const emit = defineEmits<{
  add: [value: { suggestion: PlanSuggestionDto; date: string }]
  dragStart: [suggestion: PlanSuggestionDto]
  dragEnd: []
}>()

const ui = useUiStore()
const STORAGE_KEY = 'planner.suggestionsOpen'

/** Collapsed unless the user left it open last time. */
function readOpen() {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'true'
  } catch {
    return false
  }
}

const openPanel = ref<string | undefined>(readOpen() ? 'open' : undefined)
watch(openPanel, (v) => {
  try { localStorage.setItem(STORAGE_KEY, String(v === 'open')) } catch { /* not persisted */ }
})

const suggestions = ref<PlanSuggestionDto[]>([])
const loading = ref(false)
/** The plans the current suggestions were loaded for. */
let loadedFor: string | null = null
let requestId = 0

async function load() {
  const id = ++requestId
  loadedFor = props.plansKey
  loading.value = true
  try {
    const result = await plannerApi.getSuggestions()
    if (id === requestId) suggestions.value = result
  } catch {
    if (id === requestId) suggestions.value = []
  } finally {
    if (id === requestId) loading.value = false
  }
}

// Nothing is loaded while collapsed; opening it, or changing plans while open, (re)loads.
watch([openPanel, () => props.plansKey], () => {
  if (openPanel.value === 'open' && loadedFor !== props.plansKey) load()
}, { immediate: true })

function onDragStart(event: DragEvent, s: PlanSuggestionDto) {
  if (event.dataTransfer) event.dataTransfer.effectAllowed = 'copy'
  emit('dragStart', s)
}
</script>

<style scoped>
.suggestion {
  cursor: grab;
}
.suggestion:active {
  cursor: grabbing;
}
.drag-handle {
  opacity: 0.5;
}
.min-width-0 {
  min-width: 0;
}
</style>
