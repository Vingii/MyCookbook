import { defineStore } from 'pinia'
import { ref } from 'vue'
import { plannerApi } from '../api/planner'
import type { PlannedRecipeDto } from '../api/types'

export const usePlannerStore = defineStore('planner', () => {
  const planned = ref<PlannedRecipeDto[]>([])
  const loading = ref(false)

  async function fetchRange(from: string, to: string) {
    loading.value = true
    try {
      planned.value = await plannerApi.getAll(from, to)
    } finally {
      loading.value = false
    }
  }

  return { planned, loading, fetchRange }
})
