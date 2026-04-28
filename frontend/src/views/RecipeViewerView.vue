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
        <v-btn v-if="editingMeta" icon="mdi-check" size="small" variant="text" color="primary" @click="editingMeta = false; loadRecipe()" />
        <v-btn variant="outlined" prepend-icon="mdi-share-variant" @click="shareRecipe">{{ ui.t.shareRecipe }}</v-btn>
        <v-btn variant="outlined" prepend-icon="mdi-content-copy" @click="cloneRecipe">{{ ui.t.clone }}</v-btn>
        <v-btn color="error" variant="outlined" prepend-icon="mdi-delete" @click="deleteRecipe">{{ ui.t.delete }}</v-btn>
      </v-col>
    </v-row>

    <v-row v-if="!readonly && editingMeta" class="mb-4" align="center">
      <v-col cols="12" sm="4">
        <v-combobox
          v-model="recipe.category"
          :items="store.allCategories"
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
        <div class="text-medium-emphasis text-caption">{{ ui.t.colDuration }}</div>
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
        <v-combobox
          v-if="!readonly && editingMeta"
          v-model="newTag"
          v-model:menu="tagMenuOpen"
          :items="availableTags"
          :label="ui.t.addTagPlaceholder"
          density="compact"
          hide-details
          variant="outlined"
          style="max-width: 200px;"
          @keyup.enter="addTag"
        >
          <template #item="{ props }">
            <v-list-item v-bind="{ ...props, onClick: () => onDropdownItemClick(props.value as string) }" />
          </template>
        </v-combobox>
      </div>
    </div>

    <v-row>
      <v-col cols="12" lg="5">
        <h2 class="text-h6 mb-2">{{ ui.t.ingredients }}</h2>
        <IngredientList :guid="guid" :ingredients="recipe.ingredients" :readonly="readonly" @refresh="loadRecipe" />
      </v-col>
      <v-col cols="12" lg="7">
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
import { ref, computed, onMounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { useReadonly } from '../composables/useReadonly'
import { useUiStore } from '../stores/ui'
import { useRecipesStore } from '../stores/recipes'
import { getHighlightWords } from '../composables/useIngredientHighlighter'
import type { RecipeDto } from '../api/types'
import IngredientList from '../components/IngredientList.vue'
import StepList from '../components/StepList.vue'

const route = useRoute()
const router = useRouter()
const guid = route.params.guid as string
const { viewingUser, shareToken, readonly } = useReadonly()
const ui = useUiStore()
const store = useRecipesStore()
const recipe = ref<RecipeDto | null>(null)
watch(recipe, (r) => { document.title = r?.name ?? 'MyCookbook' }, { immediate: true })
const loading = ref(true)
const newTag = ref('')
const tagMenuOpen = ref(false)
const highlightWords = ref(new Set<string>())
const editingMeta = ref(false)
const snackbar = ref(false)

const availableTags = computed(() =>
  store.allTags.filter((t) => !recipe.value?.tags?.includes(t))
)

onMounted(() => {
  loadRecipe()
  if (store.allTags.length === 0) store.fetchAllTags()
  if (store.allCategories.length === 0) store.fetchAllCategories()
})

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
  store.fetchAllCategories()
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

async function onDropdownItemClick(value: string) {
  tagMenuOpen.value = false
  await commitTag(value)
  await nextTick()
  tagMenuOpen.value = false
}

async function addTag() {
  await nextTick() // let combobox commit selected value before reading
  const value = typeof newTag.value === 'string' ? newTag.value.trim() : ''
  if (!value) return
  await commitTag(value)
}

async function commitTag(value: string) {
  newTag.value = ''
  await recipesApi.addTag(guid, value)
  await loadRecipe()
  await store.fetchAllTags()
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
