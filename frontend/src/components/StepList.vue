<template>
  <div>
    <div v-for="step in sorted" :key="step.id" class="mb-3">
      <div class="d-flex align-start ga-2">
        <span class="text-medium-emphasis mt-2" style="min-width: 24px;">{{ step.order }}.</span>
        <div class="flex-grow-1">
          <template v-if="editingSteps.has(step.id)">
            <v-textarea
              :model-value="editValues[step.id]?.description"
              density="compact"
              hide-details
              variant="outlined"
              auto-grow
              rows="2"
              @update:model-value="(v: string) => setDescription(step.id, v)"
              @blur="saveStep(step)"
            />
            <div class="d-flex ga-2 mt-1">
              <v-select
                :model-value="editValues[step.id]?.stepType"
                :items="stepTypes"
                density="compact"
                hide-details
                variant="outlined"
                style="max-width: 160px;"
                @update:model-value="(v: string) => saveStepType(step, v)"
              />
              <v-text-field
                :model-value="editValues[step.id]?.durationSeconds ?? null"
                type="number"
                density="compact"
                hide-details
                variant="outlined"
                :placeholder="ui.t.secondsPlaceholder"
                style="max-width: 120px;"
                @update:model-value="(v: string) => setDuration(step.id, v)"
                @blur="saveStep(step)"
              />
              <span v-if="editValues[step.id]?.durationSeconds" class="text-medium-emphasis text-caption mt-2">
                {{ formatDuration(editValues[step.id]?.durationSeconds ?? 0) }}
              </span>
            </div>
          </template>
          <template v-else>
            <div
              class="text-body-2 py-2"
              style="white-space: pre-wrap; line-height: 1.6;"
              v-html="renderDescription(step.description)"
            />
            <div v-if="step.durationSeconds || step.stepType" class="d-flex ga-2 mt-1">
              <v-chip size="small" variant="tonal">{{ step.stepType }}</v-chip>
              <v-chip v-if="step.durationSeconds" size="small" variant="tonal">{{ formatDuration(step.durationSeconds) }}</v-chip>
            </div>
          </template>
        </div>
        <template v-if="!readonly">
          <v-btn
            :icon="editingSteps.has(step.id) ? 'mdi-check' : 'mdi-pencil'"
            size="small"
            variant="text"
            :color="editingSteps.has(step.id) ? 'primary' : undefined"
            @click="toggleEdit(step.id)"
          />
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
import { ref, computed, watch } from 'vue'
import { recipesApi } from '../api/recipes'
import { useUiStore } from '../stores/ui'
import { highlightText } from '../composables/useIngredientHighlighter'
import type { StepDto } from '../api/types'

const props = defineProps<{ guid: string; steps: StepDto[]; readonly?: boolean; highlightWords?: Set<string> }>()
const emit = defineEmits<{ refresh: [] }>()

const ui = useUiStore()
const stepTypes = ['Active', 'SemiPassive', 'Passive']
const sorted = computed(() => [...props.steps].sort((a, b) => a.order - b.order))
const newDesc = ref('')
const newType = ref('Active')
const newDuration = ref<number | null>(null)
const editingSteps = ref(new Set<number>())
const editValues = ref<Record<number, { description: string; stepType: string; durationSeconds?: number }>>({})

watch(() => props.steps, (steps) => {
  const updated: Record<number, { description: string; stepType: string; durationSeconds?: number }> = {}
  for (const s of steps) {
    updated[s.id] = { description: s.description ?? '', stepType: s.stepType, durationSeconds: s.durationSeconds }
  }
  editValues.value = updated
}, { immediate: true })

function toggleEdit(id: number) {
  const s = new Set(editingSteps.value)
  if (s.has(id)) s.delete(id)
  else s.add(id)
  editingSteps.value = s
}

function setDescription(id: number, v: string) {
  const e = editValues.value[id]
  if (e) e.description = v
}

function setDuration(id: number, v: string) {
  const e = editValues.value[id]
  if (e) e.durationSeconds = v ? Number(v) : undefined
}

async function saveStepType(step: StepDto, v: string) {
  const e = editValues.value[step.id]
  if (!e) return
  e.stepType = v
  await recipesApi.updateStep(props.guid, step.id, e)
  emit('refresh')
}

function renderDescription(text: string): string {
  return highlightText(text, props.highlightWords ?? new Set())
}

function formatDuration(sec: number) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return m > 0 ? `${m}m ${s}s` : `${s}s`
}

async function saveStep(step: StepDto) {
  const update = editValues.value[step.id]
  if (!update) return
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
