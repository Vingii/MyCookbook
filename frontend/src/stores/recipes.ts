import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recipesApi } from '../api/recipes'
import type { RecipeDto } from '../api/types'

export const useRecipesStore = defineStore('recipes', () => {
  const recipes = ref<RecipeDto[]>([])
  const loading = ref(false)

  async function fetchAll(params?: { search?: string; category?: string; tag?: string }) {
    loading.value = true
    try {
      recipes.value = await recipesApi.getAll(params)
    } finally {
      loading.value = false
    }
  }

  return { recipes, loading, fetchAll }
})
