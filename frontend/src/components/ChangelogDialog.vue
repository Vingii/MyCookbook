<template>
  <v-dialog :model-value="modelValue" max-width="600" scrollable @update:model-value="$emit('update:modelValue', $event)">
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between">
        <span>{{ ui.t.whatsNew }}</span>
        <v-btn
            icon="mdi-close"
            variant="text"
            density="comfortable"
            @click="$emit('update:modelValue', false)"
        ></v-btn>
      </v-card-title>

      <v-card-text>
        <div v-if="loading" class="text-center pa-4">
          <v-progress-circular indeterminate color="primary" />
        </div>
        <div v-else>
          <v-card
              v-for="entry in displayEntries"
              :key="entry.version"
              variant="outlined"
              class="mb-3"
          >
            <v-card-title class="text-body-1 font-weight-bold">
              v{{ entry.version }}
              <span v-if="entry.releaseDate" class="text-caption text-medium-emphasis ml-2">
                {{ formatDate(entry.releaseDate) }}
              </span>
            </v-card-title>
            <v-card-text>
              <div v-html="entry.rawHtml" class="changelog-content" />
            </v-card-text>
          </v-card>
        </div>
      </v-card-text>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { changelogApi, type ChangelogEntry } from '../api/changelog'
import { useUiStore } from '../stores/ui'

const props = defineProps<{ modelValue: boolean; sinceVersion?: string | null }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const ui = useUiStore()
const entries = ref<ChangelogEntry[]>([])
const loading = ref(false)

watch(() => props.modelValue, async (open) => {
  if (open && entries.value.length === 0) {
    loading.value = true
    try {
      entries.value = await changelogApi.getEntries()
    } finally {
      loading.value = false
      if (props.sinceVersion != null && displayEntries.value.length === 0) {
        emit('update:modelValue', false)
      }
    }
  }
})

function compareVersions(a: string, b: string): number {
  const pa = a.split('.').map(Number)
  const pb = b.split('.').map(Number)
  for (let i = 0; i < 3; i++) {
    if ((pa[i] ?? 0) !== (pb[i] ?? 0)) return (pa[i] ?? 0) - (pb[i] ?? 0)
  }
  return 0
}

const displayEntries = computed(() =>
  props.sinceVersion
    ? entries.value.filter(e => compareVersions(e.version, props.sinceVersion!) > 0)
    : entries.value
)

function formatDate(d: string) {
  return new Date(d).toLocaleDateString(ui.locale === 'cs' ? 'cs-CZ' : 'en-US')
}
</script>

<style scoped>
.changelog-content :deep(ul) {
  padding-left: 1.5em;
}
.changelog-content :deep(h3) {
  font-size: 0.9rem;
  font-weight: 600;
  margin: 0.5em 0 0.25em;
}
</style>
