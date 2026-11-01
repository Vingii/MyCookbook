import client from './client'

/** A recipe someone shared with the current user (MC-71). */
export interface SharedItemDto {
  id: number
  ownerName: string
  recipeGuid: string
  /** Null when the recipe no longer exists. */
  recipeName: string | null
  lastOpened: string
  pinned: boolean
  /** False once the owner deleted the recipe. */
  available: boolean
}

export const sharedApi = {
  getAll: () => client.get<SharedItemDto[]>('/shared').then((r) => r.data),

  recordRecipe: (guid: string) => client.post(`/shared/recipes/${guid}`),

  setPinned: (id: number, pinned: boolean) => client.put(`/shared/${id}/pin`, { pinned }),

  remove: (id: number) => client.delete(`/shared/${id}`),
}
