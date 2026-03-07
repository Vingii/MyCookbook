<template>
  <div>
    <div v-for="ing in sorted" :key="ing.id" class="d-flex align-center ga-2 mb-1">
      <v-text-field
        :model-value="ing.amount"
        density="compact"
        hide-details
        variant="outlined"
        style="max-width: 90px;"
        :readonly="readonly"
        @change="(v: string) => saveIngredient(ing, { amount: v, name: ing.name })"
      />
      <v-text-field
        :model-value="ing.name"
        density="compact"
        hide-details
        variant="outlined"
        class="flex-grow-1"
        :readonly="readonly"
        @change="(v: string) => saveIngredient(ing, { amount: ing.amount ?? '', name: v })"
      />
      <template v-if="!readonly">
        <v-btn icon="mdi-arrow-up" size="small" variant="text" @click="moveUp(ing)" />
        <v-btn icon="mdi-arrow-down" size="small" variant="text" @click="moveDown(ing)" />
        <v-btn icon="mdi-delete" size="small" variant="text" color="error" @click="remove(ing)" />
      </template>
    </div>


    <div v-if="!readonly" class="d-flex align-center ga-2 mt-3">
      <v-text-field
        v-model="newAmount"
        placeholder="Amount"
        density="compact"
        hide-details
        variant="outlined"
        style="max-width: 90px;"
      />
      <v-text-field
        v-model="newName"
        placeholder="Ingredient name"
        density="compact"
        hide-details
        variant="outlined"
        class="flex-grow-1"
        @keyup.enter="addIngredient"
      />
      <v-btn color="primary" size="small" @click="addIngredient">Add</v-btn>
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
const newName = ref('')
const newAmount = ref('')

async function saveIngredient(ing: IngredientDto, update: { name: string; amount: string }) {
  if (update.name === ing.name && update.amount === ing.amount) return
  await recipesApi.updateIngredient(props.guid, ing.id, { name: update.name, amount: update.amount })
  emit('refresh')
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
