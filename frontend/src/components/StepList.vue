<template>
  <div>
    <div v-for="step in sorted" :key="step.id" style="margin-bottom: 0.5rem;">
      <div style="display: flex; gap: 0.5rem; align-items: flex-start;">
        <span style="min-width: 24px; color: #888;">{{ step.order }}.</span>
        <div style="flex: 1;">
          <div v-if="!editing[step.id]" @dblclick="startEdit(step.id)" style="white-space: pre-wrap;">
            {{ step.description }}
          </div>
          <textarea
            v-else
            v-model="editDesc[step.id]"
            @blur="saveEdit(step)"
            rows="3"
            style="width: 100%;"
            autofocus
          />
          <div style="font-size: 0.8em; color: #666;">
            {{ step.stepType }}
            <span v-if="step.durationSeconds"> · {{ formatDuration(step.durationSeconds) }}</span>
          </div>
        </div>
        <button @click="moveUp(step)" title="Move up">↑</button>
        <button @click="moveDown(step)" title="Move down">↓</button>
        <button @click="remove(step)" title="Delete">×</button>
      </div>
    </div>

    <div style="margin-top: 0.5rem;">
      <textarea v-model="newDesc" placeholder="Step description..." rows="2" style="width: 100%;" />
      <div style="display: flex; gap: 0.5rem; margin-top: 4px;">
        <select v-model="newType">
          <option>Active</option>
          <option>SemiPassive</option>
          <option>Passive</option>
        </select>
        <input v-model.number="newDuration" type="number" placeholder="Seconds" style="width: 80px;" />
        <button @click="addStep">Add Step</button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { recipesApi } from '../api/recipes'
import type { StepDto } from '../api/types'

const props = defineProps<{ guid: string; steps: StepDto[] }>()
const emit = defineEmits<{ refresh: [] }>()

const sorted = computed(() => [...props.steps].sort((a, b) => a.order - b.order))
const editing = ref<Record<number, boolean>>({})
const editDesc = ref<Record<number, string>>({})
const newDesc = ref('')
const newType = ref('Active')
const newDuration = ref<number | null>(null)

function formatDuration(sec: number) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return m > 0 ? `${m}m ${s}s` : `${s}s`
}

function startEdit(id: number) {
  const step = props.steps.find((s) => s.id === id)
  editDesc.value[id] = step?.description ?? ''
  editing.value[id] = true
}

async function saveEdit(step: StepDto) {
  editing.value[step.id] = false
  await recipesApi.updateStep(props.guid, step.id, {
    description: editDesc.value[step.id] ?? '',
    durationSeconds: step.durationSeconds ?? undefined,
    stepType: step.stepType,
  })
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
