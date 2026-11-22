import { computed } from 'vue'
import { useAuthStore } from '../stores/auth'

/** Whether the signed-in user can only look: guests, and sessions opened with a read-only API token. */
export function useReadonly() {
  const auth = useAuthStore()
  const readonly = computed(() => auth.isGuest || auth.isReadOnly)
  return { readonly }
}
