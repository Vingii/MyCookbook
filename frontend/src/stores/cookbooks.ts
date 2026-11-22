import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { cookbooksApi } from '../api/cookbooks'
import type { CookbookDto, RecipeDto } from '../api/types'
import { useUiStore } from './ui'

const STORAGE_KEY = 'activeCookbook'

function readStoredId(): number | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? Number(raw) || null : null
  } catch {
    return null
  }
}

/** The user's cookbooks (MC-80) and the one the recipe list shows, remembered per browser. */
export const useCookbooksStore = defineStore('cookbooks', () => {
  const cookbooks = ref<CookbookDto[]>([])
  const loaded = ref(false)
  const activeId = ref<number | null>(readStoredId())

  const active = computed(
    () => cookbooks.value.find((c) => c.id === activeId.value) ?? cookbooks.value[0] ?? null
  )
  /** With a single cookbook the app looks the way it did before cookbooks existed. */
  const hasSeveral = computed(() => cookbooks.value.length > 1)

  async function fetchAll() {
    cookbooks.value = await cookbooksApi.getAll()
    loaded.value = true
    // A remembered cookbook may have been deleted, possibly in another browser.
    if (!cookbooks.value.some((c) => c.id === activeId.value)) setActive(cookbooks.value[0]?.id ?? null)
  }

  /** Loads the cookbooks once; later calls reuse them. */
  async function ensureLoaded() {
    if (!loaded.value) await fetchAll()
  }

  function setActive(id: number | null) {
    activeId.value = id
    try {
      if (id == null) localStorage.removeItem(STORAGE_KEY)
      else localStorage.setItem(STORAGE_KEY, String(id))
    } catch {
      // Storage may be unavailable (private mode); the choice then lasts for this visit only.
    }
  }

  /** The name to show: the default cookbook is called "My recipes" until the user names it. */
  function displayName(cookbook: CookbookDto | null | undefined) {
    if (!cookbook) return ''
    return cookbook.name || useUiStore().t.defaultCookbookName
  }

  function recipeCountLabel(cookbook: CookbookDto) {
    return useUiStore().t.cookbookRecipeCount.replace('{n}', String(cookbook.recipeCount))
  }

  /** The cookbook a recipe belongs to (recipes without one are in the default cookbook). */
  function ofRecipe(recipe: Pick<RecipeDto, 'cookbookId'>) {
    return cookbooks.value.find((c) => c.id === recipe.cookbookId) ?? cookbooks.value.find((c) => c.isDefault) ?? null
  }

  /**
   * The cookbook new recipes (clones of shared ones) go to: the one you are browsing, or undefined (the default
   * cookbook) when you have just one or they can't be loaded.
   */
  async function targetForNewRecipe(): Promise<number | undefined> {
    try {
      await ensureLoaded()
    } catch {
      return undefined
    }
    return hasSeveral.value ? active.value?.id : undefined
  }

  return { cookbooks, loaded, activeId, active, hasSeveral, fetchAll, ensureLoaded, setActive, displayName, recipeCountLabel, ofRecipe, targetForNewRecipe }
})
