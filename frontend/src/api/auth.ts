import client from './client'

export const authApi = {
  me: () =>
    client.get<{ username: string; isAuthenticated: boolean; isGuest?: boolean }>('/auth/me').then((r) => r.data),

  getToken: () =>
    client.get<{ token?: string; message?: string }>('/auth/token').then((r) => r.data),

  revokeToken: () =>
    client.delete('/auth/token'),

  getShareToken: () =>
    client.get<{ token: string | null }>('/auth/share-token').then((r) => r.data),

  createShareToken: () =>
    client.post<{ token: string }>('/auth/share-token').then((r) => r.data),

  revokeShareToken: () =>
    client.delete('/auth/share-token'),
}
