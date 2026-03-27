<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="$emit('update:modelValue', $event)">
    <v-card>
      <v-card-title>{{ ui.t.bugReport }}</v-card-title>
      <v-card-text>
        <p class="text-body-2 mb-3 text-medium-emphasis">{{ ui.t.bugReportDesc }}</p>
        <v-textarea
          v-model="message"
          auto-grow
          rows="4"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-file-input
          v-model="files"
          multiple
          chips
          variant="outlined"
          density="compact"
          hide-details
          prepend-icon="mdi-paperclip"
          :label="ui.t.attachFiles"
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="cancel">{{ ui.t.cancel }}</v-btn>
        <v-btn color="primary" :disabled="!message.trim() || sending" :loading="sending" @click="submit">{{ ui.t.send }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { feedbackApi } from '../api/feedback'
import { useUiStore } from '../stores/ui'

defineProps<{ modelValue: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

const ui = useUiStore()
const message = ref('')
const files = ref<File[]>([])
const sending = ref(false)

async function submit() {
  if (!message.value.trim()) return
  sending.value = true
  try {
    await feedbackApi.send(message.value.trim(), files.value)
    message.value = ''
    files.value = []
    emit('update:modelValue', false)
  } finally {
    sending.value = false
  }
}

function cancel() {
  message.value = ''
  files.value = []
  emit('update:modelValue', false)
}
</script>
