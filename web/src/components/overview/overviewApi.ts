import { computed, type Ref } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { storeToRefs } from 'pinia'
import { apiClient, ApiClientError } from '@/api'
import { useWorkspaceStore } from '@/stores/workspace'
import type {
  CampaignPerformanceResponse,
  OverviewAlert,
  OverviewAlertSummary,
  OverviewData,
  OverviewKpi,
  OverviewResponse,
} from './types'

export interface OverviewQueryParams {
  from: Ref<string>
  to: Ref<string>
  currency?: Ref<string | null | undefined>
}

function nullableNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null
}

function queryPath(from: string, to: string, currency?: string | null): string {
  const query = new URLSearchParams({ from, to })
  if (currency) query.set('currency', currency)
  return `/app?${query.toString()}`
}

function campaignPath(currency: string | null): string {
  const query = new URLSearchParams({ limit: '100', sort: 'createdAt', direction: 'desc' })
  if (currency) query.set('currency', currency)
  return `/app/campaigns?${query.toString()}`
}

function mapKpis(response: OverviewResponse): OverviewKpi[] {
  const currency = response.currency || null
  const budgetPacing = nullableNumber(response.kpis.budgetPacing)
  return [
    { key: 'spend', label: 'Spend', value: nullableNumber(response.spend), currency, comparison: null },
    { key: 'budgetConsumed', label: 'Budget consumed', value: budgetPacing === null ? null : budgetPacing * 100, currency: null, comparison: null },
    { key: 'cpc', label: 'CPC', value: nullableNumber(response.kpis.cpc), currency, comparison: null },
    { key: 'cpa', label: 'CPA', value: nullableNumber(response.kpis.cpa), currency, comparison: null },
    { key: 'roas', label: 'ROAS', value: nullableNumber(response.kpis.roas), currency: null, comparison: null },
  ]
}

function mapAlert(alert: OverviewAlertSummary): OverviewAlert {
  const status = alert.status || 'open'
  return {
    id: alert.id,
    campaignId: alert.campaignId,
    severity: (alert.severity || 'info') as OverviewAlert['severity'],
    campaignName: alert.campaignName,
    metricName: alert.metricName,
    status,
    detectedAt: alert.detectedAt,
    rule: alert.metricName,
    explanation: `Status: ${status}`,
  }
}

function mapCampaigns(response: CampaignPerformanceResponse) {
  return response.items.map((campaign) => ({
    ...campaign,
    budget: nullableNumber(campaign.budget),
    spend: nullableNumber(campaign.spend),
    cpc: nullableNumber(campaign.cpc),
    cpa: nullableNumber(campaign.cpa),
    roas: nullableNumber(campaign.roas),
  }))
}

async function loadOverview(from: string, to: string, currency: string | null | undefined, signal: AbortSignal): Promise<OverviewData> {
  const response = await apiClient.get<OverviewResponse>(queryPath(from, to, currency), signal)
  let campaigns: OverviewData['campaigns'] = []
  let partial = false
  let missingSections: string[] = []
  try {
    const campaignResponse = await apiClient.get<CampaignPerformanceResponse>(campaignPath(response.currency || currency || null), signal)
    campaigns = mapCampaigns(campaignResponse).filter((campaign) => !response.currency || campaign.currency === response.currency)
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    partial = true
    missingSections = ['campaign performance']
  }

  return {
    period: { from: response.from, to: response.to, reportingTimeZone: response.reportingTimeZone },
    kpis: mapKpis(response),
    trend: response.trend.map((point) => ({ date: point.date, spend: nullableNumber(point.spend), revenue: nullableNumber(point.revenue) })),
    budgetPacing: response.budgetPacing.map((row) => {
      const pacing = nullableNumber(row.pacing)
      return {
        campaignId: row.campaignId,
        campaignName: row.campaignName,
        spent: nullableNumber(row.spent),
        plannedToDate: nullableNumber(row.plannedToDate),
        pacing: pacing === null ? null : pacing * 100,
        remainingBudget: nullableNumber(row.remainingBudget),
        status: (row.status || 'insufficient-data') as OverviewData['budgetPacing'][number]['status'],
      }
    }),
    alerts: response.priorityAlerts.map(mapAlert),
    campaigns,
    currency: response.currency,
    meta: { lastUpdatedAt: new Date().toISOString(), partial, missingSections, correlationId: null },
  }
}

export function useOverviewQuery(params: OverviewQueryParams) {
  const workspace = useWorkspaceStore()
  const { selectedWorkspaceId } = storeToRefs(workspace)
  const currency = computed(() => params.currency?.value || null)
  const enabled = computed(() => Boolean(params.from.value && params.to.value && params.from.value <= params.to.value))

  return useQuery({
    queryKey: computed(() => ['overview', selectedWorkspaceId.value, params.from.value, params.to.value, currency.value]),
    queryFn: ({ signal }) => loadOverview(params.from.value, params.to.value, currency.value, signal),
    enabled,
  })
}

export function overviewErrorCorrelationId(error: unknown): string | null {
  return error instanceof ApiClientError ? error.correlationId : null
}
