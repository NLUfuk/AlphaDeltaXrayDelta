import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createBrowserRouter } from 'react-router-dom'
import './assets/mdi/css/materialdesignicons.min.css'
import './index.css'
import { initTheme } from './lib/theme'
import { AuthProvider } from './lib/auth'

initTheme() // apply saved/system dark mode before first paint
import Login from './screens/Login'
import Shell from './screens/Shell'
import Home from './screens/Home'
import Kanban from './screens/Kanban'
import CustomerTickets from './screens/CustomerTickets'
import TicketDetail from './screens/TicketDetail'
import PublicForm from './screens/PublicForm'
import CustomerAccess from './screens/CustomerAccess'
import AcceptInvite from './screens/AcceptInvite'
import Register from './screens/Register'
import ForgotPassword from './screens/ForgotPassword'
import Settings from './screens/Settings'
import Dashboard from './screens/Dashboard'
import AdminUsers from './screens/admin/AdminUsers'
import Companies from './screens/admin/Companies'
import Permissions from './screens/admin/Permissions'
import Columns from './screens/admin/Columns'
import Moderation from './screens/Moderation'
import Revenue from './screens/Revenue'
import Templates from './screens/admin/Templates'
import FormFields from './screens/admin/FormFields'
import Account from './screens/Account'
import NotFound from './screens/NotFound'

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: 1, refetchOnWindowFocus: false } },
})

// SPA data router. Protected screens hang off Shell; more Faz 7 slices plug into its children.
//
// Everything sits under one PATHLESS root whose only job is `errorElement`: without it React Router
// falls back to its built-in developer screen ("Hey developer 👋 … ErrorBoundary") for any error
// thrown below, and the `*` child does the same for an address that matches nothing. A route without
// an `element` renders <Outlet/>, so the wrapper adds no markup.
const router = createBrowserRouter([
  {
    errorElement: <NotFound />,
    children: [
      { path: '/login', element: <Login /> },
      { path: '/register', element: <Register /> },
      { path: '/forgot-password', element: <ForgotPassword /> },
      { path: '/invite', element: <AcceptInvite /> },
      { path: '/form/:slug', element: <PublicForm /> },
      { path: '/c/:slug', element: <CustomerAccess /> },
      {
        path: '/',
        element: <Shell />,
        children: [
          { index: true, element: <Home /> },
          // The board and the customer's ticket list used to BE the index route. They moved aside so
          // the index could become the per-role home screen; both are one nav click away.
          { path: 'pano', element: <Kanban /> },
          { path: 'taleplerim', element: <CustomerTickets /> },
          { path: 'account', element: <Account /> },
          { path: 'tickets/:id', element: <TicketDetail /> },
          { path: 'moderation', element: <Moderation /> },
          { path: 'revenue', element: <Revenue /> },
          { path: 'admin/columns', element: <Columns /> },
          { path: 'admin/form-fields', element: <FormFields /> },
          { path: 'reports', element: <Dashboard /> },
          { path: 'settings', element: <Settings /> },
          { path: 'admin/templates', element: <Templates /> },
          { path: 'admin/users', element: <AdminUsers /> },
          { path: 'admin/companies', element: <Companies /> },
          { path: 'admin/permissions', element: <Permissions /> },
        ],
      },
      { path: '*', element: <NotFound /> },
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
