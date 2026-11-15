<template>
  <v-dialog :model-value="modelValue" max-width="560" scrollable @update:model-value="close">
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between">
        <span>{{ ui.t.shoppingList }}</span>
        <v-btn icon="mdi-close" variant="text" density="comfortable" :aria-label="ui.t.close" @click="close" />
      </v-card-title>

      <v-card-text>
        <div class="d-flex ga-3 mb-1">
          <v-text-field
            v-model="from"
            :label="ui.t.shoppingListFrom"
            type="date"
            variant="outlined"
            density="compact"
            hide-details
            data-test="from"
          />
          <v-text-field
            v-model="to"
            :label="ui.t.shoppingListTo"
            type="date"
            :min="from"
            variant="outlined"
            density="compact"
            hide-details
            data-test="to"
          />
        </div>
        <p class="text-caption text-medium-emphasis mb-3">{{ ui.t.shoppingListFridgeNote }}</p>

        <div v-if="loading" class="d-flex justify-center py-6">
          <v-progress-circular indeterminate color="primary" size="32" />
        </div>
        <p v-else-if="sorted.length === 0" class="text-medium-emphasis py-4" data-test="empty">
          {{ ui.t.shoppingListEmpty }}
        </p>
        <v-list v-else density="compact" class="py-0">
          <v-list-item
            v-for="item in sorted"
            :key="item.name"
            class="px-0"
            data-test="item"
            :class="{ 'text-disabled': checked.has(item.name) }"
            @click="toggle(item.name)"
          >
            <template #prepend>
              <v-checkbox-btn :model-value="checked.has(item.name)" density="compact" class="mr-1" />
            </template>
            <v-list-item-title :class="{ 'text-decoration-line-through': checked.has(item.name) }">
              <span class="font-weight-medium">{{ item.name }}</span>
              <span v-if="amountText(item)" class="ml-2">{{ amountText(item) }}</span>
            </v-list-item-title>
            <v-list-item-subtitle>{{ item.recipes.join(', ') }}</v-list-item-subtitle>
          </v-list-item>
        </v-list>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn
          variant="text"
          prepend-icon="mdi-content-copy"
          :disabled="sorted.length === 0"
          @click="copyList"
        >{{ copied ? ui.t.copied : ui.t.copy }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { plannerApi } from '../api/planner'
import type { IngredientSummaryDto } from '../api/types'
import { useUiStore } from '../stores/ui'

const props = defineProps<{ modelValue: boolean; user?: string; shareToken?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const ui = useUiStore()

/** YYYY-MM-DD in local time (toISOString would shift the day around midnight). */
function isoDate(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function defaultRange() {
  const start = new Date()
  const end = new Date(start)
  end.setDate(start.getDate() + 6)
  return { from: isoDate(start), to: isoDate(end) }
}

const from = ref(defaultRange().from)
const to = ref(defaultRange().to)
const items = ref<IngredientSummaryDto[]>([])
const loading = ref(false)
const checked = ref(new Set<string>())
const copied = ref(false)
let requestId = 0

const sorted = computed(() =>
  [...items.value].sort((a, b) => a.name.localeCompare(b.name, ui.locale, { sensitivity: 'base' })),
)

function amountText(item: IngredientSummaryDto) {
  const fmt = new Intl.NumberFormat(ui.locale === 'cs' ? 'cs-CZ' : 'en-US', { maximumFractionDigits: 2, useGrouping: false })
  return [
    ...item.totals.map((t) => (t.unit ? `${fmt.format(t.quantity)} ${t.unit}` : fmt.format(t.quantity))),
    ...item.otherAmounts,
  ].join(' + ')
}

async function load() {
  if (!from.value || !to.value || to.value < from.value) {
    items.value = []
    return
  }
  const id = ++requestId
  loading.value = true
  try {
    const result = await plannerApi.getIngredients(from.value, to.value, props.user, props.shareToken)
    // Ignore responses to an older date range that arrive after a newer one.
    if (id === requestId) items.value = result
  } finally {
    if (id === requestId) loading.value = false
  }
}

function toggle(name: string) {
  const next = new Set(checked.value)
  if (next.has(name)) next.delete(name)
  else next.add(name)
  checked.value = next
}

/** Copies the items not yet ticked off, one per line. */
async function copyList() {
  const text = sorted.value
    .filter((i) => !checked.value.has(i.name))
    .map((i) => {
      const amount = amountText(i)
      return amount ? `${i.name}: ${amount}` : i.name
    })
    .join('\n')
  await navigator.clipboard.writeText(text)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

function close() {
  emit('update:modelValue', false)
}

watch(() => props.modelValue, (open) => {
  if (!open) return
  const range = defaultRange()
  from.value = range.from
  to.value = range.to
  checked.value = new Set()
  load()
}, { immediate: true })

watch([from, to], () => {
  if (props.modelValue) load()
})
</script>
