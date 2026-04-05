import axios from 'axios'

const client = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.location.href = '/api/auth/login?returnUrl=' + encodeURIComponent(window.location.href)
    }
    return Promise.reject(error)
  }
)

export default client
