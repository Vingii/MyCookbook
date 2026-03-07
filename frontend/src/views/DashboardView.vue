<template>
  <div>
    <h1>Dashboard</h1>

    <section>
      <h2>Favorites</h2>
      <RecipeTable :recipes="favorites" />
    </section>

    <section>
      <h2>Longest Uncooked</h2>
      <RecipeTable :recipes="longestUncooked" />
    </section>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRecipesStore } from '../stores/recipes'
import { useReadonly } from '../composables/useReadonly'
import RecipeTable from '../components/RecipeTable.vue'

const store = useRecipesStore()
const { viewingUser, shareToken } = useReadonly()

onMounted(() => store.fetchAll({ user: viewingUser.value || undefined, shareToken: shareToken.value }))

const favorites = computed(() =>
  store.recipes.filter((r) => r.isFavorite)
)

const longestUncooked = computed(() => {
  return [...store.recipes]
    .sort((a, b) => {
      const aTime = a.lastCooked ? new Date(a.lastCooked).getTime() : 0
      const bTime = b.lastCooked ? new Date(b.lastCooked).getTime() : 0
      return aTime - bTime
    })
    .slice(0, 10)
})
</script>
