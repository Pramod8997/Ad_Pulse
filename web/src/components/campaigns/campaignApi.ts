import { apiClient, ApiClientError } from '../../api';

export type DecimalValue = string | number | null;

export interface CampaignRecord {
  id: string;
  name: string;
  status: string;
  currency: string;
  budget: DecimalValue;
  startsOn: string;
  endsOn: string;
  spend: DecimalValue;
  cpc: DecimalValue;
  cpa: DecimalValue;
  roas: DecimalValue;
  lastMetricDate: string | null;
}

export interface CampaignWrite {
  name: string;
  status: string;
  currency: string;
  budget: number;
  startsOn: string;
  endsOn: string;
}

export type CampaignSortField = 'createdAt' | 'name' | 'status' | 'budget';
export type SortDirection = 'asc' | 'desc';

export interface CampaignListFilters {
  search: string;
  status: string;
  startDate: string;
  endDate: string;
  sort?: CampaignSortField;
  direction?: SortDirection;
}

export interface DailyMetricWrite {
  spend: number;
  revenue: number;
  impressions: number;
  clicks: number;
  conversions: number;
}

export interface CampaignScoreResult {
  alert?: unknown;
  created?: boolean;
  message?: string;
}

export interface CampaignListResult {
  items: CampaignRecord[];
  page: number;
  pageSize: number;
  totalCount: number | null;
  nextCursor: string | null;
  hasNext: boolean;
  lastSuccessfulAt: string | null;
}

export interface MetricPoint {
  date: string;
  spend: DecimalValue;
  revenue: DecimalValue;
  impressions: number | null;
  clicks: number | null;
  conversions: number | null;
}

export interface CampaignAlert {
  id: string;
  title: string;
  severity: string;
  status: string;
  metricName: string;
  detectedAt: string;
  observed: DecimalValue;
  baseline: DecimalValue;
}

export interface CampaignActivity {
  id: string;
  occurredAt: string;
  action: string;
  actorSubjectId: string;
  entityType: string;
}

export interface CampaignDetailResult {
  campaign: CampaignRecord;
  metrics: MetricPoint[];
  alerts: CampaignAlert[];
  activity: CampaignActivity[];
  kpis: {
    spend: DecimalValue;
    cpc: DecimalValue;
    cpa: DecimalValue;
    roas: DecimalValue;
    budgetPacing: DecimalValue;
  };
  lastSuccessfulAt: string | null;
  partial: boolean;
}

export class CampaignApiError extends Error {
  readonly correlationId: string | null;
  readonly status: number;

  constructor(message: string, status: number, correlationId: string | null = null) {
    super(message);
    this.name = 'CampaignApiError';
    this.status = status;
    this.correlationId = correlationId;
  }
}

type RecordValue = Record<string, unknown>;

