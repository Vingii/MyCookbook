<template>
  <div v-if="recipe">
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">{{ recipe.name }}</h1>
      </v-col>
      <v-col v-if="canCloneToOwn" cols="auto">
        <v-btn variant="outlined" prepend-icon="mdi-content-copy" @click="cloneRecipe">{{ ui.t.cloneToMyRecipes }}</v-btn>
      </v-col>
    </v-row>

    <div class="d-flex flex-wrap ga-6 mb-4 text-body-2">
      <div v-if="recipe.category">
        <div class="text-medium-emphasis text-caption">{{ ui.t.category }}</div>
        {{ recipe.category }}
      </div>
      <div v-if="recipe.durationText">
        <div class="text-medium-emphasis text-caption">{{ ui.t.colDuration }}</div>
        {{ recipe.durationText }}
      </div>
      <div v-if="recipe.servings">
        <div class="text-medium-emphasis text-caption">{{ ui.t.servings }}</div>
        {{ recipe.servings }}
      </div>
    </div>

    <div v-if="recipe.tags.length" class="mb-4">
      <div class="d-flex align-center flex-wrap ga-2">
        <v-chip v-for="tag in recipe.tags" :key="tag">{{ tag }}</v-chip>
      </div>
    </div>

    <v-row>
      <v-col cols="12" md="5">
        <h2 class="text-h6 mb-2">{{ ui.t.ingredients }}</h2>
        <IngredientList :guid="guid" :ingredients="recipe.ingredients" :readonly="true" />
      </v-col>
      <v-col cols="12" md="7">
        <h2 class="text-h6 mb-2">{{ ui.t.steps }}</h2>
        <StepList :guid="guid" :steps="recipe.steps" :readonly="true" :highlight-words="highlightWords" :links="recipe.links" />
      </v-col>
    </v-row>
  </div>
  <div v-else-if="loading" class="text-center pa-8">
    <v-progress-circular indeterminate color="primary" />
  </div>
  <div v-else class="text-center pa-8 text-medium-emphasis">{{ ui.t.recipeNotFound }}</div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { sharedApi } from '../api/shared'
import { getHighlightWords } from '../composables/useIngredientHighlighter'
import { useUiStore } from '../stores/ui'
import { useAuthStore } from '../stores/auth'
import type { RecipeDto } from '../api/types'
import IngredientList from '../components/IngredientList.vue'
import StepList from '../components/StepList.vue'

const route = useRoute()
// Computed rather than captured: following a recipe link reuses this component instance.
const guid = computed(() => route.params.guid as string)
const router = useRouter()
const ui = useUiStore()
const auth = useAuthStore()
// This page is public, so the router guard never loaded auth. Only signed-in, non-guest users
// have a cookbook to clone into (MC-70).
const canCloneToOwn = computed(() => auth.isAuthenticated && !auth.isGuest)
const recipe = ref<RecipeDto | null>(null)
const loading = ref(true)
const highlightWords = ref(new Set<string>())

onMounted(async () => {
  await Promise.all([loadRecipe(), auth.load()])
  recordVisit()
  window.addEventListener('pageshow', onPageShow)
  document.addEventListener('visibilitychange', onVisibilityChange)
})
onUnmounted(() => {
  window.removeEventListener('pageshow', onPageShow)
  document.removeEventListener('visibilitychange', onVisibilityChange)
})
watch(guid, async () => {
  await loadRecipe()
  recordVisit()
})

/** Remembers this recipe on the signed-in user's "Shared" page (MC-71). The server ignores your own recipes. */
function recordVisit() {
  if (recipe.value && canCloneToOwn.value) sharedApi.recordRecipe(guid.value).catch(() => {})
}

// In-app browsers (Messenger, WhatsApp) often restore a link opened earlier instead of loading it again,
// which showed an outdated version of the recipe (MC-72). Refresh whenever the page is shown again.
function onPageShow(e: PageTransitionEvent) {
  if (e.persisted) refreshRecipe()
}

function onVisibilityChange() {
  if (document.visibilityState === 'visible') refreshRecipe()
}

/** Like loadRecipe, but keeps the current recipe on screen if the refresh fails (e.g. briefly offline). */
async function refreshRecipe() {
  try {
    const fresh = await recipesApi.getShared(guid.value)
    highlightWords.value = await getHighlightWords(fresh.ingredients.map((i) => i.name))
    recipe.value = fresh
  } catch {
    // Keep showing what we have.
  }
}

async function cloneRecipe() {
  const cloned = await recipesApi.clone(guid.value)
  router.push(`/recipe/${cloned.guid}`)
}

async function loadRecipe() {
  loading.value = true
  try {
    recipe.value = await recipesApi.getShared(guid.value)
    highlightWords.value = await getHighlightWords(recipe.value.ingredients.map((i) => i.name))
  } catch {
    recipe.value = null
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
:deep(mark) {
  background-color: transparent;
  font-weight: bold;
  color: rgb(var(--v-theme-primary));
}
</style>
