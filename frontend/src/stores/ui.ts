import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { translations, type Locale } from '../i18n/translations'

export const useUiStore = defineStore('ui', () => {
  const theme = ref<'light' | 'dark'>(
    (localStorage.getItem('theme') as 'light' | 'dark') ?? 'light'
  )
  const locale = ref<Locale>(
    (localStorage.getItem('locale') as Locale) ?? 'en'
  )

  const t = computed(() => translations[locale.value])

  function toggleTheme() {
    theme.value = theme.value === 'light' ? 'dark' : 'light'
    localStorage.setItem('theme', theme.value)
  }

  function setLocale(l: Locale) {
    locale.value = l
    localStorage.setItem('locale', l)
  }

  return { theme, locale, t, toggleTheme, setLocale }
})
