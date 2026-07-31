import { useAuth } from '../lib/auth'
import Kanban from './Kanban'
import CustomerTickets from './CustomerTickets'

// The landing screen depends on who you are: staff (super admin or a company member) get the kanban
// board; a customer gets their own ticket list. One index route, two audiences.
export default function Home() {
  const { user } = useAuth()
  const isStaff = !!user && (user.isSuperAdmin || user.companies.length > 0)
  return isStaff ? <Kanban /> : <CustomerTickets />
}
