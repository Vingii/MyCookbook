<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">{{ ui.t.recipes }}</h1>
      </v-col>
      <v-col cols="auto">
        <v-btn v-if="!readonly" color="primary" prepend-icon="mdi-plus" @click="createRecipe">
          {{ ui.t.newRecipe }}
        </v-btn>
      </v-col>
    </v-row>

    <v-row class="mb-4">
      <v-col cols="12" sm="6" md="3">
        <v-text-field
          v-model="search"
          :placeholder="ui.t.searchPlaceholder"
          prepend-inner-icon="mdi-magnify"
          density="compact"
          hide-details
          variant="outlined"
          clearable
          @input="onSearchInput"
          @click:clear="onSearchClear"
        />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-autocomplete
          v-model="category"
          :items="categories"
          :placeholder="ui.t.categoryPlaceholder"
          prepend-inner-icon="mdi-tag-outline"
          density="compact"
          hide-details
          variant="outlined"
          clearable
          @update:model-value="applyFilters"
        />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-autocomplete
          v-model="tag"
          :items="tags"
          :placeholder="ui.t.tagPlaceholder"
          prepend-inner-icon="mdi-label-outline"
          density="compact"
          hide-details
          variant="outlined"
          clearable
          @update:model-value="applyFilters"
        />
      </v-col>
      <v-col cols="12" sm="6" md="3">
        <v-autocomplete
          v-model="selectedIngredients"
          :items="store.ingredientNames"
          :placeholder="ui.t.ingredientFilterPlaceholder"
          prepend-inner-icon="mdi-food-apple-outline"
          density="compact"
          hide-details
          variant="outlined"
          multiple
          clearable
          chips
          closable-chips
        />
      </v-col>
    </v-row>

    <RecipeTable :recipes="filteredRecipes" @clone="handleClone" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useRecipesStore } from '../stores/recipes'
import { useUiStore } from '../stores/ui'
import { useReadonly } from '../composables/useReadonly'
import { recipesApi } from '../api/recipes'
import RecipeTable from '../components/RecipeTable.vue'

const store = useRecipesStore()
const ui = useUiStore()
const router = useRouter()
const { viewingUser, shareToken, readonly } = useReadonly()
const search = ref('')
const category = ref<string | null>(null)
const tag = ref<string | null>(null)
const selectedIngredients = ref<string[]>([])

const categories = computed(() =>
  [...new Set(store.recipes.map((r) => r.category).filter(Boolean))].sort() as string[]
)
const tags = computed(() =>
  [...new Set(store.recipes.flatMap((r) => r.tags ?? []))].sort()
)
const filteredRecipes = computed(() => {
  if (selectedIngredients.value.length === 0) return store.recipes
  return store.recipes.filter((r) =>
    selectedIngredients.value.some((ing) =>
      r.ingredients.some((i) => i.name.toLowerCase() === ing.toLowerCase())
    )
  )
})

onMounted(() => {
  store.fetchAll({ user: viewingUser.value || undefined, shareToken: shareToken.value })
  store.fetchIngredientNames()
})

function applyFilters() {
  store.fetchAll({
    search: search.value || undefined,
    category: category.value || undefined,
    tag: tag.value || undefined,
    user: viewingUser.value || undefined,
    shareToken: shareToken.value,
  })
}

let searchDebounceTimer: ReturnType<typeof setTimeout> | null = null

function onSearchInput() {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  searchDebounceTimer = setTimeout(applyFilters, 200)
}

function onSearchClear() {
  if (searchDebounceTimer) clearTimeout(searchDebounceTimer)
  search.value = ''
  applyFilters()
}

async function createRecipe() {
  const name = prompt(`${ui.t.colName}:`)
  if (!name) return
  const recipe = await recipesApi.create({ name, servings: 2 })
  router.push(`/recipe/${recipe.guid}`)
}

async function handleClone(guid: string) {
  await recipesApi.clone(guid)
  applyFilters()
}
</script>
