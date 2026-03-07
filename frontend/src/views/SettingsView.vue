<template>
  <div>
    <h1>Settings</h1>

    <section style="margin-bottom: 2rem;">
      <h2>API Key</h2>
      <p>Use a bearer token to access the API from scripts or Home Assistant.</p>
      <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
        <button @click="generateApiToken" :disabled="apiTokenBusy">Generate Token</button>
        <button @click="revokeApiToken" :disabled="apiTokenBusy">Revoke Token</button>
      </div>
      <div v-if="newApiToken" style="margin-top: 0.75rem;">
        <pre style="background: #f4f4f4; padding: 0.5rem; word-break: break-all; margin: 0;">{{ newApiToken }}</pre>
        <p style="color: #888; font-size: 0.85em; margin: 0.25rem 0 0;">Save this token now — it won't be shown again.</p>
      </div>
      <p v-if="apiTokenMessage" :style="{ color: apiTokenMessage.ok ? 'green' : 'red', marginTop: '0.5rem' }">
        {{ apiTokenMessage.text }}
      </p>
    </section>

    <section>
      <h2>Share Access</h2>
      <p>
        Generate a share token to give others read-only access to your recipes and planner.
        Anyone with the link can browse your data.
      </p>
      <div v-if="shareToken === undefined">Loading...</div>
      <div v-else-if="shareToken">
        <p style="margin-bottom: 0.5rem;">Shareable link:</p>
        <div style="display: flex; gap: 0.5rem; align-items: center; flex-wrap: wrap;">
          <input :value="shareUrl" readonly style="flex: 1; min-width: 300px;"
            @focus="($event.target as HTMLInputElement).select()" />
          <button @click="copyShareUrl">{{ shareTokenCopied ? 'Copied!' : 'Copy' }}</button>
        </div>
        <button style="margin-top: 0.5rem;" @click="revokeShareToken" :disabled="shareTokenBusy">Revoke</button>
      </div>
      <div v-else>
        <button @click="generateShareToken" :disabled="shareTokenBusy">Generate Share Link</button>
      </div>
      <p v-if="shareTokenError" style="color: red; margin-top: 0.5rem;">{{ shareTokenError }}</p>
    </section>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuthStore } from '../stores/auth'
import { authApi } from '../api/auth'

const auth = useAuthStore()

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
