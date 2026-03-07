<template>
  <v-table hover>
    <thead>
      <tr>
        <th>Name</th>
        <th>Category</th>
        <th>Duration</th>
        <th>Last Cooked</th>
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
        <td colspan="4" class="text-medium-emphasis pa-4">No recipes found.</td>
      </tr>
    </tbody>
  </v-table>
</template>

<script setup lang="ts">
import type { RecipeDto } from '../api/types'

defineProps<{ recipes: RecipeDto[] }>()

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString()
}
</script>
