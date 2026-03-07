<template>
  <v-table hover>
    <thead>
      <tr>
        <th>{{ ui.t.colName }}</th>
        <th>{{ ui.t.colCategory }}</th>
        <th>{{ ui.t.colDuration }}</th>
        <th>{{ ui.t.colLastCooked }}</th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="recipe in recipes"
        :key="recipe.guid"
        @click="$router.push(`/recipe/${recipe.guid}`)"
        style="cursor: pointer;"
      >
        <td>{{ recipe.name }}</td>
        <td>{{ recipe.category }}</td>
        <td>{{ recipe.durationText }}</td>
        <td>{{ formatDate(recipe.lastCooked) }}</td>
      </tr>
      <tr v-if="!recipes.length">
        <td colspan="4" class="text-medium-emphasis pa-4">{{ ui.t.noRecipesFound }}</td>
      </tr>
    </tbody>
  </v-table>
</template>

<script setup lang="ts">
import { useUiStore } from '../stores/ui'
import type { RecipeDto } from '../api/types'

defineProps<{ recipes: RecipeDto[] }>()

const ui = useUiStore()

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString()
}
</script>
