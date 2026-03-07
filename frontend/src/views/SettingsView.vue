<template>
  <div>
    <h1 class="text-h4 mb-6">{{ ui.t.settings }}</h1>

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
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { useUiStore } from '../stores/ui'
import { authApi } from '../api/auth'

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

onMounted(loadShareToken)
</script>
