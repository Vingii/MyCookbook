<template>
  <div>
    <h1 class="text-h4 mb-4">{{ ui.t.sharedTitle }}</h1>

    <div v-if="loading" class="text-center pa-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <p v-else-if="items.length === 0" class="text-medium-emphasis">{{ ui.t.sharedEmpty }}</p>

    <template v-else>
      <v-text-field
        v-model="search"
        :placeholder="ui.t.searchPlaceholder"
        prepend-inner-icon="mdi-magnify"
        density="compact"
        hide-details
        variant="outlined"
        clearable
        class="mb-4"
        style="max-width: 400px"
      />

      <p v-if="pinned.length === 0 && recent.length === 0" class="text-medium-emphasis">{{ ui.t.sharedNoMatch }}</p>

      <template v-for="section in sections" :key="section.title">
        <template v-if="section.items.length">
          <h2 class="text-subtitle-1 font-weight-bold mt-2">{{ section.title }}</h2>
          <v-list density="comfortable" class="mb-4 bg-transparent">
            <v-list-item
              v-for="item in section.items"
              :key="item.id"
              :to="item.available ? `/recipe/shared/${item.recipeGuid}` : undefined"
              :class="{ 'text-disabled': !item.available }"
              prepend-icon="mdi-silverware-fork-knife"
            >
              <v-list-item-title>{{ item.recipeName ?? ui.t.sharedUnavailable }}</v-list-item-title>
              <v-list-item-subtitle>
                {{ subtitle(item) }}
              </v-list-item-subtitle>
              <template #append>
                <v-btn
                  :icon="item.pinned ? 'mdi-pin' : 'mdi-pin-outline'"
                  :color="item.pinned ? 'primary' : undefined"
                  :aria-label="item.pinned ? ui.t.sharedUnpin : ui.t.sharedPin"
                  :title="item.pinned ? ui.t.sharedUnpin : ui.t.sharedPin"
                  variant="text"
                  size="small"
                  @click.prevent.stop="togglePin(item)"
                />
                <v-btn
                  icon="mdi-close"
                  :aria-label="ui.t.sharedRemove"
                  :title="ui.t.sharedRemove"
                  variant="text"
                  size="small"
                  @click.prevent.stop="remove(item)"
                />
              </template>
            </v-list-item>
          </v-list>
        </template>
      </template>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watchEffect } from 'vue'
import { sharedApi, type SharedItemDto } from '../api/shared'
import { useUiStore } from '../stores/ui'

const ui = useUiStore()
watchEffect(() => { document.title = ui.t.sharedTitle })

const items = ref<SharedItemDto[]>([])
const loading = ref(true)
const search = ref<string | null>('')

onMounted(async () => {
  try {
    items.value = await sharedApi.getAll()
  } finally {
    loading.value = false
  }
})

/** Case- and diacritics-insensitive, so "kure" finds "Kuře". */
function normalize(s: string) {
  return s.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase()
}

const filtered = computed(() => {
  const q = normalize(search.value ?? '').trim()
  if (!q) return items.value
  return items.value.filter((i) => normalize(`${i.recipeName ?? ''} ${i.ownerName}`).includes(q))
})
// The server already sorts pinned first, then most recently opened.
const pinned = computed(() => filtered.value.filter((i) => i.pinned))
const recent = computed(() => filtered.value.filter((i) => !i.pinned))
const sections = computed(() => [
  { title: ui.t.sharedPinned, items: pinned.value },
  { title: ui.t.sharedRecent, items: recent.value },
])

function subtitle(item: SharedItemDto) {
  const opened = `${ui.t.sharedOpened} ${new Date(item.lastOpened).toLocaleDateString(ui.locale === 'cs' ? 'cs-CZ' : 'en-US')}`
  return `${ui.t.sharedRecipeFrom} ${item.ownerName} · ${opened}`
}

async function togglePin(item: SharedItemDto) {
  const pinned = !item.pinned
  await sharedApi.setPinned(item.id, pinned)
  // Keep the server's order: pinned first, then most recently opened.
  items.value = items.value.map((i) => (i.id === item.id ? { ...i, pinned } : i)).sort(
    (a, b) => Number(b.pinned) - Number(a.pinned) || Date.parse(b.lastOpened) - Date.parse(a.lastOpened)
  )
}

async function remove(item: SharedItemDto) {
  await sharedApi.remove(item.id)
  items.value = items.value.filter((i) => i.id !== item.id)
}
</script>
