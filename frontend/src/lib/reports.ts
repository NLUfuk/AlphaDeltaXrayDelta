import { useQuery } from '@tanstack/react-query'
import { api } from './api'

export type TicketReport = {
  companyId: string | null
  totalTickets: number
  byStatusCategory: { category: number; count: number }[]
  avgFirstResponseHours: number | null
  avgResolutionHours: number | null
  staffLoad: { assignedToId: string | null; openCount: number }[]
  byCategory: { categoryId: string | null; count: number }[]
  trend: { date: string; opened: number; closed: number }[]
}

// Global for super admin, otherwise the caller's company. Path decided here so screens stay dumb.
function reportPath(companyId: string | null, suffix = '') {
  return companyId ? `/reports/company/${companyId}${suffix}` : `/reports/global${suffix}`
}

export function useReport(companyId: string | null) {
  return useQuery({
    queryKey: ['report', companyId],
    queryFn: async () => (await api.get<TicketReport>(reportPath(companyId))).data,
  })
}

/** Downloads the CSV through the authed client (a plain <a> can't send the Bearer header). */
export async function downloadCsv(companyId: string | null) {
  const res = await api.get(reportPath(companyId, '/export.csv'), { responseType: 'blob' })
  const url = URL.createObjectURL(res.data as Blob)
  const a = document.createElement('a')
  a.href = url
  a.download = companyId ? `report-${companyId}.csv` : 'report-global.csv'
  a.click()
  URL.revokeObjectURL(url)
}
