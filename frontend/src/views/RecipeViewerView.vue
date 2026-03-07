<template>
  <div v-if="recipe">
    <div style="display: flex; gap: 1rem; align-items: baseline; margin-bottom: 1rem;">
      <h1 style="margin: 0;">
        <span v-if="readonly || !editingName" @dblclick="!readonly && (editingName = true)">{{ recipe.name }}</span>
        <input v-else v-model="recipe.name" @blur="saveRecipe" @keyup.enter="saveRecipe" autofocus />
      </h1>
      <template v-if="!readonly">
        <button @click="cloneRecipe">Clone</button>
        <button @click="markCooked">Mark cooked</button>
        <button @click="deleteRecipe" style="color: red;">Delete</button>
      </template>
    </div>

    <div style="display: flex; gap: 1rem; margin-bottom: 1rem; flex-wrap: wrap;">
      <label>Category:
        <input v-model="recipe.category" @blur="!readonly && saveRecipe()" :disabled="readonly" />
      </label>
      <label>Duration (min):
        <input type="number" v-model.number="recipe.duration" @blur="!readonly && saveRecipe()" :disabled="readonly" />
      </label>
      <label>Servings:
        <input type="number" v-model.number="recipe.servings" @blur="!readonly && saveRecipe()" :disabled="readonly" />
      </label>
    </div>

    <div style="margin-bottom: 1rem;">
      <strong>Tags:</strong>
      <span v-for="tag in recipe.tags" :key="tag" style="margin: 0 4px;">
        {{ tag }}
        <button v-if="!readonly" @click="removeTag(tag)">×</button>
      </span>
      <input v-if="!readonly" v-model="newTag" placeholder="Add tag..." @keyup.enter="addTag" style="width: 120px;" />
    </div>

    <div style="display: flex; gap: 2rem; flex-wrap: wrap;">
      <div>
        <h2>Ingredients</h2>
        <IngredientList :guid="guid" :ingredients="recipe.ingredients" :readonly="readonly" @refresh="loadRecipe" />
      </div>
      <div style="flex: 1;">
        <h2>Steps</h2>
        <StepList :guid="guid" :steps="recipe.steps" :readonly="readonly" @refresh="loadRecipe" />
      </div>
    </div>
  </div>
  <div v-else-if="loading">Loading...</div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { useReadonly } from '../composables/useReadonly'
import type { RecipeDto } from '../api/types'
import IngredientList from '../components/IngredientList.vue'
import StepList from '../components/StepList.vue'

const route = useRoute()
const router = useRouter()
const guid = route.params.guid as string
const { viewingUser, shareToken, readonly } = useReadonly()
const recipe = ref<RecipeDto | null>(null)
const loading = ref(true)
const editingName = ref(false)
const newTag = ref('')

onMounted(loadRecipe)

async function loadRecipe() {
  loading.value = true
  try {
    recipe.value = await recipesApi.getById(guid, { user: viewingUser.value || undefined, shareToken: shareToken.value })
  } finally {
    loading.value = false
  }
}

async function saveRecipe() {
  editingName.value = false
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

async function markCooked() {
  await recipesApi.markCooked(guid)
  await loadRecipe()
}

async function deleteRecipe() {
  if (!confirm(`Delete "${recipe.value?.name}"?`)) return
  await recipesApi.delete(guid)
  router.push('/browser')
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
</script>
