import { defineStore } from 'pinia'
import { ref } from 'vue'
import { authApi } from '../api/auth'

export const useAuthStore = defineStore('auth', () => {
  const username = ref<string | null>(null)
  const isAuthenticated = ref(false)
  const isGuest = ref(false)
  const loaded = ref(false)

  async function load() {
    if (loaded.value) return
    try {
      const me = await authApi.me()
      username.value = me.username
      isAuthenticated.value = me.isAuthenticated
      isGuest.value = me.isGuest ?? false
    } catch {
      isAuthenticated.value = false
    } finally {
      loaded.value = true
    }
  }

  return { username, isAuthenticated, isGuest, loaded, load }
})
