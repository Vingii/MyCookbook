import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'

export function useReadonly() {
  const auth = useAuthStore()
  const route = useRoute()

  const viewingUser = computed(() =>
    (route.query.user as string | undefined) ?? auth.username ?? ''
  )

  const shareToken = computed(() =>
    (route.query.shareToken as string | undefined) ?? undefined
  )

  const readonly = computed(() =>
    auth.isGuest || (auth.username !== null && viewingUser.value !== auth.username)
  )

  return { viewingUser, shareToken, readonly }
}
