export type NullableNumber = number | null

export type TrendDirection = 'up' | 'down' | 'flat' | null

export interface OverviewComparison {
  value: NullableNumber
  direction: TrendDirection
  label: string | null
}

export interface OverviewKpis {
  ctr: NullableNumber
  cpc: NullableNumber
  cpa: NullableNumber
  roas: NullableNumber
  budgetPacing: NullableNumber
}

export interface OverviewKpi {
  key: 'spend' | 'budgetConsumed' | 'cpc' | 'cpa' | 'roas'
  label: string
  value: NullableNumber
  currency: string | null
  comparison: OverviewComparison | null
}

export interface OverviewTrendPoint {
  date: string
  spend: NullableNumber
  revenue: NullableNumber
}

export type PacingStatus = 'on-track' | 'at-risk' | 'over-budget' | 'insufficient-data'

export interface BudgetPacingRow {
  campaignId: string
  campaignName: string
  spent: NullableNumber
  plannedToDate: NullableNumber
  pacing: NullableNumber
  remainingBudget: NullableNumber
  status: PacingStatus
}

export type AlertSeverity = 'critical' | 'high' | 'medium' | 'low' | 'info'

export interface OverviewAlert {
  id: string
  campaignId: string | null
  severity: AlertSeverity
  campaignName: string | null
  metricName: string
  status: string
  detectedAt: string
  rule: string
  explanation: string
}

export interface OverviewAlertSummary {
  id: string
  campaignId: string | null
  campaignName: string | null
  metricName: string
  severity: string
  status: string
  detectedAt: string
}

export interface CampaignPerformanceRow {
  campaignId: string
  campaignName: string
  status: string | null
  currency: string | null
  budget: NullableNumber
  spend: NullableNumber
  cpc: NullableNumber
  cpa: NullableNumber
  roas: NullableNumber
  lastMetricDate: string | null
}

export interface OverviewPeriod {
  from: string
  to: string
  reportingTimeZone: string
}

export interface OverviewResponse {
  from: string
  to: string
  reportingTimeZone: string
  currency: string
  spend: NullableNumber
  revenue: NullableNumber
  kpis: OverviewKpis
  trend: OverviewTrendPoint[]
  budgetPacing: BudgetPacingRow[]
  priorityAlerts: OverviewAlertSummary[]
}

export interface OverviewMeta {
  lastUpdatedAt: string | null
  partial: boolean
  missingSections: string[]
  correlationId: string | null
}

export interface OverviewData {
  period: OverviewPeriod
  kpis: OverviewKpi[]
  trend: OverviewTrendPoint[]
  budgetPacing: BudgetPacingRow[]
  alerts: OverviewAlert[]
  campaigns: CampaignPerformanceRow[]
  currency: string
  meta: OverviewMeta
}

export interface CampaignPerformanceResponse {
  items: CampaignPerformanceRow[]
  nextCursor: string | null
  hasMore: boolean
  limit: number
}
