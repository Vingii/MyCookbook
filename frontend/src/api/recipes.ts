import client from './client'
import type {
  RecipeDto,
  CreateRecipeRequest,
  UpdateRecipeRequest,
  CreateIngredientRequest,
  UpdateIngredientRequest,
  CreateStepRequest,
  UpdateStepRequest,
} from './types'

export const recipesApi = {
  getAll: (params?: { search?: string; category?: string; tag?: string; user?: string; shareToken?: string }) =>
    client.get<RecipeDto[]>('/recipes', { params }).then((r) => r.data),

  getById: (guid: string, params?: { user?: string; shareToken?: string }) =>
    client.get<RecipeDto>(`/recipes/${guid}`, { params }).then((r) => r.data),

  getRandom: (params?: { user?: string; shareToken?: string }) =>
    client.get<RecipeDto>('/recipes/random', { params }).then((r) => r.data),

  getShared: (guid: string) =>
    client.get<RecipeDto>(`/recipes/shared/${guid}`).then((r) => r.data),

  create: (req: CreateRecipeRequest) =>
    client.post<RecipeDto>('/recipes', req).then((r) => r.data),

  update: (guid: string, req: UpdateRecipeRequest) =>
    client.put(`/recipes/${guid}`, req),

  delete: (guid: string) =>
    client.delete(`/recipes/${guid}`),

  clone: (guid: string) =>
    client.post<RecipeDto>(`/recipes/${guid}/clone`).then((r) => r.data),

  markCooked: (guid: string) =>
    client.post(`/recipes/${guid}/lastcooked`),

  addIngredient: (guid: string, req: CreateIngredientRequest) =>
    client.post(`/recipes/${guid}/ingredients`, req).then((r) => r.data),

  updateIngredient: (guid: string, id: number, req: UpdateIngredientRequest) =>
    client.put(`/recipes/${guid}/ingredients/${id}`, req),

  deleteIngredient: (guid: string, id: number) =>
    client.delete(`/recipes/${guid}/ingredients/${id}`),

  moveIngredientUp: (guid: string, id: number) =>
    client.post(`/recipes/${guid}/ingredients/${id}/up`),

  moveIngredientDown: (guid: string, id: number) =>
    client.post(`/recipes/${guid}/ingredients/${id}/down`),

  addStep: (guid: string, req: CreateStepRequest) =>
    client.post(`/recipes/${guid}/steps`, req).then((r) => r.data),

  updateStep: (guid: string, id: number, req: UpdateStepRequest) =>
    client.put(`/recipes/${guid}/steps/${id}`, req),

  deleteStep: (guid: string, id: number) =>
    client.delete(`/recipes/${guid}/steps/${id}`),

  moveStepUp: (guid: string, id: number) =>
    client.post(`/recipes/${guid}/steps/${id}/up`),

  moveStepDown: (guid: string, id: number) =>
    client.post(`/recipes/${guid}/steps/${id}/down`),

  addTag: (guid: string, name: string) =>
    client.post(`/recipes/${guid}/tags`, { name }),

  deleteTag: (guid: string, name: string) =>
    client.delete(`/recipes/${guid}/tags/${encodeURIComponent(name)}`),

  addFavorite: (guid: string) =>
    client.post(`/recipes/${guid}/favorite`),

  removeFavorite: (guid: string) =>
    client.delete(`/recipes/${guid}/favorite`),
}
