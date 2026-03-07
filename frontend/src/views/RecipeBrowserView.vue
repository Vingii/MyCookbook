<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">Recipes</h1>
      </v-col>
      <v-col cols="auto">
        <v-btn v-if="!readonly" color="primary" prepend-icon="mdi-plus" @click="createRecipe">New Recipe</v-btn>
      </v-col>
    </v-row>

    <v-row class="mb-4">
      <v-col cols="12" sm="4">
        <v-text-field
          v-model="search"
          placeholder="Search..."
          prepend-inner-icon="mdi-magnify"
          density="compact"
          hide-details
          variant="outlined"
          @input="applyFilters"
        />
      </v-col>
      <v-col cols="12" sm="4">
        <v-text-field
          v-model="category"
          placeholder="Category..."
          prepend-inner-icon="mdi-tag-outline"
          density="compact"
          hide-details
          variant="outlined"
          @input="applyFilters"
        />
      </v-col>
      <v-col cols="12" sm="4">
        <v-text-field
          v-model="tag"
          placeholder="Tag..."
          prepend-inner-icon="mdi-label-outline"
          density="compact"
          hide-details
          variant="outlined"
          @input="applyFilters"
        />
      </v-col>
    </v-row>

    <RecipeTable :recipes="store.recipes" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRecipesStore } from '../stores/recipes'
import { useReadonly } from '../composables/useReadonly'
import { recipesApi } from '../api/recipes'
import RecipeTable from '../components/RecipeTable.vue'

const store = useRecipesStore()
const router = useRouter()
const { viewingUser, shareToken, readonly } = useReadonly()
const search = ref('')
const category = ref('')
const tag = ref('')

onMounted(() => store.fetchAll({ user: viewingUser.value || undefined, shareToken: shareToken.value }))

function applyFilters() {
  store.fetchAll({
    search: search.value || undefined,
    category: category.value || undefined,
    tag: tag.value || undefined,
    user: viewingUser.value || undefined,
    shareToken: shareToken.value,
  })
}

async function createRecipe() {
  const name = prompt('Recipe name:')
  if (!name) return
  const recipe = await recipesApi.create({ name, servings: 2 })
  router.push(`/recipe/${recipe.guid}`)
}
</script>
