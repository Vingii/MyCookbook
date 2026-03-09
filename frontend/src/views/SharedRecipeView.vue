<template>
  <div v-if="recipe">
    <v-row align="center" class="mb-4">
      <v-col>
        <h1 class="text-h4">{{ recipe.name }}</h1>
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
        <StepList :guid="guid" :steps="recipe.steps" :readonly="true" :highlight-words="highlightWords" />
      </v-col>
    </v-row>
  </div>
  <div v-else-if="loading" class="text-center pa-8">
    <v-progress-circular indeterminate color="primary" />
  </div>
  <div v-else class="text-center pa-8 text-medium-emphasis">{{ ui.t.recipeNotFound }}</div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { getHighlightWords } from '../composables/useIngredientHighlighter'
import { useUiStore } from '../stores/ui'
import type { RecipeDto } from '../api/types'
import IngredientList from '../components/IngredientList.vue'
import StepList from '../components/StepList.vue'

const route = useRoute()
const guid = route.params.guid as string
const ui = useUiStore()
const recipe = ref<RecipeDto | null>(null)
const loading = ref(true)
const highlightWords = ref(new Set<string>())

onMounted(async () => {
  try {
    recipe.value = await recipesApi.getShared(guid)
    highlightWords.value = await getHighlightWords(recipe.value.ingredients.map((i) => i.name))
  } catch {
    recipe.value = null
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
:deep(mark) {
  background-color: transparent;
  font-weight: bold;
  color: rgb(var(--v-theme-primary));
}
</style>
