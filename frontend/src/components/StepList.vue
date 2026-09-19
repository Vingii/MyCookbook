<template>
  <div>
    <div v-for="(step, idx) in sorted" :key="step.id" class="d-flex" style="min-height: 56px;">

      <!-- Vertical timeline bar -->
      <div
        class="step-bar flex-shrink-0 d-flex align-center justify-center"
        :style="{
          background: barColor(step.stepType),
          cursor: (!readonly && !editingSteps.has(step.id)) ? 'pointer' : 'default',
          borderTopLeftRadius: idx === 0 ? '4px' : '0',
          borderBottomLeftRadius: idx === sorted.length - 1 ? '4px' : '0',
        }"
        @click="!readonly && !editingSteps.has(step.id) && cycleStepType(step)"
      >
        <div
          style="display: flex; align-items: center; justify-content: center;"
          @click.stop="!readonly && !editingSteps.has(step.id) && promptDuration(step)"
        >
          <v-icon v-if="!step.durationSeconds" color="white" size="small">mdi-clock-outline</v-icon>
          <span
            v-else
            style="color: white; font-weight: bold; text-align: center; padding: 0 3px; font-size: 0.65rem; line-height: 1.2; word-break: break-all;"
          >{{ formatDuration(step.durationSeconds) }}</span>
        </div>
      </div>

      <!-- Step content -->
      <div class="d-flex align-start ga-2 flex-grow-1 pa-2">
        <span class="text-medium-emphasis mt-1" style="min-width: 20px; font-size: 0.85rem;">{{ step.order }}.</span>
        <div class="flex-grow-1">
          <template v-if="editingSteps.has(step.id)">
            <StepTextarea
              :ref="(el: any) => { if (el) stepDescRefs[step.id] = el }"
              :model-value="editValues[step.id]?.description ?? ''"
              :names="store.allRecipeNames"
              @update:model-value="(v: string) => setDescription(step.id, v)"
              @blur="saveStep(step)"
              @enter="saveAndCloseStep(step)"
            />
          </template>
          <template v-else>
            <div
              class="text-body-2 py-1 step-text"
              style="white-space: pre-wrap; line-height: 1.6;"
              v-html="renderDescription(step.description)"
              @click="onDescriptionClick"
            />
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
      <StepTextarea
        ref="newDescRef"
        v-model="newDesc"
        :names="store.allRecipeNames"
        :placeholder="ui.t.stepDescPlaceholder"
        class="mb-2"
        @enter="addStep"
      />
      <v-btn color="primary" size="small" @click="addStep">{{ ui.t.addStep }}</v-btn>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, nextTick } from 'vue'
import { useRouter } from 'vue-router'
import { recipesApi } from '../api/recipes'
import { useUiStore } from '../stores/ui'
import { useRecipesStore } from '../stores/recipes'
import { highlightText } from '../composables/useIngredientHighlighter'
import { renderWithLinks, useRecipeHref } from '../composables/useRecipeLinks'
import type { StepDto, RecipeLinkDto } from '../api/types'
import StepTextarea from './StepTextarea.vue'

const props = defineProps<{
  guid: string
  steps: StepDto[]
  readonly?: boolean
  highlightWords?: Set<string>
  links?: RecipeLinkDto[]
}>()
const emit = defineEmits<{ refresh: [] }>()

const ui = useUiStore()
const store = useRecipesStore()
const router = useRouter()
const recipeHref = useRecipeHref()

onMounted(() => {
  if (!props.readonly && store.allRecipeNames.length === 0) store.fetchAllRecipeNames()
})
const sorted = computed(() => [...props.steps].sort((a, b) => a.order - b.order))
const newDesc = ref('')
const newType = ref('Active')
const newDuration = ref<number | null>(null)
const editingSteps = ref(new Set<number>())
const editValues = ref<Record<number, { description: string; stepType: string; durationSeconds?: number }>>({})
const newDescRef = ref<any>(null)
const stepDescRefs: Record<number, any> = {}

watch(() => props.steps, (steps) => {
  const updated: Record<number, { description: string; stepType: string; durationSeconds?: number }> = {}
  for (const s of steps) {
    updated[s.id] = { description: s.description ?? '', stepType: s.stepType, durationSeconds: s.durationSeconds }
  }
  editValues.value = updated
}, { immediate: true })

