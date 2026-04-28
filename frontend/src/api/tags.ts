import client from './client'

export const tagsApi = {
  getAll: () =>
    client.get<string[]>('/tags').then((r) => r.data),
}

export const categoriesApi = {
  getAll: () =>
    client.get<string[]>('/categories').then((r) => r.data),
}
