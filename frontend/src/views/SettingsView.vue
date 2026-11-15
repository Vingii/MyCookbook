<template>
  <div>
    <h1 class="text-h4 mb-6">{{ ui.t.settings }}</h1>

    <v-alert v-if="auth.isReadOnly" type="info" variant="tonal" data-test="read-only">{{ ui.t.readOnlySettings }}</v-alert>

    <template v-else>
    <v-card class="mb-6" variant="outlined">
      <v-card-title>{{ ui.t.apiKey }}</v-card-title>
      <v-card-text>
        <p class="mb-3">{{ ui.t.apiKeyDesc }}</p>
        <div class="d-flex ga-2 flex-wrap mb-3">
          <v-btn :disabled="apiTokenBusy" @click="generateApiToken">{{ ui.t.generateToken }}</v-btn>
          <v-btn :disabled="apiTokenBusy" @click="revokeApiToken">{{ ui.t.revokeToken }}</v-btn>
        </div>
        <v-alert v-if="newApiToken" type="info" density="compact" class="mb-2">
          <code style="word-break: break-all; font-family: monospace;">{{ newApiToken }}</code>
          <div class="text-caption mt-1">{{ ui.t.saveTokenNote }}</div>
        </v-alert>
        <v-alert
          v-if="apiTokenMessage"
          :type="apiTokenMessage.ok ? 'success' : 'error'"
          density="compact"
        >
          {{ apiTokenMessage.text }}
        </v-alert>
      </v-card-text>
    </v-card>

    <v-card variant="outlined">
      <v-card-title>{{ ui.t.shareAccess }}</v-card-title>
      <v-card-text>
        <p class="mb-3">{{ ui.t.shareAccessDesc }}</p>
        <div v-if="shareToken === undefined">Loading...</div>
        <div v-else-if="shareToken">
          <p class="mb-2">{{ ui.t.shareLink }}</p>
          <div class="d-flex ga-2 align-center flex-wrap mb-2">
            <v-text-field
              :model-value="shareUrl"
              readonly
              density="compact"
              variant="outlined"
              hide-details
              style="min-width: 300px; flex: 1;"
              @focus="($event.target as HTMLInputElement).select()"
            />
            <v-btn @click="copyShareUrl">{{ shareTokenCopied ? ui.t.copied : ui.t.copy }}</v-btn>
          </div>
          <v-btn :disabled="shareTokenBusy" @click="revokeShareToken">{{ ui.t.revoke }}</v-btn>
        </div>
        <div v-else>
          <v-btn color="primary" :disabled="shareTokenBusy" @click="generateShareToken">
            {{ ui.t.generateShareLink }}
          </v-btn>
        </div>
        <v-alert v-if="shareTokenError" type="error" density="compact" class="mt-3">
          {{ shareTokenError }}
        </v-alert>
      </v-card-text>
    </v-card>
    <v-card class="mt-6" variant="outlined" data-test="ha-card">
      <v-card-title>{{ ui.t.homeAssistant }}</v-card-title>
      <v-card-text>
        <p class="mb-3">{{ ui.t.homeAssistantDesc }}</p>
        <v-text-field
          v-model="haUrl"
          :label="ui.t.homeAssistantWebhookLabel"
          :hint="ui.t.homeAssistantWebhookHint"
          persistent-hint
          variant="outlined"
          density="compact"
          type="url"
          class="mb-3"
          data-test="ha-url"
          :error-messages="haError ? [haError] : []"
          @update:model-value="haSaved = false"
        />
        <div class="d-flex ga-2 flex-wrap mb-3">
          <v-btn color="primary" :disabled="haBusy || !haUrl.trim()" data-test="ha-save" @click="saveHomeAssistant">
            {{ haSaved ? ui.t.homeAssistantSaved : ui.t.save }}
          </v-btn>
          <v-btn v-if="haStoredUrl" :disabled="haBusy" data-test="ha-remove" @click="removeHomeAssistant">{{ ui.t.remove }}</v-btn>
        </div>
        <v-expansion-panels variant="accordion" flat>
          <v-expansion-panel :title="ui.t.homeAssistantSetup" class="border">
            <v-expansion-panel-text>
              <p class="mb-3 text-body-2">{{ ui.t.homeAssistantSetupSteps }}</p>
              <pre class="ha-yaml pa-3 mb-2" data-test="ha-yaml">{{ haYaml }}</pre>
              <v-btn size="small" prepend-icon="mdi-content-copy" @click="copyHaYaml">{{ haYamlCopied ? ui.t.copied : ui.t.copy }}</v-btn>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
      </v-card-text>
    </v-card>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useUiStore } from '../stores/ui'
import { authApi } from '../api/auth'
import { homeAssistantApi } from '../api/homeAssistant'

const auth = useAuthStore()
const ui = useUiStore()

// --- API Token ---
const newApiToken = ref<string | null>(null)
const apiTokenBusy = ref(false)
const apiTokenMessage = ref<{ ok: boolean; text: string } | null>(null)