function barColor(type: string): string {
  if (type === 'Active') return 'rgb(var(--v-theme-primary))'
  if (type === 'SemiPassive') return 'rgb(var(--v-theme-warning))'
  return 'rgb(var(--v-theme-success))'
}

function formatDuration(sec: number): string {
  if (!sec) return '0s'
  const h = Math.floor(sec / 3600)
  const m = Math.floor((sec % 3600) / 60)
  const s = sec % 60
  const parts = []
  if (h > 0) parts.push(`${h}h`)
  if (m > 0) parts.push(`${m}m`)
  if (s > 0) parts.push(`${s}s`)
  return parts.join(' ') || '0s'
}

function parseDuration(input: string): number {
  input = input.trim().toLowerCase()
  if (!input || input === '0') return 0
  const h = input.match(/(\d+)\s*h/)
  const m = input.match(/(\d+)\s*m(?!s)/)
  const s = input.match(/(\d+)\s*s/)
  let total = 0
  if (h?.[1]) total += parseInt(h[1]) * 3600
  if (m?.[1]) total += parseInt(m[1]) * 60
  if (s?.[1]) total += parseInt(s[1])
  if (total === 0) {
    const n = parseInt(input) * 60
    if (!isNaN(n)) return n
  }
  return total
}

async function cycleStepType(step: StepDto) {
  const next = step.stepType === 'Active' ? 'Passive'
             : step.stepType === 'Passive' ? 'SemiPassive' : 'Active'
  await recipesApi.updateStep(props.guid, step.id, {
    description: step.description,
    durationSeconds: step.durationSeconds,
    stepType: next,
  })
  emit('refresh')
}

async function promptDuration(step: StepDto) {
  const current = step.durationSeconds ? formatDuration(step.durationSeconds) : ''
  const input = prompt('Duration (e.g. 30s, 2m, 1m30s, 1h):', current)
  if (input === null) return
  const seconds = parseDuration(input)
  await recipesApi.updateStep(props.guid, step.id, {
    description: step.description,
    durationSeconds: seconds || undefined,
    stepType: step.stepType,
  })
  emit('refresh')
}

function toggleEdit(id: number) {
  const s = new Set(editingSteps.value)
  const opening = !s.has(id)
  if (opening) s.add(id)
  else s.delete(id)
  editingSteps.value = s
  if (opening) nextTick(() => stepDescRefs[id]?.focus())
}

function setDescription(id: number, v: string) {
  const e = editValues.value[id]
  if (e) e.description = v
}

function renderDescription(text: string): string {
  return renderWithLinks(
    text,
    props.links ?? [],
    (part) => highlightText(part, props.highlightWords ?? new Set()),
    recipeHref,
  )
}

/** Recipe links are real anchors so middle-click opens a new tab; plain clicks stay in the SPA. */
function onDescriptionClick(e: MouseEvent) {
  const anchor = (e.target as HTMLElement).closest?.('a.recipe-link') as HTMLAnchorElement | null
  if (!anchor || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return
  e.preventDefault()
  router.push(anchor.getAttribute('href')!)
}

async function saveStep(step: StepDto) {
  const update = editValues.value[step.id]
  if (!update) return
  await recipesApi.updateStep(props.guid, step.id, update)
  emit('refresh')
}

async function saveAndCloseStep(step: StepDto) {
  await saveStep(step)
  toggleEdit(step.id)
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
  await nextTick()
  newDescRef.value?.focus()
}
</script>

<style scoped>
.step-bar {
  width: 40px;
  transition: filter 0.15s ease;
  user-select: none;
}
.step-bar:hover:not([style*="cursor: default"]) {
  filter: brightness(1.1);
}
.step-text :deep(.recipe-link) {
  color: rgb(var(--v-theme-primary));
  font-weight: 500;
  text-decoration: none;
  border-bottom: 1px solid currentColor;
}
.step-text :deep(.recipe-link:hover) {
  opacity: 0.8;
}
/* A link whose target recipe does not exist — visible to the editor, unobtrusive while cooking. */
.step-text :deep(.recipe-link-missing) {
  opacity: 0.6;
  border-bottom: 1px dashed currentColor;
}
</style>
