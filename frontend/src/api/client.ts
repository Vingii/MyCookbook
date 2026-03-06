import axios from 'axios'

const client = axios.create({
  baseURL: '/api',
  withCredentials: true,
})

client.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      const returnUrl = encodeURIComponent(window.location.pathname + window.location.search)
      window.location.href = `/Account/Login?returnUrl=${returnUrl}`
    }
    return Promise.reject(error)
  }
)

export default client