async function generateApiToken() {
  apiTokenBusy.value = true
  apiTokenMessage.value = null
  newApiToken.value = null
  try {
    const res = await authApi.getToken()
    if (res.token) {
      newApiToken.value = res.token
    } else {
      apiTokenMessage.value = { ok: false, text: res.message ?? 'Unknown error.' }
    }
  } catch {
    apiTokenMessage.value = { ok: false, text: 'Failed to generate token.' }
  } finally {
    apiTokenBusy.value = false
  }
}

async function revokeApiToken() {
  apiTokenBusy.value = true
  apiTokenMessage.value = null
  newApiToken.value = null
  try {
    await authApi.revokeToken()
    apiTokenMessage.value = { ok: true, text: 'Token revoked.' }
  } catch {
    apiTokenMessage.value = { ok: false, text: 'Failed to revoke token.' }
  } finally {
    apiTokenBusy.value = false
  }
}

// --- Share Token ---
const shareToken = ref<string | null | undefined>(undefined)
const shareTokenBusy = ref(false)
const shareTokenError = ref<string | null>(null)
const shareTokenCopied = ref(false)

const shareUrl = computed(() => {
  if (!shareToken.value || !auth.username) return ''
  const base = window.location.origin
  return `${base}/browser?user=${encodeURIComponent(auth.username)}&shareToken=${encodeURIComponent(shareToken.value)}`
})

async function loadShareToken() {
  try {
    const res = await authApi.getShareToken()
    shareToken.value = res.token
  } catch {
    shareTokenError.value = 'Failed to load share token.'
    shareToken.value = null
  }
}

async function generateShareToken() {
  shareTokenBusy.value = true
  shareTokenError.value = null
  try {
    const res = await authApi.createShareToken()
    shareToken.value = res.token
  } catch {
    shareTokenError.value = 'Failed to generate share token.'
  } finally {
    shareTokenBusy.value = false
  }
}

async function revokeShareToken() {
  shareTokenBusy.value = true
  shareTokenError.value = null
  try {
    await authApi.revokeShareToken()
    shareToken.value = null
  } catch {
    shareTokenError.value = 'Failed to revoke share token.'
  } finally {
    shareTokenBusy.value = false
  }
}

function copyShareUrl() {
  navigator.clipboard.writeText(shareUrl.value)
  shareTokenCopied.value = true
  setTimeout(() => { shareTokenCopied.value = false }, 2000)
}

// --- Home Assistant ---
const haUrl = ref('')
const haStoredUrl = ref<string | null>(null)
const haBusy = ref(false)
const haError = ref<string | null>(null)
const haSaved = ref(false)
const haYamlCopied = ref(false)
// Suggested webhook ID for a new setup; long and random because knowing it is enough to add items.
const suggestedWebhookId = `mycookbook-${Array.from(crypto.getRandomValues(new Uint8Array(12)), (b) => b.toString(16).padStart(2, '0')).join('')}`

const webhookId = computed(() => /\/api\/webhook\/([A-Za-z0-9_-]+)\/?$/.exec(haUrl.value.trim())?.[1] ?? suggestedWebhookId)

const haYaml = computed(() => [
  'alias: MyCookbook shopping list',
  'triggers:',
  '  - trigger: webhook',
  `    webhook_id: ${webhookId.value}`,
  '    allowed_methods: [POST]',
  '    local_only: true',
  'actions:',
  '  - repeat:',
  // trigger.json.items would be the dict method; index it instead.
  '      for_each: "{{ trigger.json[\'items\'] }}"',
  '      sequence:',
  '        - action: todo.add_item',
  '          target:',
  '            entity_id: todo.shopping_list',
  '          data:',
  '            item: "{{ repeat.item.summary }}"',
  'mode: queued',
].join('\n'))

async function loadHomeAssistant() {
  try {
    haStoredUrl.value = (await homeAssistantApi.get()).webhookUrl
    haUrl.value = haStoredUrl.value ?? ''
  } catch {
    haStoredUrl.value = null
  }
}

async function saveHomeAssistant() {
  haBusy.value = true
  haError.value = null
  try {
    await homeAssistantApi.save(haUrl.value.trim())
    haStoredUrl.value = haUrl.value.trim()
    haSaved.value = true
  } catch (e) {
    const status = (e as { response?: { status?: number } }).response?.status
    haError.value = status === 400 ? ui.t.homeAssistantInvalidUrl : ui.t.homeAssistantSaveFailed
  } finally {
    haBusy.value = false
  }
}

async function removeHomeAssistant() {
  haBusy.value = true
  haError.value = null
  try {
    await homeAssistantApi.remove()
    haStoredUrl.value = null
    haUrl.value = ''
    haSaved.value = false
  } catch {
    haError.value = ui.t.homeAssistantSaveFailed
  } finally {
    haBusy.value = false
  }
}

function copyHaYaml() {
  navigator.clipboard.writeText(haYaml.value)
  haYamlCopied.value = true
  setTimeout(() => { haYamlCopied.value = false }, 2000)
}

onMounted(() => {
  // A read-only API token can't read or change these settings.
  if (auth.isReadOnly) return
  loadShareToken()
  loadHomeAssistant()
})
</script>

<style scoped>
.ha-yaml {
  background: rgba(128, 128, 128, 0.12);
  border-radius: 4px;
  font-size: 0.8rem;
  overflow-x: auto;
  white-space: pre;
}
</style>
