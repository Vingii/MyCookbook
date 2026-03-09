<template>
  <div v-if="stepsWithDuration.length > 0" class="step-timeline d-flex rounded overflow-hidden" style="height: 36px;">
    <div
      v-for="step in stepsWithDuration"
      :key="step.id"
      :class="colorClass(step.stepType)"
      :style="{ width: pct(step.durationSeconds!) + '%' }"
      class="d-flex align-center px-1 overflow-hidden"
      :title="step.description + ' (' + formatDuration(step.durationSeconds!) + ')'"
    >
      <span class="text-caption text-truncate" style="white-space: nowrap; overflow: hidden;">
        {{ step.description }} ({{ formatDuration(step.durationSeconds!) }})
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import type { StepDto } from '../api/types'

const props = defineProps<{ steps: StepDto[] }>()

const stepsWithDuration = computed(() => props.steps.filter((s) => (s.durationSeconds ?? 0) > 0))

const total = computed(() => stepsWithDuration.value.reduce((sum, s) => sum + (s.durationSeconds ?? 0), 0))

function pct(seconds: number) {
  if (total.value === 0) return 0
  return (seconds / total.value) * 100
}

function colorClass(type: string) {
  if (type === 'Active') return 'bg-primary'
  if (type === 'SemiPassive') return 'bg-warning'
  return 'bg-success'
}

function formatDuration(seconds: number) {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  if (s === 0) return `${m}m`
  return `${m}m ${s}s`
}
</script>

<style scoped>
.step-timeline {
  width: 100%;
}
</style>
