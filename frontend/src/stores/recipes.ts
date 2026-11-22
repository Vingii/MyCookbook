import { defineStore } from 'pinia'
import { ref } from 'vue'
import { recipesApi, type RecipeListParams } from '../api/recipes'
import { tagsApi, categoriesApi } from '../api/tags'
import type { RecipeDto } from '../api/types'

export const useRecipesStore = defineStore('recipes', () => {
  const recipes = ref<RecipeDto[]>([])
  const loading = ref(false)
  const ingredientNames = ref<string[]>([])
  const allTags = ref<string[]>([])
  const allCategories = ref<string[]>([])
  const allRecipeNames = ref<string[]>([])

  async function fetchAll(params?: RecipeListParams) {
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

  async function fetchAllRecipeNames() {
    allRecipeNames.value = await recipesApi.getAllNames()
  }

  return {
    recipes, loading, ingredientNames, allTags, allCategories, allRecipeNames,
    fetchAll, fetchIngredientNames, fetchAllTags, fetchAllCategories, fetchAllRecipeNames,
  }
})
