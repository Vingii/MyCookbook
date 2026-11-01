import client from './client'

export type SharedItemKind = 'Recipe' | 'Cookbook'

/** A recipe or cookbook someone shared with the current user (MC-71). */
export interface SharedItemDto {
  id: number
  kind: SharedItemKind
  /** Owner's user id, for building ?user= links. */
  owner: string
  ownerName: string
  recipeGuid: string | null
  /** Null when the recipe no longer exists. */
  recipeName: string | null
  shareToken: string | null
  lastOpened: string
  pinned: boolean
  /** False once the recipe is deleted or the cookbook link revoked. */
  available: boolean
}

export const sharedApi = {
  getAll: () => client.get<SharedItemDto[]>('/shared').then((r) => r.data),

  recordRecipe: (guid: string) => client.post(`/shared/recipes/${guid}`),

  recordCookbook: (user: string, shareToken: string) => client.post('/shared/cookbooks', { user, shareToken }),

  setPinned: (id: number, pinned: boolean) => client.put(`/shared/${id}/pin`, { pinned }),

  remove: (id: number) => client.delete(`/shared/${id}`),
}
