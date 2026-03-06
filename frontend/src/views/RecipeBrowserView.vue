<template>
  <div>
    <div style="display: flex; gap: 1rem; align-items: center; margin-bottom: 1rem;">
      <h1 style="margin: 0;">Recipes</h1>
      <button @click="createRecipe">+ New Recipe</button>
    </div>

    <div style="display: flex; gap: 0.5rem; margin-bottom: 1rem; flex-wrap: wrap;">
      <input v-model="search" placeholder="Search..." @input="applyFilters" />
      <input v-model="category" placeholder="Category..." @input="applyFilters" />
      <input v-model="tag" placeholder="Tag..." @input="applyFilters" />
    </div>

    <RecipeTable :recipes="store.recipes" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRecipesStore } from '../stores/recipes'
import { recipesApi } from '../api/recipes'
import RecipeTable from '../components/RecipeTable.vue'

const store = useRecipesStore()
const router = useRouter()
const search = ref('')
const category = ref('')
const tag = ref('')

onMounted(() => store.fetchAll())

function applyFilters() {
  store.fetchAll({
    search: search.value || undefined,
    category: category.value || undefined,
    tag: tag.value || undefined,
  })
}

async function createRecipe() {
  const name = prompt('Recipe name:')
  if (!name) return
  const recipe = await recipesApi.create({ name, servings: 2 })
  router.push(`/recipe/${recipe.guid}`)
}
</script>
