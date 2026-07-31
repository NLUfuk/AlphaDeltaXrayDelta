import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import './assets/mdi/css/materialdesignicons.min.css'
import './index.css'
import { AuthProvider } from './lib/auth'
import Login from './screens/Login'
import Shell from './screens/Shell'
import Home from './screens/Home'
import TicketDetail from './screens/TicketDetail'
import PublicForm from './screens/PublicForm'
import AcceptInvite from './screens/AcceptInvite'
import Register from './screens/Register'
import Settings from './screens/Settings'
import Dashboard from './screens/Dashboard'
import AdminUsers from './screens/admin/AdminUsers'
import Companies from './screens/admin/Companies'
import Permissions from './screens/admin/Permissions'
import Columns from './screens/admin/Columns'
import Moderation from './screens/Moderation'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

// SPA data router. Protected screens hang off Shell; more Faz 7 slices plug into its children.
const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  { path: '/register', element: <Register /> },
  { path: '/invite', element: <AcceptInvite /> },
  { path: '/form/:slug', element: <PublicForm /> },
  {
    path: '/',
    element: <Shell />,
    children: [
      { index: true, element: <Home /> },
      { path: 'tickets/:id', element: <TicketDetail /> },
      { path: 'moderation', element: <Moderation /> },
      { path: 'admin/columns', element: <Columns /> },
      { path: 'reports', element: <Dashboard /> },
      { path: 'settings', element: <Settings /> },
      { path: 'admin/users', element: <AdminUsers /> },
      { path: 'admin/companies', element: <Companies /> },
      { path: 'admin/permissions', element: <Permissions /> },
    ],
  },
])

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RouterProvider router={router} />
      </AuthProvider>
    </QueryClientProvider>
  </StrictMode>,
)
