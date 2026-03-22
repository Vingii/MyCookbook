<template>
  <div>
    <div v-for="ing in sorted" :key="ing.id" class="d-flex align-start ga-2 mb-1">
      <template v-if="!readonly && editingIngredients.has(ing.id)">
        <v-combobox
            :ref="(el: any) => { if (el) ingNameRefs[ing.id] = el }"
            :model-value="editValues[ing.id]?.name"
            :items="store.ingredientNames"
            density="compact"
            hide-details
            variant="outlined"
            class="flex-grow-1"
            @update:model-value="(v: string) => setName(ing.id, v)"
            @blur="saveIngredient(ing)"
            @keyup.enter="focusIngAmount(ing.id)"
        />
        <v-text-field
          :ref="(el: any) => { if (el) ingAmountRefs[ing.id] = el }"
          :model-value="editValues[ing.id]?.amount"
          density="compact"
          hide-details
          variant="outlined"
          :placeholder="ui.t.amountPlaceholder"
          style="max-width: 90px;"
          @update:model-value="(v: string) => setAmount(ing.id, v)"
          @blur="saveIngredient(ing)"
          @keyup.enter="saveAndCloseIngredient(ing)"
        />
      </template>
      <template v-else>
        <v-btn
          :icon="checked.has(ing.id) ? 'mdi-checkbox-marked' : 'mdi-checkbox-blank-outline'"
          size="x-small"
          variant="text"
          density="compact"
          @click="toggleCheck(ing.id)"
        />
        <span
          class="flex-grow-1"
          :class="{ 'text-decoration-line-through text-medium-emphasis': checked.has(ing.id) }"
        >{{ ing.name }}</span>
        <span
            class="text-medium-emphasis text-center"
            style="min-width: 90px;"
            :class="{ 'text-decoration-line-through': checked.has(ing.id) }"
        >{{ ing.amount || '' }}</span>
      </template>
      <template v-if="!readonly">
        <v-btn
          :icon="editingIngredients.has(ing.id) ? 'mdi-check' : 'mdi-pencil'"
          size="small"
          variant="text"
          :color="editingIngredients.has(ing.id) ? 'primary' : undefined"
          @click="toggleEdit(ing.id)"
        />
        <v-btn icon="mdi-arrow-up" size="small" variant="text" @click="moveDown(ing)" />
        <v-btn icon="mdi-arrow-down" size="small" variant="text" @click="moveUp(ing)" />
        <v-btn icon="mdi-delete" size="small" variant="text" color="error" @click="remove(ing)" />
      </template>
    </div>

    <div v-if="!readonly" class="d-flex align-center ga-2 mt-3">
      <v-combobox
        ref="newNameRef"
        v-model="newName"
        :items="store.ingredientNames"
        :placeholder="ui.t.ingredientNamePlaceholder"
        density="compact"
        hide-details
        variant="outlined"
        class="flex-grow-1"
        @keyup.enter="onNewNameEnter"
      />
      <v-text-field
          ref="newAmountRef"
          v-model="newAmount"
          :placeholder="ui.t.amountPlaceholder"
          density="compact"
          hide-details
          variant="outlined"
          style="max-width: 90px;"
          @keyup.enter="onNewAmountEnter"
      />
      <v-btn color="primary" size="small" @click="addIngredient">{{ ui.t.addIngredient }}</v-btn>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { recipesApi } from '../api/recipes'
import { useUiStore } from '../stores/ui'
import { useRecipesStore } from '../stores/recipes'
import type { IngredientDto } from '../api/types'

const props = defineProps<{ guid: string; ingredients: IngredientDto[]; readonly?: boolean }>()
const emit = defineEmits<{ refresh: [] }>()

const ui = useUiStore()
const store = useRecipesStore()

onMounted(() => {
  if (store.ingredientNames.length === 0) store.fetchIngredientNames()
})

const sorted = computed(() => [...props.ingredients].sort((a, b) => a.order - b.order))
const newName = ref('')
const newAmount = ref('')
const editValues = ref<Record<number, { name: string; amount: string }>>({})
const editingIngredients = ref(new Set<number>())
const checked = ref(new Set<number>())
const newNameRef = ref<any>(null)
const newAmountRef = ref<any>(null)
const ingNameRefs: Record<number, any> = {}
const ingAmountRefs: Record<number, any> = {}

function toggleCheck(id: number) {
  const s = new Set(checked.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  checked.value = s
}

watch(() => props.ingredients, (ingredients) => {
  const updated: Record<number, { name: string; amount: string }> = {}
  for (const ing of ingredients) {
    updated[ing.id] = { name: ing.name ?? '', amount: ing.amount ?? '' }
  }
  editValues.value = updated
}, { immediate: true })

function toggleEdit(id: number) {
  const s = new Set(editingIngredients.value)
  const opening = !s.has(id)
  if (opening) s.add(id)
  else s.delete(id)
  editingIngredients.value = s
  if (opening) nextTick(() => ingNameRefs[id]?.focus())
}

function setAmount(id: number, v: string) {
  const e = editValues.value[id]
  if (e) e.amount = v
}

function setName(id: number, v: string) {
  const e = editValues.value[id]
  if (e) e.name = v
}

function focusIngAmount(id: number) {
  nextTick(() => ingAmountRefs[id]?.focus())
}

async function saveIngredient(ing: IngredientDto) {
  const update = editValues.value[ing.id]
  if (!update) return
  if (update.name === (ing.name ?? '') && update.amount === (ing.amount ?? '')) return
  await recipesApi.updateIngredient(props.guid, ing.id, { name: update.name, amount: update.amount || undefined })
  emit('refresh')
}

async function saveAndCloseIngredient(ing: IngredientDto) {
  await saveIngredient(ing)
  toggleEdit(ing.id)
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

async function onNewNameEnter() {
  await nextTick() // let combobox commit the selected value first
  newAmountRef.value?.focus()
}

async function onNewAmountEnter() {
  await addIngredient()
  await nextTick()
  newNameRef.value?.focus()
}
</script>
