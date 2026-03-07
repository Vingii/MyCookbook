<template>
  <div>
    <h1>Export / Import</h1>

    <section style="margin-bottom: 2rem;">
      <h2>Export</h2>
      <p>Download all your recipes as a JSON file.</p>
      <a href="/api/export" download="cookbook-export.json">
        <button>Download Export</button>
      </a>
    </section>

    <section v-if="!readonly">
      <h2>Import</h2>
      <p>Import recipes from a JSON file. This will replace all existing recipes.</p>
      <input type="file" accept=".json" @change="onFileSelected" />
      <button @click="importFile" :disabled="!selectedFile || importing">
        {{ importing ? 'Importing...' : 'Import' }}
      </button>
      <p v-if="importResult" :style="{ color: importResult.success ? 'green' : 'red' }">
        {{ importResult.message }}
      </p>
    </section>
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
