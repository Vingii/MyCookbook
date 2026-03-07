<template>
  <div>
    <div v-for="step in sorted" :key="step.id" class="mb-3">
      <div class="d-flex align-start ga-2">
        <span class="text-medium-emphasis mt-2" style="min-width: 24px;">{{ step.order }}.</span>
        <div class="flex-grow-1">
          <v-textarea
            :model-value="step.description"
            density="compact"
            hide-details
            variant="outlined"
            auto-grow
            rows="2"
            :readonly="readonly"
            @change="(v: string) => saveStep(step, { description: v, stepType: step.stepType, durationSeconds: step.durationSeconds ?? undefined })"
          />
          <div class="d-flex ga-2 mt-1">
            <v-select
              :model-value="step.stepType"
              :items="stepTypes"
              density="compact"
              hide-details
              variant="outlined"
              style="max-width: 160px;"
              :readonly="readonly"
              @update:model-value="(v: string) => saveStep(step, { description: step.description, stepType: v, durationSeconds: step.durationSeconds ?? undefined })"
            />
            <v-text-field
              :model-value="step.durationSeconds ?? null"
              type="number"
              density="compact"
              hide-details
              variant="outlined"
              :placeholder="ui.t.secondsPlaceholder"
              style="max-width: 120px;"
              :readonly="readonly"
              @change="(v: string) => saveStep(step, { description: step.description, stepType: step.stepType, durationSeconds: v ? Number(v) : undefined })"
            />
            <span v-if="step.durationSeconds" class="text-medium-emphasis text-caption mt-2">
              {{ formatDuration(step.durationSeconds) }}
            </span>
          </div>
        </div>
        <template v-if="!readonly">
          <v-btn icon="mdi-arrow-up" size="small" variant="text" @click="moveDown(step)" />
          <v-btn icon="mdi-arrow-down" size="small" variant="text" @click="moveUp(step)" />
          <v-btn icon="mdi-delete" size="small" variant="text" color="error" @click="remove(step)" />
        </template>
      </div>
    </div>

    <div v-if="!readonly" class="mt-3">
      <v-textarea
        v-model="newDesc"
        :placeholder="ui.t.stepDescPlaceholder"
        density="compact"
        hide-details
        variant="outlined"
        auto-grow
        rows="2"
        class="mb-2"
      />
      <div class="d-flex ga-2 align-center">
        <v-select
          v-model="newType"
          :items="stepTypes"
          density="compact"
          hide-details
          variant="outlined"
          style="max-width: 160px;"
        />
        <v-text-field
          v-model.number="newDuration"
          type="number"
          :placeholder="ui.t.secondsPlaceholder"
          density="compact"
          hide-details
          variant="outlined"
          style="max-width: 120px;"
        />
        <v-btn color="primary" size="small" @click="addStep">{{ ui.t.addStep }}</v-btn>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { recipesApi } from '../api/recipes'
import { useUiStore } from '../stores/ui'
import type { StepDto } from '../api/types'

const props = defineProps<{ guid: string; steps: StepDto[]; readonly?: boolean }>()
const emit = defineEmits<{ refresh: [] }>()

const ui = useUiStore()
const stepTypes = ['Active', 'SemiPassive', 'Passive']
const sorted = computed(() => [...props.steps].sort((a, b) => a.order - b.order))
const newDesc = ref('')
const newType = ref('Active')
const newDuration = ref<number | null>(null)

function formatDuration(sec: number) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return m > 0 ? `${m}m ${s}s` : `${s}s`
}

async function saveStep(step: StepDto, update: { description: string; stepType: string; durationSeconds?: number }) {
  await recipesApi.updateStep(props.guid, step.id, update)
  emit('refresh')
}

async function moveUp(step: StepDto) {
  await recipesApi.moveStepUp(props.guid, step.id)
  emit('refresh')
}

async function moveDown(step: StepDto) {
  await recipesApi.moveStepDown(props.guid, step.id)
  emit('refresh')
}

async function remove(step: StepDto) {
  await recipesApi.deleteStep(props.guid, step.id)
  emit('refresh')
}

async function addStep() {
  if (!newDesc.value.trim()) return
  await recipesApi.addStep(props.guid, {
    description: newDesc.value.trim(),
    stepType: newType.value,
    durationSeconds: newDuration.value ?? undefined,
  })
  newDesc.value = ''
  newDuration.value = null
  emit('refresh')
}
</script>
