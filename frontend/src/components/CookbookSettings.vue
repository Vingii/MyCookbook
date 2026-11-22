<template>
  <v-card id="cookbooks" class="mb-6" variant="outlined" data-test="cookbooks-card">
    <v-card-title>{{ ui.t.cookbooks }}</v-card-title>
    <v-card-text>
      <p class="mb-3">{{ ui.t.cookbooksDesc }}</p>

      <div
        v-for="c in store.cookbooks"
        :key="c.id"
        class="d-flex flex-wrap align-center ga-3 mb-3"
        :data-test="`cookbook-row-${c.id}`"
      >
        <v-text-field
          v-model="names[c.id]"
          :placeholder="ui.t.defaultCookbookName"
          :label="ui.t.cookbookName"
          :maxlength="MAX_NAME_LENGTH"
          density="compact"
          variant="outlined"
          hide-details
          style="min-width: 200px; flex: 1;"
          data-test="cookbook-name"
          @blur="saveName(c)"
          @keyup.enter="saveName(c)"
        />
        <v-switch
          :model-value="c.showInPlanner"
          :label="ui.t.cookbookShowInPlanner"
          color="primary"
          density="compact"
          hide-details
          inset
          data-test="cookbook-planner"
          @update:model-value="(v) => save(c, { showInPlanner: !!v })"
        />
        <span class="text-caption text-medium-emphasis" style="min-width: 80px;">{{ store.recipeCountLabel(c) }}</span>
        <v-btn
          icon="mdi-delete"
          variant="text"
          size="small"
          :disabled="busy || !canDelete(c)"
          :title="deleteHint(c)"
          :aria-label="ui.t.delete"
          data-test="cookbook-delete"
          @click="remove(c)"
        />
      </div>

      <div class="d-flex flex-wrap align-center ga-2 mt-4">
        <v-text-field
          v-model="newName"
          :label="ui.t.newCookbook"
          :maxlength="MAX_NAME_LENGTH"
          density="compact"
          variant="outlined"
          hide-details
          style="min-width: 200px; flex: 1;"
          data-test="cookbook-new-name"
          @keyup.enter="add"
        />
        <v-btn color="primary" prepend-icon="mdi-plus" :disabled="busy || !newName.trim()" data-test="cookbook-add" @click="add">
          {{ ui.t.add }}
        </v-btn>
      </div>

      <v-alert v-if="error" type="error" density="compact" class="mt-3" data-test="cookbook-error">{{ error }}</v-alert>
    </v-card-text>
  </v-card>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, watch } from 'vue'
import { useCookbooksStore } from '../stores/cookbooks'
import { useUiStore } from '../stores/ui'
import { cookbooksApi } from '../api/cookbooks'
import type { CookbookDto } from '../api/types'

// Matches CookbookService.MaxNameLength on the server.
const MAX_NAME_LENGTH = 50

const store = useCookbooksStore()
const ui = useUiStore()
const names = reactive<Record<number, string>>({})
const newName = ref('')
const busy = ref(false)
const error = ref<string | null>(null)

watch(
  () => store.cookbooks,
  (list) => {
    for (const c of list) names[c.id] = c.name
  },
  { immediate: true },
)

const canDelete = (c: CookbookDto) => store.cookbooks.length > 1 && c.recipeCount === 0

function deleteHint(c: CookbookDto) {
  if (store.cookbooks.length <= 1) return ui.t.cookbookDeleteLast
  if (c.recipeCount > 0) return ui.t.cookbookDeleteNotEmpty
  return ui.t.delete
}

async function run(action: () => Promise<unknown>) {
  busy.value = true
  error.value = null
  try {
    await action()
    await store.fetchAll()
  } catch {
    error.value = ui.t.cookbookSaveFailed
  } finally {
    busy.value = false
  }
}

function save(c: CookbookDto, change: { name?: string; showInPlanner?: boolean }) {
  return run(() =>
    cookbooksApi.update(c.id, {
      name: change.name ?? c.name,
      showInPlanner: change.showInPlanner ?? c.showInPlanner,
    }),
  )
}

function saveName(c: CookbookDto) {
  const name = (names[c.id] ?? '').trim()
  // A cookbook needs a name; clearing the field just undoes the edit.
  if (!name || name === c.name) {
    names[c.id] = c.name
    return
  }
  return save(c, { name })
}

function add() {
  const name = newName.value.trim()
  if (!name) return
  return run(async () => {
    await cookbooksApi.create({ name, showInPlanner: true })
    newName.value = ''
  })
}

function remove(c: CookbookDto) {
  if (!canDelete(c)) return
  if (!confirm(`${ui.t.delete} "${store.displayName(c)}"?`)) return
  return run(() => cookbooksApi.delete(c.id))
}

onMounted(() => {
  store.fetchAll().catch(() => {
    error.value = ui.t.cookbookLoadFailed
  })
})
</script>
