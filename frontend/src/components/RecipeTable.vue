<template>
  <table style="width: 100%; border-collapse: collapse;">
    <thead>
      <tr>
        <th style="text-align: left; padding: 4px 8px;">Name</th>
        <th style="text-align: left; padding: 4px 8px;">Category</th>
        <th style="text-align: left; padding: 4px 8px;">Duration</th>
        <th style="text-align: left; padding: 4px 8px;">Last Cooked</th>
      </tr>
    </thead>
    <tbody>
      <tr
        v-for="recipe in recipes"
        :key="recipe.guid"
        @click="$router.push(`/recipe/${recipe.guid}`)"
        style="cursor: pointer;"
        :style="{ background: recipe.isFavorite ? '#fffbe6' : undefined }"
      >
        <td style="padding: 4px 8px;">{{ recipe.name }}</td>
        <td style="padding: 4px 8px;">{{ recipe.category }}</td>
        <td style="padding: 4px 8px;">{{ recipe.durationText }}</td>
        <td style="padding: 4px 8px;">{{ formatDate(recipe.lastCooked) }}</td>
      </tr>
      <tr v-if="!recipes.length">
        <td colspan="4" style="padding: 8px; color: #999;">No recipes found.</td>
      </tr>
    </tbody>
  </table>
</template>

<script setup lang="ts">
import type { RecipeDto } from '../api/types'

defineProps<{ recipes: RecipeDto[] }>()

function formatDate(d?: string) {
  if (!d) return '—'
  return new Date(d).toLocaleDateString()
}
</script>
