import client from './client'

export const authApi = {
  me: () =>
    client.get<{ username: string; isAuthenticated: boolean }>('/auth/me').then((r) => r.data),

  getToken: () =>
    client.get<{ token?: string; message?: string }>('/auth/token').then((r) => r.data),

  revokeToken: () =>
    client.delete('/auth/token'),
}
