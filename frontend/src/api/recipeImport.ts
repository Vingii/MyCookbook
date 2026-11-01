import client from './client'
import type { RecipeDto } from './types'

// MC-69: importing a recipe from a web page link with AI.

export type StepType = 'Active' | 'SemiPassive' | 'Passive'

/** A complete recipe that has not been saved yet. */
export interface RecipeDraft {
  name: string
  category: string | null
  servings: number
  duration: number | null
  tags: string[]
  ingredients: { name: string; amount: string | null }[]
  steps: { description: string; durationMinutes: number | null; stepType: StepType }[]
}

export interface RecipeImportVariant {
  /** "suggested" = translated and adapted to the user's conventions; "original" = as on the page. */
  key: 'suggested' | 'original'
  recipe: RecipeDraft
}

export interface RecipeImportPreview {
  sourceUrl: string
  variants: RecipeImportVariant[]
}

export const recipeImportApi = {
  /** False when no AI is configured on the server; the feature is then hidden. */
  isEnabled: () => client.get<{ enabled: boolean }>('/recipe-import/status').then((r) => r.data.enabled),

  // Reading a page with AI can take a while, so allow well beyond the usual request time.
  preview: (url: string, language: string) =>
    client.post<RecipeImportPreview>('/recipe-import/preview', { url, language }, { timeout: 150_000 }).then((r) => r.data),

  save: (recipe: RecipeDraft) => client.post<RecipeDto>('/recipe-import/save', recipe).then((r) => r.data),
}
