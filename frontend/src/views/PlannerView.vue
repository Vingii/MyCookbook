<template>
  <div>
    <div class="d-flex align-center ga-3 mb-4">
      <h1 class="text-h4">{{ ui.t.mealPlanner }}</h1>
    </div>

    <div v-for="(week, wi) in weeks" :key="wi">
      <div v-if="wi > 0" class="d-flex align-center ga-3 my-5">
        <v-divider />
        <span class="text-caption text-medium-emphasis text-no-wrap font-weight-bold text-uppercase px-2">
          {{ ui.t.week }} {{ wi + 1 }}
        </span>
        <v-divider />
      </div>

      <v-row dense>
        <v-col
            v-for="day in week"
            :key="day.date"
            cols="12"
            sm="6"
            lg="3"
        >
          <v-card
              variant="outlined"
              :color="day.isToday ? 'primary' : undefined"
              min-height="120"
              class="fill-height"
              :class="{ 'drop-target': !readonly && dragOverDate === day.date, 'day-weekend': day.isWeekend, 'day-weekday': !day.isWeekend }"
              @dragover.prevent="onDragOver(day.date)"
              @dragleave="onDragLeave(day.date)"
              @drop.prevent="onDrop(day.date)"
          >
            <v-card-title class="text-body-2 font-weight-bold pb-1 d-flex align-center ga-1">
              <v-icon v-if="day.isToday" icon="mdi-calendar-today" size="x-small" />
              {{ day.label }}
            </v-card-title>
            <v-card-text class="pt-0">
              <div
                  v-for="plan in getPlansForDate(day.date)"
                  :key="plan.id"
                  class="d-flex align-center mb-1"
                  :draggable="!readonly"
                  @dragstart="onDragStart($event, plan)"
                  @dragend="onDragEnd"
                  :class="{ 'drag-source': draggingId === plan.id }"
              >
                <v-icon
                    v-if="!readonly"
                    icon="mdi-drag"
                    size="x-small"
                    class="drag-handle mr-1 text-medium-emphasis"
                />
                <v-btn
                    :to="`/recipe/${plan.recipeGuid}`"
                    variant="text"
                    size="small"
                    color="default"
                    class="text-none flex-grow-1 justify-start px-1"
                    density="compact"
                >{{ plan.recipeName }}</v-btn>
                <v-btn
                    :icon="plan.fromFridge ? 'mdi-fridge' : 'mdi-silverware-fork-knife'"
                    size="x-small"
                    variant="text"
                    :color="plan.fromFridge ? 'info' : undefined"
                    :title="plan.fromFridge ? 'From fridge' : 'Cook'"
                    @click="toggleFromFridge(plan)"
                />
                <v-btn
                    v-if="!readonly"
                    icon="mdi-content-copy"
                    size="x-small"
                    variant="text"
                    :title="ui.t.cloneToNextDay"
                    @click="cloneToNextDay(plan)"
                />
                <v-btn
                    v-if="!readonly"
                    icon="mdi-close"
                    size="x-small"
                    variant="text"
                    @click="deletePlan(plan.id)"
                />
              </div>
              <v-btn
                  v-if="!readonly"
                  size="small"
                  variant="text"
                  prepend-icon="mdi-plus"
                  @click="openAddDialog(day.date)"
              >{{ ui.t.add }}</v-btn>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </div>

    <!-- Add recipe dialog -->
    <v-dialog v-model="dialogOpen" max-width="420">
      <v-card>
        <v-card-title>{{ ui.t.addToPlanner }}</v-card-title>
        <v-card-text>
          <div @keydown.enter.capture="onAutocompleteWrapperEnter">
            <v-autocomplete
              v-model="selectedRecipeGuid"
              v-model:search="autocompleteSearch"
              v-model:menu="autocompleteMenuOpen"
              :items="filteredRecipes"
              item-title="name"
              item-value="guid"
              :placeholder="ui.t.searchPlaceholder"
              prepend-inner-icon="mdi-magnify"
              density="compact"
              variant="outlined"
              hide-details
              autofocus
              :custom-filter="noFilter"
            />
          </div>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="dialogOpen = false">{{ ui.t.cancel }}</v-btn>
          <v-btn color="primary" :disabled="!selectedRecipeGuid" @click="confirmAdd">{{ ui.t.add }}</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { usePlannerStore } from '../stores/planner'
import { useRecipesStore } from '../stores/recipes'
import { useUiStore } from '../stores/ui'
import { useReadonly } from '../composables/useReadonly'
import { plannerApi } from '../api/planner'
import { recipesApi } from '../api/recipes'
import type { RecipeDto, PlannedRecipeDto } from '../api/types'

