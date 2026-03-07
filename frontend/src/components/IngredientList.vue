<template>
  <div>
    <div v-for="ing in sorted" :key="ing.id" style="display: flex; gap: 0.5rem; margin-bottom: 4px; align-items: center;">
      <span style="min-width: 80px; color: #555;">{{ ing.amount }}</span>
      <span v-if="readonly || !editing[ing.id]" @dblclick="!readonly && startEdit(ing.id)">{{ ing.name }}</span>
      <input
        v-else
        v-model="editValues[ing.id]"
        @blur="saveEdit(ing)"
        @keyup.enter="saveEdit(ing)"
        autofocus
        style="flex: 1;"
      />
      <template v-if="!readonly">
        <button @click="moveUp(ing)" title="Move up">↑</button>
        <button @click="moveDown(ing)" title="Move down">↓</button>
        <button @click="remove(ing)" title="Delete">×</button>
      </template>
    </div>

    <div v-if="!readonly" style="display: flex; gap: 0.5rem; margin-top: 0.5rem;">
      <input v-model="newAmount" placeholder="Amount" style="width: 80px;" />
      <input v-model="newName" placeholder="Ingredient name" @keyup.enter="addIngredient" />
      <button @click="addIngredient">Add</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { recipesApi } from '../api/recipes'
import type { IngredientDto } from '../api/types'

const props = defineProps<{ guid: string; ingredients: IngredientDto[]; readonly?: boolean }>()
const emit = defineEmits<{ refresh: [] }>()

const sorted = computed(() => [...props.ingredients].sort((a, b) => a.order - b.order))
const editing = ref<Record<number, boolean>>({})
const editValues = ref<Record<number, string>>({})
const newName = ref('')
const newAmount = ref('')

function startEdit(id: number) {
  const ing = props.ingredients.find((i) => i.id === id)
  editValues.value[id] = ing?.name ?? ''
  editing.value[id] = true
}

async function saveEdit(ing: IngredientDto) {
  editing.value[ing.id] = false
  if (editValues.value[ing.id] !== ing.name) {
    await recipesApi.updateIngredient(props.guid, ing.id, {
      name: editValues.value[ing.id] ?? '',
      amount: ing.amount,
    })
    emit('refresh')
  }
}

async function moveUp(ing: IngredientDto) {
  await recipesApi.moveIngredientUp(props.guid, ing.id)
  emit('refresh')
}

async function moveDown(ing: IngredientDto) {
  await recipesApi.moveIngredientDown(props.guid, ing.id)
  emit('refresh')
}

async function remove(ing: IngredientDto) {
  await recipesApi.deleteIngredient(props.guid, ing.id)
  emit('refresh')
}

async function addIngredient() {
  if (!newName.value.trim()) return
  await recipesApi.addIngredient(props.guid, { name: newName.value.trim(), amount: newAmount.value.trim() || undefined })
  newName.value = ''
  newAmount.value = ''
  emit('refresh')
}
</script>
