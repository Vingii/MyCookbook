<template>
  <div>
    <div class="d-flex align-center ga-3 mb-4">
      <h1 class="text-h4">{{ ui.t.mealPlanner }}</h1>
    </div>

    <div v-for="(week, wi) in weeks" :key="wi">
      <v-divider v-if="wi > 0" class="my-4" />

      <v-row dense>
        <v-col
            v-for="day in week"
            :key="day.date"
            cols="12"
            sm="6"
            md="3"
        >
          <v-card variant="outlined" min-height="120" class="fill-height">
            <v-card-title class="text-body-2 font-weight-bold pb-1">
              {{ day.label }}
            </v-card-title>
            <v-card-text class="pt-0">
              <div v-for="plan in getPlansForDate(day.date)" :key="plan.id" class="d-flex align-center mb-1">
                <router-link :to="`/recipe/${plan.recipeGuid}`" class="text-body-2 flex-grow-1 text-decoration-none">
                  {{ plan.recipeName }}
                </router-link>
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
          <v-autocomplete
            v-model="selectedRecipeGuid"
            :items="recipesStore.recipes"
            item-title="name"
            item-value="guid"
            :placeholder="ui.t.searchPlaceholder"
            prepend-inner-icon="mdi-magnify"
            density="compact"
            variant="outlined"
            hide-details
            autofocus
          />
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
import { ref, computed, onMounted } from 'vue'
import { usePlannerStore } from '../stores/planner'
import { useRecipesStore } from '../stores/recipes'
import { useUiStore } from '../stores/ui'
import { useReadonly } from '../composables/useReadonly'
import { plannerApi } from '../api/planner'
import type { PlannedRecipeDto } from '../api/types'

const plannerStore = usePlannerStore()
const recipesStore = useRecipesStore()
const ui = useUiStore()
const { viewingUser, shareToken, readonly } = useReadonly()
const dialogOpen = ref(false)
const dialogDate = ref('')
const selectedRecipeGuid = ref<string | null>(null)

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
    result.push({
      date,
      label: d.toLocaleDateString(ui.locale === 'cs' ? 'cs-CZ' : 'en-US', { weekday: 'long', month: 'short', day: 'numeric' }),
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
</script>
