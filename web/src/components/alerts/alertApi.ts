import type {
  AlertAction,
  AlertApi,
  AlertDetail,
  AlertFilters,
  AlertListResponse,
  AlertSeverity,
  AlertStatus,
  AlertSummary,
} from './types'

const alertsPath = '/app/alerts'

export class AlertApiError extends Error {
  readonly status: number
  readonly correlationId: string | null

  constructor(message: string, status: number, correlationId: string | null = null) {
    super(message)
    this.name = 'AlertApiError'
    this.status = status
    this.correlationId = correlationId
  }
}

type JsonObject = Record<string, unknown>

function asObject(value: unknown): JsonObject {
  return typeof value === 'object' && value !== null ? value as JsonObject : {}
}

function first(value: JsonObject, ...keys: string[]): unknown {
  for (const key of keys) {
    if (key in value) return value[key]
  }
  return undefined
}

function stringValue(value: unknown): string | null {
  return typeof value === 'string' && value.trim() !== '' ? value : null
}

function numberValue(value: unknown): number | null {
  if (typeof value === 'number' && Number.isFinite(value)) return value
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value)
    return Number.isFinite(parsed) ? parsed : null
  }
  return null
}

function metricValue(value: unknown): string | number | null {
  return typeof value === 'string' || (typeof value === 'number' && Number.isFinite(value)) ? value : null
}

function statusValue(value: unknown): AlertStatus {
  const status = String(value ?? '').toLowerCase()
  if (status === 'active' || status === 'reopened') return 'open'
  if (status === 'open' || status === 'acknowledged' || status === 'resolved') return status
  return 'unknown'
}

function severityValue(value: unknown): AlertSeverity {
  const severity = String(value ?? '').toLowerCase()
  if (severity === 'critical' || severity === 'high' || severity === 'medium' || severity === 'low' || severity === 'none') {
    return severity
  }
  return 'unknown'
}

function stringArray(value: unknown): string[] {
  return Array.isArray(value) ? value.filter((entry): entry is string => typeof entry === 'string' && entry.trim() !== '') : []
}

function summaryValue(raw: unknown): AlertSummary {
  const value = asObject(raw)
  const id = stringValue(first(value, 'id', 'alertId')) ?? ''
  const metricName = stringValue(first(value, 'metricName', 'metric_name', 'metric'))
  const dataSufficiency = String(first(value, 'dataSufficiency', 'data_sufficiency') ?? '').toLowerCase()
  return {
    id,
    title: stringValue(first(value, 'title', 'alertTitle', 'name')) ?? metricName ?? id,
    campaignId: stringValue(first(value, 'campaignId', 'campaign_id')),
    campaignName: stringValue(first(value, 'campaignName', 'campaign_name')),
    fingerprint: stringValue(first(value, 'fingerprint')),
    metricName,
    score: numberValue(first(value, 'score')),
    severity: severityValue(first(value, 'severity')),
    status: statusValue(first(value, 'status')),
    observed: metricValue(first(value, 'observed', 'observedValue', 'observed_value')),
    baseline: metricValue(first(value, 'baseline', 'baselineValue', 'baseline_value')),
    confidence: numberValue(first(value, 'confidence')),
    dataSufficiency: dataSufficiency === 'sufficient' || dataSufficiency === 'insufficient' ? dataSufficiency : 'unknown',
    detectedAt: stringValue(first(value, 'detectedAt', 'detected_at', 'createdAt', 'created_at')),
    updatedAt: stringValue(first(value, 'updatedAt', 'updated_at')),
  }
}

function detailValue(raw: unknown): AlertDetail {
  const value = asObject(raw)
  const summary = summaryValue(value)
  const dataSufficiency = String(first(value, 'dataSufficiency', 'data_sufficiency') ?? '').toLowerCase()
  return {
    ...summary,
    explanation: stringValue(first(value, 'explanation', 'description', 'reason')),
    ruleId: stringValue(first(value, 'ruleId', 'rule_id')),
    scoringVersion: stringValue(first(value, 'scoringVersion', 'scoring_version')),
    evidenceWindowStart: stringValue(first(value, 'evidenceWindowStart', 'evidence_window_start')),
    evidenceWindowEnd: stringValue(first(value, 'evidenceWindowEnd', 'evidence_window_end')),
    confidence: numberValue(first(value, 'confidence')),
    dataSufficiency: dataSufficiency === 'sufficient' || dataSufficiency === 'insufficient' ? dataSufficiency : 'unknown',
    reasonCodes: stringArray(first(value, 'reasonCodes', 'reason_codes')),
    recommendedChecks: stringArray(first(value, 'recommendedChecks', 'recommended_checks')),
  }
}

