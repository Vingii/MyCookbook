<template>
  <v-data-table
      :headers="headers"
      :items="recipes"
      :hover="true"
      :no-data-text="ui.t.noRecipesFound"
      @click:row="onRowClick"
      class="recipe-table"
  >
    <template v-slot:item.lastCooked="{ value }">
      {{ formatDate(value) }}
    </template>

    <template v-slot:item.actions="{ item }">
      <v-btn
          icon="mdi-content-copy"
          size="small"
          variant="text"
          :title="ui.t.clone"
          @click.stop="emit('clone', item.guid)"
      />
    </template>
  </v-data-table>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useUiStore } from '../stores/ui'
import type { RecipeDto } from '../api/types'

defineProps<{ recipes: RecipeDto[] }>()

const emit = defineEmits<{ clone: [guid: string] }>()

const ui = useUiStore()
const router = useRouter()

const headers = computed(() => [
  { title: ui.t.colName, key: 'name', sortable: true },
  { title: ui.t.colCategory, key: 'category', sortable: true },
  { title: ui.t.colDuration, key: 'durationText', sortable: true },
  { title: ui.t.colLastCooked, key: 'lastCooked', sortable: true },
  { title: '', key: 'actions', sortable: false, align: 'end' as const },
])

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString(ui.locale === 'cs' ? 'cs-CZ' : 'en-US')
}

function onRowClick(_e: MouseEvent, { item }: { item: RecipeDto }) {
  router.push(`/recipe/${item.guid}`)
}
</script>

<style scoped>
.recipe-table :deep(.v-data-table__tr) {
  cursor: pointer;
}
</style>