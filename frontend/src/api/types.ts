export interface RecipeDto {
  guid: string
  name: string
  category?: string
  duration?: number
  durationText: string
  servings: number
  lastCooked?: string
  isFavorite: boolean
  tags: string[]
  ingredients: IngredientDto[]
  steps: StepDto[]
  /** Resolved [[wiki links]] occurring in this recipe's steps. Only present on detail requests. */
  links: RecipeLinkDto[]
  /** Recipes whose steps link to this one. Only present on authenticated detail requests. */
  usedIn: RecipeRefDto[]
}

export interface RecipeLinkDto {
  /** The text between the brackets, as the user typed it. */
  text: string
  name: string
  guid: string
}

export interface RecipeRefDto {
  guid: string
  name: string
}

export interface IngredientDto {
  id: number
  name: string
  amount?: string
  order: number
}

export interface StepDto {
  id: number
  description: string
  order: number
  durationSeconds?: number
  stepType: 'Active' | 'SemiPassive' | 'Passive'
}

export interface PlannedRecipeDto {
  id: number
  recipeId: number
  recipeGuid: string
  recipeName: string
  date: string
  fromFridge: boolean
}

export interface CreateRecipeRequest {
  name: string
  category?: string
  duration?: number
  servings: number
}

export interface UpdateRecipeRequest {
  name: string
  category?: string
  duration?: number
  servings: number
}

export interface CreateIngredientRequest {
  name: string
  amount?: string
}

export interface UpdateIngredientRequest {
  name: string
  amount?: string
}

export interface CreateStepRequest {
  description: string
  durationSeconds?: number
  stepType: string
}

export interface UpdateStepRequest {
  description: string
  durationSeconds?: number
  stepType: string
}

export interface CreatePlannedRecipeRequest {
  recipeGuid: string
  date: string
  fromFridge: boolean
}

export interface UpdatePlannedRecipeRequest {
  date: string
  fromFridge: boolean
}