function listValue(raw: unknown, response: Response): AlertListResponse {
  const envelope = asObject(raw)
  const rawItems = Array.isArray(raw) ? raw : first(envelope, 'items', 'alerts', 'data')
  const items = Array.isArray(rawItems) ? rawItems.map(summaryValue).filter((item) => item.id !== '') : []
  const totalCount = numberValue(first(envelope, 'totalCount', 'total_count', 'total'))
  return {
    items,
    totalCount,
    hasMore: first(envelope, 'hasMore', 'has_more', 'hasNextPage', 'has_next_page') === true,
    nextCursor: stringValue(first(envelope, 'nextCursor', 'next_cursor')),
    lastSuccessfulRefresh: stringValue(first(envelope, 'lastSuccessfulRefresh', 'last_successful_refresh')),
    partial: first(envelope, 'partial', 'isPartial', 'is_partial') === true,
    stale: first(envelope, 'stale', 'isStale', 'is_stale') === true,
    correlationId: response.headers.get('x-correlation-id') ?? stringValue(first(envelope, 'correlationId', 'correlation_id')),
  }
}

function apiBase(value?: string): string {
  if (value !== undefined) return value.replace(/\/$/, '')
  return (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
}

function queryString(filters: AlertFilters, cursor: string | null | undefined): string {
  const params = new URLSearchParams({ page: '1', pageSize: '25' })
  if (filters.status) params.set('status', filters.status)
  if (filters.severity) params.set('severity', filters.severity)
  if (filters.metric.trim()) params.set('metricName', filters.metric.trim())
  if (filters.campaign.trim()) params.set('campaignId', filters.campaign.trim())
  if (filters.dateFrom) params.set('from', filters.dateFrom)
  if (filters.dateTo) params.set('to', filters.dateTo)
  if (cursor) params.set('cursor', cursor)
  const encoded = params.toString()
  return encoded ? `?${encoded}` : ''
}

async function readResponse(response: Response): Promise<unknown> {
  const text = await response.text()
  if (!text) return null
  try {
    return JSON.parse(text) as unknown
  } catch {
    return null
  }
}

async function request(base: string, path: string, init?: RequestInit): Promise<{ value: unknown; response: Response }> {
  const response = await fetch(`${base}${path}`, {
    credentials: 'include',
    ...init,
    headers: {
      Accept: 'application/json',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  })
  const value = await readResponse(response)
  if (!response.ok) {
    const error = asObject(value)
    const message = stringValue(first(error, 'title', 'detail', 'message')) ?? `Alert request failed (${response.status})`
    throw new AlertApiError(message, response.status, response.headers.get('x-correlation-id') ?? stringValue(first(error, 'correlationId', 'correlation_id')))
  }
  return { value, response }
}

export function createAlertApi(baseUrl?: string): AlertApi {
  const base = apiBase(baseUrl)
  return {
    async list(filters, cursor) {
      const result = await request(base, `${alertsPath}${queryString(filters, cursor)}`)
      return listValue(result.value, result.response)
    },
    async get(id) {
      const result = await request(base, `${alertsPath}/${encodeURIComponent(id)}`)
      const payload = asObject(result.value)
      return detailValue(first(payload, 'alert', 'data') ?? result.value)
    },
    async mutate(id, action, reason) {
      const result = await request(base, `${alertsPath}/${encodeURIComponent(id)}/${action}`, {
        method: 'POST',
        body: JSON.stringify({ confirm: true, reason }),
      })
      if (result.value === null) return null
      const payload = asObject(result.value)
      return detailValue(first(payload, 'alert', 'data') ?? result.value)
    },
  }
}
