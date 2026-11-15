<template>
  <v-expansion-panels v-model="openPanel" class="mb-4" data-test="suggestions">
    <v-expansion-panel value="open">
      <v-expansion-panel-title>
        <v-icon icon="mdi-lightbulb-on-outline" size="small" class="mr-2" />
        {{ ui.t.suggestions }}
      </v-expansion-panel-title>
      <v-expansion-panel-text>
        <div class="d-flex align-center flex-wrap ga-3 mb-2">
          <v-select
            v-model="selectedDate"
            :items="days"
            item-title="label"
            item-value="date"
            :label="ui.t.suggestionsFor"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 280px"
            data-test="suggestions-day"
          />
          <span class="text-caption text-medium-emphasis">{{ ui.t.suggestionsHint }}</span>
        </div>

        <div v-if="loading" class="d-flex justify-center py-4">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>
        <p v-else-if="suggestions.length === 0" class="text-medium-emphasis py-2" data-test="suggestions-empty">
          {{ ui.t.suggestionsEmpty }}
        </p>
        <v-row v-else dense>
          <v-col v-for="s in suggestions" :key="s.recipeGuid" cols="12" sm="6" md="4">
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
                  <div v-if="s.category" class="text-caption text-medium-emphasis">{{ s.category }}</div>
                </div>
                <v-btn
                  icon="mdi-plus"
                  size="small"
                  variant="text"
                  :title="ui.t.add"
                  :aria-label="ui.t.add"
                  data-test="suggestion-add"
                  @click="emit('add', { recipeGuid: s.recipeGuid, date: selectedDate })"
                />
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
  /** Days shown in the planner; `planned` marks days that already have something. */
  days: { date: string; label: string; planned: boolean }[]
  /** Changes whenever the plans change, so the suggestions are refreshed. */
  plansKey: string
  today: string
}>()
const emit = defineEmits<{
  add: [value: { recipeGuid: string; date: string }]
  dragStart: [suggestion: PlanSuggestionDto]
  dragEnd: []
}>()

const ui = useUiStore()
const STORAGE_KEY = 'planner.suggestionsOpen'

function readOpen() {
  try {
    return localStorage.getItem(STORAGE_KEY) !== 'false'
  } catch {
    return true
  }
}

const openPanel = ref<string | undefined>(readOpen() ? 'open' : undefined)
watch(openPanel, (v) => {
  try { localStorage.setItem(STORAGE_KEY, String(v === 'open')) } catch { /* not persisted */ }
})

const selectedDate = ref('')
/** True once the user picks a day themselves; until then we follow the first empty day. */
let userPicked = false
/** Set while we change the day ourselves, so the watcher doesn't count it as the user's choice. */
let settingDay = false
const suggestions = ref<PlanSuggestionDto[]>([])
const loading = ref(false)
let requestId = 0

function firstEmptyDay() {
  const upcoming = props.days.filter((d) => d.date >= props.today)
  return (upcoming.find((d) => !d.planned) ?? upcoming[0] ?? props.days[0])?.date ?? ''
}

async function load() {
  if (!selectedDate.value) return
  const id = ++requestId
  loading.value = true
  try {
    const result = await plannerApi.getSuggestions(selectedDate.value)
    if (id === requestId) suggestions.value = result
  } catch {
    if (id === requestId) suggestions.value = []
  } finally {
    if (id === requestId) loading.value = false
  }
}

watch(selectedDate, (_value, old) => {
  if (old && !settingDay) userPicked = true
  settingDay = false
  load()
})

watch(() => [props.plansKey, props.days.map((d) => d.date).join()], () => {
  const next = userPicked ? selectedDate.value : firstEmptyDay()
  if (next !== selectedDate.value) {
    settingDay = true
    selectedDate.value = next
  } else {
    load()
  }
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
