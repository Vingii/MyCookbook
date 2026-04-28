import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recipesApi } from '../api/recipes'
import { tagsApi, categoriesApi } from '../api/tags'
import type { RecipeDto } from '../api/types'

export const useRecipesStore = defineStore('recipes', () => {
  const recipes = ref<RecipeDto[]>([])
  const loading = ref(false)
  const ingredientNames = ref<string[]>([])
  const allTags = ref<string[]>([])
  const allCategories = ref<string[]>([])

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

  async function fetchAllTags() {
    allTags.value = await tagsApi.getAll()
  }

  async function fetchAllCategories() {
    allCategories.value = await categoriesApi.getAll()
  }

  return { recipes, loading, ingredientNames, allTags, allCategories, fetchAll, fetchIngredientNames, fetchAllTags, fetchAllCategories }
})
