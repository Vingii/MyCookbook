<template>
  <div v-if="recipe">
    <v-row align="center" class="mb-4">
      <v-col>
        <div class="d-flex align-center ga-2">
          <v-text-field
            v-if="!readonly && editingMeta"
            v-model="recipe.name"
            variant="plain"
            density="compact"
            hide-details
            class="text-h4"
            @blur="saveRecipe"
            @keyup.enter="saveRecipe"
          />
          <h1 v-else class="text-h4">{{ recipe.name }}</h1>
          <v-btn
            v-if="!readonly && !editingMeta"
            icon="mdi-pencil"
            size="small"
            variant="text"
            @click="editingMeta = true"
          />
        </div>
      </v-col>
      <v-col v-if="!readonly" cols="auto" class="d-flex ga-2">
        <v-btn v-if="editingMeta" icon="mdi-check" size="small" variant="text" color="primary" @click="editingMeta = false" />
        <v-btn variant="outlined" prepend-icon="mdi-share-variant" @click="shareRecipe">{{ ui.t.shareRecipe }}</v-btn>
        <v-btn variant="outlined" prepend-icon="mdi-content-copy" @click="cloneRecipe">{{ ui.t.clone }}</v-btn>
        <v-btn color="error" variant="outlined" prepend-icon="mdi-delete" @click="deleteRecipe">{{ ui.t.delete }}</v-btn>
      </v-col>
    </v-row>

    <v-row v-if="!readonly && editingMeta" class="mb-4" align="center">
      <v-col cols="12" sm="4">
        <v-text-field
          v-model="recipe.category"
          :label="ui.t.category"
          density="compact"
          variant="outlined"
          hide-details
          @blur="saveRecipe"
        />
      </v-col>
      <v-col cols="6" sm="4">
        <v-text-field
          v-model.number="recipe.duration"
          :label="ui.t.durationMin"
          type="number"
          density="compact"
          variant="outlined"
          hide-details
          @blur="saveRecipe"
        />
      </v-col>
      <v-col cols="6" sm="4">
        <v-text-field
          v-model.number="recipe.servings"
          :label="ui.t.servings"
          type="number"
          density="compact"
          variant="outlined"
          hide-details
          @blur="saveRecipe"
        />
      </v-col>
    </v-row>
    <div v-else class="d-flex flex-wrap ga-6 mb-4 text-body-2">
      <div>
        <div class="text-medium-emphasis text-caption">{{ ui.t.category }}</div>
        {{ recipe.category || '—' }}
      </div>
      <div>
        <div class="text-medium-emphasis text-caption">{{ ui.t.durationMin }}</div>
        {{ recipe.durationText || '—' }}
      </div>
      <div>
        <div class="text-medium-emphasis text-caption">{{ ui.t.servings }}</div>
        {{ recipe.servings ?? '—' }}
      </div>
    </div>

    <div class="mb-4">
      <div class="d-flex align-center flex-wrap ga-2">
        <v-chip
          v-for="tag in recipe.tags"
          :key="tag"
          :closable="!readonly"
          @click:close="removeTag(tag)"
        >{{ tag }}</v-chip>
        <v-text-field
          v-if="!readonly"
          v-model="newTag"
          :placeholder="ui.t.addTagPlaceholder"
          density="compact"
          hide-details
          variant="outlined"
          style="max-width: 150px;"
          @keyup.enter="addTag"
        />
      </div>
    </div>

    <StepTimeline :steps="recipe.steps" class="mb-4" />

    <v-row>
      <v-col cols="12" md="5">
        <h2 class="text-h6 mb-2">{{ ui.t.ingredients }}</h2>
        <IngredientList :guid="guid" :ingredients="recipe.ingredients" :readonly="readonly" @refresh="loadRecipe" />
      </v-col>
      <v-col cols="12" md="7">
        <h2 class="text-h6 mb-2">{{ ui.t.steps }}</h2>
        <StepList :guid="guid" :steps="recipe.steps" :readonly="readonly" :highlight-words="highlightWords" @refresh="loadRecipe" />
      </v-col>
    </v-row>

    <div v-if="!readonly" class="d-flex justify-center mt-6">
      <v-btn color="success" size="large" prepend-icon="mdi-check-circle" @click="finishCooking">
        {{ ui.t.finishCooking }}
      </v-btn>
    </div>

    <v-snackbar v-model="snackbar" :timeout="2000" location="bottom">
      {{ ui.t.linkCopied }}
    </v-snackbar>
  </div>
  <div v-else-if="loading" class="text-center pa-8">
    <v-progress-circular indeterminate color="primary" />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { useReadonly } from '../composables/useReadonly'
import { useUiStore } from '../stores/ui'
import { getHighlightWords } from '../composables/useIngredientHighlighter'
import type { RecipeDto } from '../api/types'
import IngredientList from '../components/IngredientList.vue'
import StepList from '../components/StepList.vue'
import StepTimeline from '../components/StepTimeline.vue'

const route = useRoute()
const router = useRouter()
const guid = route.params.guid as string
const { viewingUser, shareToken, readonly } = useReadonly()
const ui = useUiStore()
const recipe = ref<RecipeDto | null>(null)
const loading = ref(true)
const newTag = ref('')
const highlightWords = ref(new Set<string>())
const editingMeta = ref(false)
const snackbar = ref(false)

onMounted(loadRecipe)

async function loadRecipe() {
  loading.value = true
  try {
    recipe.value = await recipesApi.getById(guid, { user: viewingUser.value || undefined, shareToken: shareToken.value })
    highlightWords.value = await getHighlightWords(recipe.value.ingredients.map((i) => i.name))
  } finally {
    loading.value = false
  }
}

async function saveRecipe() {
  if (!recipe.value) return
  await recipesApi.update(guid, {
    name: recipe.value.name,
    category: recipe.value.category,
    duration: recipe.value.duration,
    servings: recipe.value.servings,
  })
}

async function cloneRecipe() {
  const cloned = await recipesApi.clone(guid)
  router.push(`/recipe/${cloned.guid}`)
}

async function finishCooking() {
  await recipesApi.markCooked(guid)
  router.push('/')
}

async function deleteRecipe() {
  if (!confirm(`${ui.t.delete} "${recipe.value?.name}"?`)) return
  await recipesApi.delete(guid)
  router.push('/')
}

async function addTag() {
  if (!newTag.value.trim()) return
  await recipesApi.addTag(guid, newTag.value.trim())
  newTag.value = ''
  await loadRecipe()
}

async function removeTag(name: string) {
  await recipesApi.deleteTag(guid, name)
  await loadRecipe()
}

async function shareRecipe() {
  const url = `${window.location.origin}/recipe/shared/${guid}`
  await navigator.clipboard.writeText(url)
  snackbar.value = true
}
</script>

<style scoped>
:deep(mark) {
  background-color: transparent;
  font-weight: bold;
  color: rgb(var(--v-theme-primary));
}
</style>
