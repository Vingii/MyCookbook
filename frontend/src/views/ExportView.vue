<template>
  <div>
    <h1 class="text-h4 mb-6">Export / Import</h1>

    <v-card class="mb-6" variant="outlined">
      <v-card-title>Export</v-card-title>
      <v-card-text>
        <p class="mb-3">Download all your recipes as a JSON file.</p>
        <v-btn color="primary" prepend-icon="mdi-download" :href="'/api/export'" download="cookbook-export.json">
          Download Export
        </v-btn>
      </v-card-text>
    </v-card>

    <v-card v-if="!readonly" variant="outlined">
      <v-card-title>Import</v-card-title>
      <v-card-text>
        <p class="mb-3">Import recipes from a JSON file. This will replace all existing recipes.</p>
        <v-file-input
          accept=".json"
          label="Select JSON file"
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
          {{ importing ? 'Importing...' : 'Import' }}
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

const { readonly } = useReadonly()
const selectedFile = ref<File | null>(null)
const importing = ref(false)
const importResult = ref<{ success: boolean; message: string } | null>(null)

function onFileSelected(e: Event) {
  selectedFile.value = (e.target as HTMLInputElement).files?.[0] ?? null
}

async function importFile() {
  if (!selectedFile.value) return
  if (!confirm('This will replace all your recipes. Continue?')) return
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
