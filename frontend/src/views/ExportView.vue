<template>
  <div>
    <h1 class="text-h4 mb-6">{{ ui.t.exportImport }}</h1>

    <v-card class="mb-6" variant="outlined">
      <v-card-title>{{ ui.t.export }}</v-card-title>
      <v-card-text>
        <p class="mb-3">{{ ui.t.exportDesc }}</p>
        <v-btn color="primary" prepend-icon="mdi-download" :href="'/api/export'" download="cookbook-export.json">
          {{ ui.t.downloadExport }}
        </v-btn>
      </v-card-text>
    </v-card>

    <v-card v-if="!readonly" variant="outlined">
      <v-card-title>{{ ui.t.import }}</v-card-title>
      <v-card-text>
        <p class="mb-3">{{ ui.t.importDesc }}</p>
        <v-file-input
          accept=".json"
          label="JSON"
          density="compact"
          variant="outlined"
          hide-details
          class="mb-3"
          @change="onFileSelected"
        />
        <v-btn
          color="primary"
          prepend-icon="mdi-upload"
          :disabled="!selectedFile || importing"
          @click="importFile"
        >
          {{ importing ? ui.t.importing : ui.t.import }}
        </v-btn>
        <v-alert
          v-if="importResult"
          :type="importResult.success ? 'success' : 'error'"
          class="mt-3"
          density="compact"
        >
          {{ importResult.message }}
        </v-alert>
      </v-card-text>
    </v-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import client from '../api/client'
import { useReadonly } from '../composables/useReadonly'
import { useUiStore } from '../stores/ui'

const { readonly } = useReadonly()
const ui = useUiStore()
const selectedFile = ref<File | null>(null)
const importing = ref(false)
const importResult = ref<{ success: boolean; message: string } | null>(null)

function onFileSelected(e: Event) {
  selectedFile.value = (e.target as HTMLInputElement).files?.[0] ?? null
}

async function importFile() {
  if (!selectedFile.value) return
  if (!confirm(ui.t.importDesc)) return
  importing.value = true
  importResult.value = null
  try {
    const form = new FormData()
    form.append('file', selectedFile.value)
    await client.post('/import', form, { headers: { 'Content-Type': 'multipart/form-data' } })
    importResult.value = { success: true, message: 'Import successful!' }
  } catch (e: any) {
    importResult.value = { success: false, message: `Import failed: ${e.message}` }
  } finally {
    importing.value = false
  }
}
</script>
