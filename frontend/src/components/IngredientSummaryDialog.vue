<template>
  <v-dialog :model-value="modelValue" max-width="560" scrollable @update:model-value="close">
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between">
        <span>{{ ui.t.shoppingList }}</span>
        <v-btn icon="mdi-close" variant="text" density="comfortable" :aria-label="ui.t.close" @click="close" />
      </v-card-title>

      <v-card-text>
        <div class="d-flex ga-3 mb-3">
          <v-menu
            v-for="field in dateFields"
            :key="field.key"
            v-model="menus[field.key]"
            :close-on-content-click="false"
            min-width="auto"
          >
            <template #activator="{ props: activator }">
              <v-text-field
                v-bind="activator"
                :model-value="formatDate(field.value)"
                :label="field.label"
                prepend-inner-icon="mdi-calendar"
                variant="outlined"
                density="compact"
                hide-details
                readonly
                :data-test="field.key"
              />
            </template>
            <v-locale-provider :locale="ui.locale">
              <v-date-picker
                :model-value="parseIsoDate(field.value)"
                :first-day-of-week="firstDay"
                :min="field.key === 'to' ? from : undefined"
                hide-header
                :data-test="`${field.key}-picker`"
                @update:model-value="(d: unknown) => pick(field.key, d)"
              />
            </v-locale-provider>
          </v-menu>
        </div>

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

        <v-alert
          v-if="haMessage"
          :type="haMessage.ok ? 'success' : 'error'"
          density="compact"
          class="mt-3"
          data-test="ha-message"
        >{{ haMessage.text }}</v-alert>
      </v-card-text>

      <v-card-actions class="flex-wrap">
        <v-spacer />
        <v-btn
          v-if="haConfigured"
          variant="text"
          prepend-icon="mdi-home-assistant"
          :loading="haSending"
          :disabled="remaining.length === 0"
          data-test="send-ha"
          @click="sendToHomeAssistant"
        >{{ ui.t.sendToHomeAssistant }}</v-btn>
        <v-btn
          variant="text"
          prepend-icon="mdi-content-copy"
          :disabled="remaining.length === 0"
          data-test="copy"
          @click="copyList"
        >{{ copied ? ui.t.copied : ui.t.copy }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch, reactive } from 'vue'
import { plannerApi } from '../api/planner'
import { homeAssistantApi } from '../api/homeAssistant'
import type { IngredientSummaryDto } from '../api/types'
import { useUiStore } from '../stores/ui'
import { useAuthStore } from '../stores/auth'
import { firstDayOfWeek, isoDate, localeTag, nextWeekRange, parseIsoDate } from '../utils/dates'

const props = defineProps<{ modelValue: boolean; user?: string; shareToken?: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const ui = useUiStore()
const auth = useAuthStore()

const firstDay = computed(() => firstDayOfWeek(localeTag(ui.locale)))

function defaultRange() {
  return nextWeekRange(new Date(), firstDay.value)
}

const from = ref(defaultRange().from)
const to = ref(defaultRange().to)
const menus = reactive({ from: false, to: false })
const items = ref<IngredientSummaryDto[]>([])
const loading = ref(false)
const checked = ref(new Set<string>())
const copied = ref(false)
const haConfigured = ref(false)
const haSending = ref(false)
const haMessage = ref<{ ok: boolean; text: string } | null>(null)
let requestId = 0

const dateFields = computed(() => [
  { key: 'from' as const, label: ui.t.shoppingListFrom, value: from.value },
  { key: 'to' as const, label: ui.t.shoppingListTo, value: to.value },
])

function formatDate(iso: string) {
  return parseIsoDate(iso).toLocaleDateString(localeTag(ui.locale), { weekday: 'short', day: 'numeric', month: 'numeric', year: 'numeric' })
}

function pick(key: 'from' | 'to', value: unknown) {
  if (!(value instanceof Date)) return
  const iso = isoDate(value)
  if (key === 'from') {
    from.value = iso
    // Keep the range valid: moving the start past the end moves the end along.
    if (to.value < iso) to.value = iso
  } else {
    to.value = iso
  }
  menus[key] = false
}

const sorted = computed(() =>
  [...items.value].sort((a, b) => a.name.localeCompare(b.name, ui.locale, { sensitivity: 'base' })),
)

/** Items not ticked off yet: what is copied or sent to Home Assistant. */
const remaining = computed(() => sorted.value.filter((i) => !checked.value.has(i.name)))

function amountText(item: IngredientSummaryDto) {
  const fmt = new Intl.NumberFormat(localeTag(ui.locale), { maximumFractionDigits: 2, useGrouping: false })
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

async function loadHomeAssistant() {
  haConfigured.value = false
  if (auth.isGuest) return
  try {
    haConfigured.value = !!(await homeAssistantApi.get()).webhookUrl
  } catch {
    // No button if the setting can't be read.
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
  const text = remaining.value
    .map((i) => {
      const amount = amountText(i)
      return amount ? `${i.name}: ${amount}` : i.name
    })
    .join('\n')
  await navigator.clipboard.writeText(text)
  copied.value = true
  setTimeout(() => { copied.value = false }, 2000)
}

async function sendToHomeAssistant() {
  const list = remaining.value.map((i) => ({ name: i.name, amount: amountText(i), recipes: i.recipes }))
  haSending.value = true
  haMessage.value = null
  try {
    await homeAssistantApi.sendShoppingList(list)
    haMessage.value = { ok: true, text: ui.t.sentToHomeAssistant.replace('{n}', String(list.length)) }
  } catch {
    haMessage.value = { ok: false, text: ui.t.sendToHomeAssistantFailed }
  } finally {
    haSending.value = false
  }
}

function close() {
  emit('update:modelValue', false)
}

watch(() => props.modelValue, (open) => {
  if (!open) return
  const range = defaultRange()
  checked.value = new Set()
  haMessage.value = null
  loadHomeAssistant()
  // Changing the range reloads through the watcher below; only load directly if it stayed the same.
  if (from.value === range.from && to.value === range.to) load()
  from.value = range.from
  to.value = range.to
}, { immediate: true })

watch([from, to], () => {
  if (props.modelValue) load()
})
</script>
