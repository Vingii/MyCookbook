import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recipesApi } from '../api/recipes'
import type { RecipeDto } from '../api/types'

export const useRecipesStore = defineStore('recipes', () => {
  const recipes = ref<RecipeDto[]>([])
  const loading = ref(false)
  const ingredientNames = ref<string[]>([])

  async function fetchAll(params?: { search?: string; category?: string; tag?: string; user?: string; shareToken?: string }) {
    loading.value = true
    try {
      recipes.value = await recipesApi.getAll(params)
    } finally {
      loading.value = false
    }
  }

  async function fetchIngredientNames() {
    ingredientNames.value = await recipesApi.getAllIngredientNames()
  }

  return { recipes, loading, ingredientNames, fetchAll, fetchIngredientNames }
})
