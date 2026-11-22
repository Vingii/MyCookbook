<template>
  <div>
    <v-row align="center" class="mb-4">
      <v-col>
        <!-- With several cookbooks (MC-80) the title switches between them. -->
        <v-menu v-if="!readonly && cookbooks.hasSeveral">
          <template #activator="{ props: menuProps }">
            <h1 v-bind="menuProps" class="text-h4 cookbook-switcher" data-test="cookbook-switcher" role="button" tabindex="0">
              {{ cookbooks.displayName(cookbooks.active) }}
              <v-icon icon="mdi-chevron-down" size="small" />
            </h1>
          </template>
          <v-list density="compact">
            <v-list-item
              v-for="c in cookbooks.cookbooks"
              :key="c.id"
              :active="c.id === cookbooks.active?.id"
              :title="cookbooks.displayName(c)"
              :subtitle="cookbooks.recipeCountLabel(c)"
              :data-test="`cookbook-${c.id}`"
              @click="switchCookbook(c.id)"
            />
            <v-divider class="my-1" />
            <v-list-item prepend-icon="mdi-cog" :title="ui.t.manageCookbooks" to="/settings#cookbooks" />
          </v-list>
        </v-menu>
        <h1 v-else class="text-h4">{{ ui.t.recipes }}</h1>
      </v-col>
      <v-col cols="auto" class="d-flex ga-2">
        <v-btn
          v-if="!readonly && importEnabled"
          variant="outlined"
          color="primary"
          prepend-icon="mdi-link-variant"
          @click="importOpen = true"
        >
          {{ ui.t.importFromLink }}
        </v-btn>
        <v-btn v-if="!readonly" color="primary" prepend-icon="mdi-plus" @click="createRecipe">
          {{ ui.t.newRecipe }}
        </v-btn>
      </v-col>
    </v-row>

    <RecipeImportDialog v-model="importOpen" :cookbook-id="activeCookbookId" @created="(guid) => router.push(`/recipe/${guid}`)" />

    <v-row class="mb-4" :dense="xs">
      <v-col cols="12" sm="6" md="3" class="d-flex align-center ga-2">
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
        <!-- On phones the other filters would push the table off-screen, so they collapse behind this toggle. -->
        <v-btn
          v-if="xs"
          icon
          variant="text"
          :active="filtersOpen"
          :aria-label="ui.t.filters"
          :aria-expanded="filtersOpen"
          @click="filtersOpen = !filtersOpen"
        >
          <v-badge :model-value="activeFilterCount > 0" :content="activeFilterCount" color="primary">
            <v-icon icon="mdi-filter-variant" />
          </v-badge>
        </v-btn>
      </v-col>
      <template v-if="!xs || filtersOpen">
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
      </template>
    </v-row>

    <RecipeTable :recipes="filteredRecipes" @clone="handleClone" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watchEffect } from 'vue'
import { useRouter } from 'vue-router'
import { useDisplay } from 'vuetify'
import { useRecipesStore } from '../stores/recipes'
import { useCookbooksStore } from '../stores/cookbooks'
import { useUiStore } from '../stores/ui'
import { useReadonly } from '../composables/useReadonly'
import { recipesApi } from '../api/recipes'
import RecipeTable from '../components/RecipeTable.vue'
import RecipeImportDialog from '../components/RecipeImportDialog.vue'
import { recipeImportApi } from '../api/recipeImport'

const store = useRecipesStore()
const cookbooks = useCookbooksStore()
const ui = useUiStore()
const router = useRouter()
const { readonly } = useReadonly()
watchEffect(() => { document.title = cookbooks.hasSeveral && !readonly.value ? cookbooks.displayName(cookbooks.active) : ui.t.recipes })
const search = ref('')
const category = ref<string | null>(null)
const tag = ref<string | null>(null)
const selectedIngredients = ref<string[]>([])
// AI import from a link (MC-69) is offered only when the server has an AI configured.
const importEnabled = ref(false)
const importOpen = ref(false)
const { xs } = useDisplay()
const filtersOpen = ref(false)
// Only filter when there is a choice: a single cookbook shows every recipe, as before cookbooks existed.
const activeCookbookId = computed(() => (!readonly.value && cookbooks.hasSeveral ? cookbooks.active?.id : undefined))
const activeFilterCount = computed(
  () => (category.value ? 1 : 0) + (tag.value ? 1 : 0) + (selectedIngredients.value.length > 0 ? 1 : 0)
)

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

onMounted(async () => {
  store.fetchIngredientNames()
  if (!readonly.value) {
    recipeImportApi.isEnabled().then((enabled) => { importEnabled.value = enabled }).catch(() => {})
    // A read-only session (e.g. the Home Assistant panel) just shows every recipe.
    try {
      await cookbooks.fetchAll()
    } catch {
      // Without cookbooks the list just shows every recipe.
    }
  }
  applyFilters()
})

function applyFilters() {
  store.fetchAll({
    search: search.value || undefined,
    category: category.value || undefined,
    tag: tag.value || undefined,
    cookbook: activeCookbookId.value,
  })
}

function switchCookbook(id: number) {
  cookbooks.setActive(id)
  applyFilters()
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
  const recipe = await recipesApi.create({ name, servings: 2, cookbookId: activeCookbookId.value })
  router.push(`/recipe/${recipe.guid}`)
}

async function handleClone(guid: string) {
  await recipesApi.clone(guid)
  applyFilters()
}
</script>

<style scoped>
.cookbook-switcher {
  cursor: pointer;
  display: inline-flex;
  align-items: center;
  gap: 4px;
}
</style>
