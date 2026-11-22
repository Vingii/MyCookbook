import client from './client'
import type { CookbookDto, SaveCookbookRequest } from './types'

export const cookbooksApi = {
  getAll: () => client.get<CookbookDto[]>('/cookbooks').then((r) => r.data),

  create: (req: SaveCookbookRequest) => client.post<CookbookDto>('/cookbooks', req).then((r) => r.data),

  update: (id: number, req: SaveCookbookRequest) => client.put(`/cookbooks/${id}`, req),

  delete: (id: number) => client.delete(`/cookbooks/${id}`),
}
