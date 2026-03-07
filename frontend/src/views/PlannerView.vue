<template>
  <div>
    <div class="d-flex align-center ga-3 mb-4">
      <h1 class="text-h4">{{ ui.t.mealPlanner }}</h1>
      <v-btn icon="mdi-chevron-left" variant="text" @click="prevWeek" />
      <v-btn icon="mdi-chevron-right" variant="text" @click="nextWeek" />
    </div>

    <v-row>
      <v-col v-for="day in days" :key="day.date" cols="12" sm="6" md="auto" style="min-width: 140px; flex: 1;">
        <v-card variant="outlined" min-height="120">
          <v-card-title class="text-body-2 font-weight-bold pb-1">{{ day.label }}</v-card-title>
          <v-card-text class="pt-0">
            <div v-for="plan in getPlansForDate(day.date)" :key="plan.id" class="d-flex align-center mb-1">
              <router-link :to="`/recipe/${plan.recipeGuid}`" class="text-body-2 flex-grow-1 text-decoration-none">
                {{ plan.recipeName }}
              </router-link>
              <span v-if="plan.fromFridge" class="ml-1">🧊</span>
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

const plannerStore = usePlannerStore()
const recipesStore = useRecipesStore()
const ui = useUiStore()
const { viewingUser, shareToken, readonly } = useReadonly()
const weekOffset = ref(0)
const dialogOpen = ref(false)
const dialogDate = ref('')
const selectedRecipeGuid = ref<string | null>(null)

const days = computed(() => {
  const result = []
  const now = new Date()
  const monday = new Date(now)
  const dow = now.getDay() || 7
  monday.setDate(now.getDate() - dow + 1 + weekOffset.value * 7)
  for (let i = 0; i < 7; i++) {
    const d = new Date(monday)
    d.setDate(monday.getDate() + i)
    const date = d.toISOString().substring(0, 10)
    result.push({
      date,
      label: d.toLocaleDateString(ui.locale === 'cs' ? 'cs' : 'en', { weekday: 'short', month: 'short', day: 'numeric' }),
    })
  }
  return result
})

function fetchPlans() {
  const from = days.value[0]!.date
  const to = days.value[6]!.date
  plannerStore.fetchRange(from, to, viewingUser.value || undefined, shareToken.value)
}

onMounted(() => {
  fetchPlans()
  recipesStore.fetchAll({ user: viewingUser.value || undefined, shareToken: shareToken.value })
})

function getPlansForDate(date: string) {
  return plannerStore.planned.filter((p) => p.date === date)
}

function prevWeek() {
  weekOffset.value--
  fetchPlans()
}

function nextWeek() {
  weekOffset.value++
  fetchPlans()
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
</script>