function isRecord(value: unknown): value is RecordValue {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function asString(value: unknown, fallback = ''): string {
  return typeof value === 'string' ? value : value == null ? fallback : String(value);
}

function asNullableString(value: unknown): string | null {
  return value == null || value === '' ? null : asString(value);
}

function asDecimal(value: unknown): DecimalValue {
  return typeof value === 'string' || typeof value === 'number' ? value : null;
}

function asNullableNumber(value: unknown): number | null {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function nestedData(value: unknown): RecordValue {
  if (!isRecord(value)) return {};
  return isRecord(value.data) ? value.data : value;
}

function normalizeCampaign(value: unknown): CampaignRecord {
  const item = nestedData(value);
  return {
    id: asString(item.id),
    name: asString(item.name),
    status: asString(item.status),
    currency: asString(item.currency).toUpperCase(),
    budget: asDecimal(item.budget),
    startsOn: asString(item.startsOn ?? item.starts_on),
    endsOn: asString(item.endsOn ?? item.ends_on),
    spend: asDecimal(item.spend),
    cpc: asDecimal(item.cpc),
    cpa: asDecimal(item.cpa),
    roas: asDecimal(item.roas),
    lastMetricDate: asNullableString(item.lastMetricDate ?? item.last_metric_date),
  };
}

function normalizeMetric(value: unknown): MetricPoint {
  const item = isRecord(value) ? value : {};
  return {
    date: asString(item.date ?? item.metricDate ?? item.metric_date),
    spend: asDecimal(item.spend),
    revenue: asDecimal(item.revenue),
    impressions: asNullableNumber(item.impressions),
    clicks: asNullableNumber(item.clicks),
    conversions: asNullableNumber(item.conversions),
  };
}

function normalizeAlert(value: unknown): CampaignAlert {
  const item = isRecord(value) ? value : {};
  return {
    id: asString(item.id),
    title: asString(item.title ?? item.metricName ?? item.metric_name, 'Campaign alert'),
    severity: asString(item.severity),
    status: asString(item.status),
    metricName: asString(item.metricName ?? item.metric_name),
    detectedAt: asString(item.detectedAt ?? item.detected_at),
    observed: asDecimal(item.observed),
    baseline: asDecimal(item.baseline),
  };
}

function normalizeActivity(value: unknown): CampaignActivity {
  const item = isRecord(value) ? value : {};
  return {
    id: asString(item.id),
    occurredAt: asString(item.occurredAt ?? item.occurred_at),
    action: asString(item.action),
    actorSubjectId: asString(item.actorSubjectId ?? item.actor_subject_id),
    entityType: asString(item.entityType ?? item.entity_type),
  };
}

function getArray(value: unknown, keys: string[]): unknown[] {
  if (!isRecord(value)) return [];
  for (const key of keys) {
    if (Array.isArray(value[key])) return value[key];
  }
  if (isRecord(value.data)) return getArray(value.data, keys);
  return [];
}

async function request<T>(path: string, options: { method?: 'GET' | 'POST' | 'PUT' | 'PATCH'; body?: unknown; signal?: AbortSignal } = {}): Promise<T> {
  try {
    return await apiClient.request<T>(path, options);
  } catch (error) {
    if (error instanceof ApiClientError) throw new CampaignApiError(error.message, error.status ?? 0, error.correlationId);
    throw new CampaignApiError('The campaign service could not be reached.', 0);
  }
}

export async function listCampaigns(
  filters: CampaignListFilters,
  page: number,
  pageSize: number,
  cursor: string | null = null,
  signal?: AbortSignal,
): Promise<CampaignListResult> {
  const params = new URLSearchParams({
    limit: String(pageSize),
  });
  if (cursor) params.set('cursor', cursor);
  if (filters.search.trim()) params.set('search', filters.search.trim());
  if (filters.status) params.set('status', filters.status);
  if (filters.startDate) params.set('startsOnFrom', filters.startDate);
  if (filters.endDate) params.set('endsOnTo', filters.endDate);
  if (filters.sort) params.set('sort', filters.sort);
  if (filters.direction) params.set('direction', filters.direction);

  const payload = await request<unknown>(`/app/campaigns?${params.toString()}`, { signal });
  const data = nestedData(payload);
  const rawItems = getArray(payload, ['items', 'campaigns']);
  if (!rawItems.length && Array.isArray(payload)) rawItems.push(...payload);
  const totalValue = data.totalCount;
  const totalCount = typeof totalValue === 'number' && Number.isFinite(totalValue) ? totalValue : null;
  const hasNext = data.hasMore === true || data.hasNext === true;

  return {
    items: rawItems.map(normalizeCampaign),
    page,
    pageSize,
    totalCount,
    hasNext,
    nextCursor: asNullableString(data.nextCursor),
    lastSuccessfulAt: new Date().toISOString(),
  };
}

export async function createCampaign(input: CampaignWrite): Promise<CampaignRecord> {
  return normalizeCampaign(await request<unknown>('/app/campaigns', { method: 'POST', body: input }));
}

export async function updateCampaign(id: string, input: CampaignWrite): Promise<CampaignRecord> {
  return normalizeCampaign(await request<unknown>(`/app/campaigns/${encodeURIComponent(id)}`, {
    method: 'PATCH',
    body: input,
  }));
}

export async function upsertCampaignMetric(
  campaignId: string,
  metricDate: string,
  input: DailyMetricWrite,
): Promise<MetricPoint> {
  const payload = await request<unknown>(
    `/app/campaigns/${encodeURIComponent(campaignId)}/metrics/${encodeURIComponent(metricDate)}`,
    {
      method: 'PUT',
      body: input,
    },
  );
  return normalizeMetric(nestedData(payload));
}

export async function scoreCampaign(campaignId: string): Promise<CampaignScoreResult> {
  const payload = await request<unknown>(`/app/campaigns/${encodeURIComponent(campaignId)}/score`, {
    method: 'POST',
  });
  if (!isRecord(payload)) {
    return {};
  }
  const data = nestedData(payload);
  return {
    alert: data.alert ?? payload.alert,
    created: typeof data.created === 'boolean' ? data.created : undefined,
    message: asNullableString(data.message) ?? undefined,
  };
}

export async function getCampaignDetail(id: string, signal?: AbortSignal): Promise<CampaignDetailResult> {
  const encodedId = encodeURIComponent(id);
  const requests = await Promise.allSettled([
    request<unknown>(`/app/campaigns/${encodedId}`, { signal }),
    request<unknown>(`/app/campaigns/${encodedId}/metrics?limit=100`, { signal }),
    request<unknown>(`/app/alerts?campaignId=${encodedId}&limit=100`, { signal }),
    request<unknown>(`/app/audit?entityType=Campaign&entityId=${encodedId}&limit=100`, { signal }),
  ]);
  const campaignResponse = requests[0];
  if (campaignResponse.status === 'rejected') throw campaignResponse.reason;
  const campaign = normalizeCampaign(campaignResponse.value);
  const metricsResponse = requests[1];
  const alertsResponse = requests[2];
  const activityResponse = requests[3];
  const metricsPayload = metricsResponse.status === 'fulfilled' ? nestedData(metricsResponse.value) : {};
  const alertPayload = alertsResponse.status === 'fulfilled' ? nestedData(alertsResponse.value) : {};
  const metrics = getArray(metricsPayload, ['items', 'metrics', 'dailyMetrics']).map(normalizeMetric);
  const alerts = getArray(alertPayload, ['items', 'alerts']).map(normalizeAlert);
  const activityPayload = activityResponse.status === 'fulfilled' ? nestedData(activityResponse.value) : {};
  const activity = getArray(activityPayload, ['items', 'activity', 'activities', 'auditEvents']).map(normalizeActivity);
  const spend = sumDecimal(metrics.map(metric => metric.spend));
  const revenue = sumDecimal(metrics.map(metric => metric.revenue));
  const clicks = sumInteger(metrics.map(metric => metric.clicks));
  const conversions = sumInteger(metrics.map(metric => metric.conversions));
  const plannedToDate = campaign.budget == null ? null : plannedToDateBudget(campaign, metrics);
  const kpis = {
    spend,
    cpc: divideDecimal(spend, clicks),
    cpa: divideDecimal(spend, conversions),
    roas: divideDecimal(revenue, spend),
    budgetPacing: divideDecimal(spend, plannedToDate),
  };
  return {
    campaign,
    metrics,
    alerts,
    activity,
    kpis,
    lastSuccessfulAt: new Date().toISOString(),
    partial: metricsResponse.status === 'rejected' || alertsResponse.status === 'rejected' || activityResponse.status === 'rejected',
  };
}

function sumDecimal(values: DecimalValue[]): number | null {
  const present = values.filter((value): value is string | number => value != null);
  return present.length ? present.reduce((total, value) => total + Number(value), 0) : null;
}

function sumInteger(values: Array<number | null>): number | null {
  const present = values.filter((value): value is number => value != null);
  return present.length ? present.reduce((total, value) => total + value, 0) : null;
}

function divideDecimal(numerator: DecimalValue, denominator: DecimalValue): number | null {
  if (numerator == null || denominator == null || Number(denominator) === 0) return null;
  return Number(numerator) / Number(denominator);
}

function plannedToDateBudget(campaign: CampaignRecord, metrics: MetricPoint[]): number | null {
  const latestDate = metrics.at(-1)?.date;
  if (!latestDate) return null;
  const start = new Date(`${campaign.startsOn}T00:00:00Z`).getTime();
  const end = new Date(`${campaign.endsOn}T00:00:00Z`).getTime();
  const current = new Date(`${latestDate}T00:00:00Z`).getTime();
  const totalDays = Math.floor((end - start) / 86_400_000) + 1;
  const elapsedDays = Math.max(0, Math.min(totalDays, Math.floor((current - start) / 86_400_000) + 1));
  return totalDays > 0 ? Number(campaign.budget) * elapsedDays / totalDays : null;
}

export function formatMoney(value: DecimalValue, currency: string): string {
  if (value == null || value === '') return '—';
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: currency || 'XXX' }).format(Number(value));
  } catch {
    return `${currency || '—'} ${String(value)}`;
  }
}

export function formatDecimal(value: DecimalValue): string {
  return value == null || value === '' ? '—' : String(value);
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return '—';
  const parsed = new Date(`${value}T00:00:00`);
  return Number.isNaN(parsed.getTime()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(parsed);
}

export function formatDateTime(value: string | null | undefined): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(parsed);
}

export function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : 'The campaign request failed.';
}

export function errorCorrelationId(error: unknown): string | null {
  return error instanceof CampaignApiError ? error.correlationId : null;
}
