<template>
  <div>
    <div style="display: flex; gap: 1rem; align-items: center; margin-bottom: 1rem;">
      <h1 style="margin: 0;">Meal Planner</h1>
      <button @click="prevWeek">← Prev</button>
      <button @click="nextWeek">Next →</button>
    </div>

    <div style="display: grid; grid-template-columns: repeat(7, 1fr); gap: 0.5rem;">
      <div v-for="day in days" :key="day.date" style="border: 1px solid #ccc; padding: 0.5rem; min-height: 100px;">
        <div style="font-weight: bold; margin-bottom: 0.5rem;">{{ day.label }}</div>
        <div v-for="plan in getPlansForDate(day.date)" :key="plan.id" style="font-size: 0.9em; margin-bottom: 4px;">
          <a :href="`/recipe/${plan.recipeGuid}`" @click.prevent="$router.push(`/recipe/${plan.recipeGuid}`)">
            {{ plan.recipeName }}
          </a>
          <span v-if="plan.fromFridge"> 🧊</span>
          <button v-if="!readonly" @click="deletePlan(plan.id)" style="margin-left: 4px; font-size: 0.8em;">×</button>
        </div>
        <button v-if="!readonly" @click="addPlan(day.date)" style="font-size: 0.8em;">+ Add</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { usePlannerStore } from '../stores/planner'
import { useRecipesStore } from '../stores/recipes'
import { useReadonly } from '../composables/useReadonly'
import { plannerApi } from '../api/planner'

const plannerStore = usePlannerStore()
const recipesStore = useRecipesStore()
const { viewingUser, shareToken, readonly } = useReadonly()
const weekOffset = ref(0)

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
      label: d.toLocaleDateString('en', { weekday: 'short', month: 'short', day: 'numeric' }),
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

async function addPlan(date: string) {
  const recipes = recipesStore.recipes
  if (!recipes.length) return
  const name = prompt('Recipe name (partial match):')
  if (!name) return
  const match = recipes.find((r) => r.name.toLowerCase().includes(name.toLowerCase()))
  if (!match) { alert('Recipe not found'); return }
  await plannerApi.create({ recipeGuid: match.guid, date, fromFridge: false })
  fetchPlans()
}

async function deletePlan(id: number) {
  await plannerApi.delete(id)
  fetchPlans()
}
</script>
