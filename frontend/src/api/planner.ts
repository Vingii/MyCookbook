import client from './client'
import type { PlannedRecipeDto, CreatePlannedRecipeRequest, UpdatePlannedRecipeRequest } from './types'

export const plannerApi = {
  getAll: (from?: string, to?: string, user?: string, shareToken?: string) =>
    client.get<PlannedRecipeDto[]>('/planner', { params: { from, to, user, shareToken } }).then((r) => r.data),

  create: (req: CreatePlannedRecipeRequest) =>
    client.post<PlannedRecipeDto>('/planner', req).then((r) => r.data),

  update: (id: number, req: UpdatePlannedRecipeRequest) =>
    client.put(`/planner/${id}`, req),

  delete: (id: number) =>
    client.delete(`/planner/${id}`),
}
