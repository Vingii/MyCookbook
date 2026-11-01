<template>
  <v-dialog :model-value="modelValue" max-width="720" scrollable @update:model-value="close">
    <v-card>
      <v-card-title class="d-flex align-center justify-space-between">
        <span>{{ ui.t.importTitle }}</span>
        <v-btn icon="mdi-close" variant="text" density="comfortable" :aria-label="ui.t.close" @click="close" />
      </v-card-title>

      <v-card-text>
        <!-- Step 1: the link -->
        <template v-if="!preview">
          <p class="mb-4 text-medium-emphasis">{{ ui.t.importIntro }}</p>
          <v-text-field
            v-model="url"
            :label="ui.t.importUrlLabel"
            placeholder="https://"
            prepend-inner-icon="mdi-link-variant"
            variant="outlined"
            type="url"
            autofocus
            :disabled="loading"
            :error-messages="error ? [error] : []"
            @keyup.enter="load"
          />
          <div v-if="loading" class="d-flex align-center ga-3 text-medium-emphasis">
            <v-progress-circular indeterminate color="primary" size="24" />
            <span>{{ ui.t.importLoading }}</span>
          </div>
        </template>

        <!-- Step 2: choose a version -->
        <template v-else>
          <v-btn-toggle v-model="selectedKey" mandatory color="primary" variant="outlined" density="comfortable" class="mb-1">
            <v-btn v-for="variant in preview.variants" :key="variant.key" :value="variant.key">
              {{ variant.key === 'suggested' ? ui.t.importSuggested : ui.t.importOriginal }}
            </v-btn>
          </v-btn-toggle>
          <p class="text-caption text-medium-emphasis mb-4">
            {{ selectedKey === 'suggested' ? ui.t.importSuggestedHint : ui.t.importOriginalHint }}
          </p>

          <template v-if="selected">
            <h2 class="text-h5 mb-2">{{ selected.name }}</h2>
            <div class="d-flex flex-wrap ga-6 mb-3 text-body-2">
              <div v-if="selected.category">
                <div class="text-caption text-medium-emphasis">{{ ui.t.category }}</div>
                {{ selected.category }}
              </div>
              <div v-if="selected.duration">
                <div class="text-caption text-medium-emphasis">{{ ui.t.colDuration }}</div>
                {{ selected.duration }} min
              </div>
              <div>
                <div class="text-caption text-medium-emphasis">{{ ui.t.servings }}</div>
                {{ selected.servings }}
              </div>
            </div>
            <div v-if="selected.tags.length" class="d-flex flex-wrap ga-2 mb-4">
              <v-chip v-for="tag in selected.tags" :key="tag" size="small">{{ tag }}</v-chip>
            </div>

            <h3 class="text-subtitle-1 font-weight-bold">{{ ui.t.ingredients }}</h3>
            <ul class="mb-4 pl-5 import-ingredients">
              <li v-for="(ingredient, i) in selected.ingredients" :key="i">
                <span v-if="ingredient.amount" class="font-weight-medium">{{ ingredient.amount }}</span>
                {{ ingredient.name }}
              </li>
            </ul>

            <h3 class="text-subtitle-1 font-weight-bold">{{ ui.t.steps }}</h3>
            <ol class="pl-5 import-steps">
              <li v-for="(step, i) in selected.steps" :key="i" class="mb-1">
                {{ step.description }}
                <span v-if="step.durationMinutes" class="text-caption text-medium-emphasis text-no-wrap">
                  · {{ step.durationMinutes }} min
                </span>
              </li>
            </ol>
          </template>

          <p class="text-caption text-medium-emphasis mt-4 text-truncate">
            {{ ui.t.importSource }}: <a :href="preview.sourceUrl" target="_blank" rel="noopener noreferrer">{{ preview.sourceUrl }}</a>
          </p>
          <v-alert v-if="error" type="error" density="compact" class="mt-2">{{ error }}</v-alert>
        </template>
      </v-card-text>

      <v-card-actions>
        <v-btn v-if="preview" variant="text" :disabled="saving" @click="reset">{{ ui.t.importBack }}</v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">{{ ui.t.cancel }}</v-btn>
        <v-btn v-if="!preview" color="primary" variant="flat" :loading="loading" :disabled="!url.trim()" @click="load">
          {{ ui.t.importLoad }}
        </v-btn>
        <v-btn v-else color="primary" variant="flat" :loading="saving" @click="save">{{ ui.t.importSave }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { isAxiosError } from 'axios'
import { recipeImportApi, type RecipeImportPreview } from '../api/recipeImport'
import { useUiStore } from '../stores/ui'

defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean]; created: [guid: string] }>()

const ui = useUiStore()
const url = ref('')
const loading = ref(false)
const saving = ref(false)
const error = ref<string | null>(null)
const preview = ref<RecipeImportPreview | null>(null)
const selectedKey = ref<string>('suggested')

const selected = computed(() => preview.value?.variants.find((v) => v.key === selectedKey.value)?.recipe ?? null)

/** The server answers with a stable error code; anything else (network, timeout) is a generic failure. */
function errorMessage(e: unknown) {
  const code = isAxiosError(e) ? (e.response?.data as { code?: string } | undefined)?.code : undefined
  switch (code) {
    case 'invalid_url': return ui.t.importErrorInvalidUrl
    case 'unreachable': return ui.t.importErrorUnreachable
    case 'not_html': return ui.t.importErrorNotHtml
    case 'too_large': return ui.t.importErrorTooLarge
    case 'no_recipe': return ui.t.importErrorNoRecipe
    default: return ui.t.importErrorFailed
  }
}

async function load() {
  if (!url.value.trim() || loading.value) return
  loading.value = true
  error.value = null
  try {
    preview.value = await recipeImportApi.preview(url.value.trim(), ui.locale)
    selectedKey.value = preview.value.variants[0]!.key
  } catch (e) {
    error.value = errorMessage(e)
  } finally {
    loading.value = false
  }
}

async function save() {
  if (!selected.value) return
  saving.value = true
  error.value = null
  try {
    const created = await recipeImportApi.save(selected.value)
    emit('created', created.guid)
    close()
  } catch {
    error.value = ui.t.importErrorFailed
  } finally {
    saving.value = false
  }
}

function reset() {
  preview.value = null
  error.value = null
}

function close() {
  emit('update:modelValue', false)
  url.value = ''
  reset()
}
</script>

<style scoped>
.import-ingredients,
.import-steps {
  line-height: 1.6;
}
</style>
