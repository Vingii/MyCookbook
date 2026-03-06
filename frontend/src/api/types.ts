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
