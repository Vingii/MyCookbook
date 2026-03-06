<template>
  <div>Loading random recipe...</div>
</template>

<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'

const router = useRouter()

onMounted(async () => {
  try {
    const recipe = await recipesApi.getRandom()
    router.replace(`/recipe/${recipe.guid}`)
  } catch {
    router.replace('/')
  }
})
</script>
