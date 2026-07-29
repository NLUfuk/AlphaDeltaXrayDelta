import axios from 'axios'

// Single axios instance. Auth token/refresh interceptors are added in Faz 2 (auth).
export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})
