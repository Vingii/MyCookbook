import client from './client'

export const tagsApi = {
  getAll: () =>
    client.get<string[]>('/tags').then((r) => r.data),
}
