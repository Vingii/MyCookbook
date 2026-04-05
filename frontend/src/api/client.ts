import axios from 'axios'

const TOKEN_KEY = 'mycookbook_token'

export function storeToken(token: string) {
  localStorage.setItem(TOKEN_KEY, token)
}

const client = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

client.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY)
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.location.href = '/unauthorized'
    }
    return Promise.reject(error)
  }
)

export default client