const plannerStore = usePlannerStore()
const recipesStore = useRecipesStore()
const ui = useUiStore()
const { viewingUser, shareToken, readonly } = useReadonly()
const dialogOpen = ref(false)
const dialogDate = ref('')
const selectedRecipeGuid = ref<string | null>(null)
const autocompleteSearch = ref('')
const filteredRecipes = ref<RecipeDto[]>([])
let searchDebounce: ReturnType<typeof setTimeout> | null = null
const noFilter = () => true
const autocompleteMenuOpen = ref(false)

function onAutocompleteWrapperEnter(e: KeyboardEvent) {
  if (!autocompleteMenuOpen.value && selectedRecipeGuid.value) {
    e.stopPropagation()
    e.preventDefault()
    confirmAdd()
  }
}

watch(dialogOpen, (open) => {
  if (open) {
    autocompleteSearch.value = ''
    filteredRecipes.value = recipesStore.recipes
  }
})

watch(autocompleteSearch, (query) => {
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(async () => {
    filteredRecipes.value = await recipesApi.getAll({
      search: query || undefined,
      user: viewingUser.value || undefined,
      shareToken: shareToken.value,
    })
  }, 200)
})

const days = computed(() => {
  const result = []
  const now = new Date()
  const monday = new Date(now)
  const dow = now.getDay() || 7
  monday.setDate(now.getDate() - dow + 1)
  for (let i = 0; i < 21; i++) {
    const d = new Date(monday)
    d.setDate(monday.getDate() + i)
    const date = d.toISOString().substring(0, 10)
    const dow2 = d.getDay()
    result.push({
      date,
      label: (() => { const s = d.toLocaleDateString(ui.locale === 'cs' ? 'cs-CZ' : 'en-US', { weekday: 'long', month: 'short', day: 'numeric' }); return s.charAt(0).toUpperCase() + s.slice(1) })(),
      isWeekend: dow2 === 0 || dow2 === 6,
      isToday: date === now.toISOString().substring(0, 10),
    })
  }
  return result
})

const weeks = computed(() => {
  const result = []
  for (let w = 0; w < 3; w++) {
    result.push(days.value.slice(w * 7, w * 7 + 7))
  }
  return result
})

function fetchPlans() {
  const from = days.value[0]!.date
  const to = days.value[20]!.date
  plannerStore.fetchRange(from, to, viewingUser.value || undefined, shareToken.value)
}

onMounted(() => {
  fetchPlans()
  recipesStore.fetchAll({ user: viewingUser.value || undefined, shareToken: shareToken.value })
})

function getPlansForDate(date: string) {
  return plannerStore.planned.filter((p) => p.date === date)
}

function openAddDialog(date: string) {
  dialogDate.value = date
  selectedRecipeGuid.value = null
  dialogOpen.value = true
}

async function confirmAdd() {
  if (!selectedRecipeGuid.value) return
  await plannerApi.create({ recipeGuid: selectedRecipeGuid.value, date: dialogDate.value, fromFridge: false })
  dialogOpen.value = false
  fetchPlans()
}

async function deletePlan(id: number) {
  await plannerApi.delete(id)
  fetchPlans()
}

async function toggleFromFridge(plan: PlannedRecipeDto) {
  await plannerApi.update(plan.id, { date: plan.date, fromFridge: !plan.fromFridge })
  fetchPlans()
}

async function cloneToNextDay(plan: PlannedRecipeDto) {
  const d = new Date(plan.date)
  d.setDate(d.getDate() + 1)
  const nextDate = d.toISOString().substring(0, 10)
  await plannerApi.create({ recipeGuid: plan.recipeGuid, date: nextDate, fromFridge: !plan.fromFridge })
  fetchPlans()
}

// Drag and drop
const draggingId = ref<number | null>(null)
const draggingPlan = ref<PlannedRecipeDto | null>(null)
const dragOverDate = ref<string | null>(null)

function onDragStart(event: DragEvent, plan: PlannedRecipeDto) {
  draggingId.value = plan.id
  draggingPlan.value = plan
  event.dataTransfer!.effectAllowed = 'move'
}

function onDragEnd() {
  draggingId.value = null
  draggingPlan.value = null
  dragOverDate.value = null
}

function onDragOver(date: string) {
  dragOverDate.value = date
}

function onDragLeave(date: string) {
  if (dragOverDate.value === date) dragOverDate.value = null
}

async function onDrop(date: string) {
  const plan = draggingPlan.value
  dragOverDate.value = null
  if (!plan || plan.date === date) return
  await plannerApi.update(plan.id, { date, fromFridge: plan.fromFridge })
  fetchPlans()
}
</script>

<style scoped>
.drag-handle {
  cursor: grab;
  opacity: 0.4;
}
.drag-handle:active {
  cursor: grabbing;
}
.drag-source {
  opacity: 0.4;
}
.day-weekday {
  background: rgba(128, 128, 128, 0.07);
}
.day-weekend {
  background: rgba(128, 128, 128, 0.15);
}
.drop-target {
  outline: 2px dashed currentColor;
  opacity: 0.8;
  outline-offset: -2px;
}
</style>
