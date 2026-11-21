<script setup lang="ts">
import {
  AlertTriangle,
  CalendarDays,
  Check,
  CheckCircle2,
  CircleAlert,
  Filter,
  Info,
  LoaderCircle,
  RefreshCw,
  RotateCcw,
  Search,
  X,
} from 'lucide-vue-next'
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { AlertApiError, createAlertApi } from '../components/alerts/alertApi'
import AlertSeverityBadge from '../components/alerts/AlertSeverityBadge.vue'
import AlertStatusBadge from '../components/alerts/AlertStatusBadge.vue'
import type {
  AlertAction,
  AlertApi,
  AlertDetail,
  AlertFilters,
  AlertListResponse,
  AlertSeverity,
  AlertStatus,
  AlertSummary,
  IncidentStatus,
} from '../components/alerts/types'

const props = defineProps<{ api?: AlertApi }>()
const api = props.api ?? createAlertApi()

const filters = reactive<AlertFilters>({
  status: '',
  severity: '',
  metric: '',
  campaign: '',
  dateFrom: '',
  dateTo: '',
  incidentStatus: '',
  owner: '',
})
const appliedFilters = ref<AlertFilters>({ ...filters })
const response = ref<AlertListResponse | null>(null)
const alerts = computed(() => response.value?.items ?? [])
const listLoading = ref(true)
const listError = ref<string | null>(null)
const listCorrelationId = ref<string | null>(null)
const listRequest = ref(0)
const selectedId = ref<string | null>(null)
const selectedAlert = ref<AlertDetail | null>(null)
const detailLoading = ref(false)
const detailError = ref<string | null>(null)
const detailCorrelationId = ref<string | null>(null)
const selectedLoadingId = ref<string | null>(null)
const drawerRef = ref<HTMLElement | null>(null)
const dialogRef = ref<HTMLElement | null>(null)
const closeRef = ref<HTMLButtonElement | null>(null)
const previousFocus = ref<HTMLElement | null>(null)

type PendingAction = AlertAction | `incident:${IncidentStatus}`
const pendingAction = ref<PendingAction | null>(null)
const reason = ref('')
const mutationLoading = ref(false)
const mutationError = ref<string | null>(null)
const mutationMessage = ref<string | null>(null)
const mutationCorrelationId = ref<string | null>(null)
const ownerDraft = ref('')
const ownerSaving = ref(false)
const ownerError = ref<string | null>(null)
const noteDraft = ref('')
const noteSaving = ref(false)
const noteError = ref<string | null>(null)

const statusOptions: Array<{ value: AlertStatus | ''; label: string }> = [
  { value: '', label: 'All statuses' },
  { value: 'open', label: 'Open' },
  { value: 'acknowledged', label: 'Acknowledged' },
  { value: 'resolved', label: 'Resolved' },
]
const incidentStatusOptions: Array<{ value: IncidentStatus | ''; label: string }> = [
  { value: '', label: 'All incident states' },
  { value: 'new', label: 'New' },
  { value: 'acknowledged', label: 'Acknowledged' },
  { value: 'investigating', label: 'Investigating' },
  { value: 'mitigated', label: 'Mitigated' },
  { value: 'resolved', label: 'Resolved' },
  { value: 'false_positive', label: 'False positive' },
]
const severityOptions: Array<{ value: AlertSeverity | ''; label: string }> = [
  { value: '', label: 'All severities' },
  { value: 'critical', label: 'Critical' },
  { value: 'high', label: 'High' },
  { value: 'medium', label: 'Medium' },
  { value: 'low', label: 'Low' },
]

const hasFilters = computed(() => Object.values(appliedFilters.value).some(Boolean))
const selectedActionLabel = computed(() => pendingAction.value ? actionLabel(pendingAction.value) : '')
const canSubmitMutation = computed(() => reason.value.trim().length >= 3 && !mutationLoading.value)
const showStaleBanner = computed(() => Boolean(response.value?.stale || (alerts.value.length > 0 && listLoading.value)))

function cloneFilters(value: AlertFilters): AlertFilters {
  return { ...value }
}

function actionLabel(action: PendingAction): string {
  const value = action.startsWith('incident:') ? action.slice('incident:'.length) : action
  const label = value.replace('_', ' ')
  return label[0].toUpperCase() + label.slice(1)
}

function actionPastTense(action: AlertAction): string {
  if (action === 'acknowledge') return 'acknowledged'
  if (action === 'resolve') return 'resolved'
  return 'reopened'
}

function statusAfterAction(action: AlertAction): AlertStatus {
  if (action === 'acknowledge') return 'acknowledged'
  if (action === 'resolve') return 'resolved'
  return 'open'
}

function getErrorMessage(error: unknown): string {
  if (error instanceof AlertApiError) return error.message
  return 'Alerts could not be loaded. Try again.'
}

function formatDate(value: string | null): string {
  if (!value) return 'Unavailable'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function formatDateOnly(value: string | null): string {
  if (!value) return 'Unavailable'
  const date = new Date(`${value}T00:00:00`)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' }).format(date)
}

function formatMetric(value: string | number | null): string {
  if (value === null) return 'Missing'
  if (typeof value === 'number') return value.toLocaleString(undefined, { maximumFractionDigits: 4 })
  return value
}

function formatConfidence(value: number | null): string {
  if (value === null) return 'Unavailable'
  return Math.max(0, Math.min(1, value)).toFixed(2)
}

function openDetail(alert: AlertSummary): void {
  previousFocus.value = document.activeElement instanceof HTMLElement ? document.activeElement : null
  selectedId.value = alert.id
  selectedAlert.value = null
  ownerDraft.value = ''
  noteDraft.value = ''
  ownerError.value = null
  noteError.value = null
  detailError.value = null
  mutationMessage.value = null
  window.history.replaceState({}, '', `${window.location.pathname}?alert=${encodeURIComponent(alert.id)}`)
}

function closeDetail(): void {
  selectedId.value = null
  selectedAlert.value = null
  pendingAction.value = null
  window.history.replaceState({}, '', window.location.pathname)
  nextTick(() => previousFocus.value?.focus())
}

async function loadList(): Promise<void> {
  const requestId = listRequest.value + 1
  listRequest.value = requestId
  listError.value = null
  listLoading.value = true
  try {
    const result = await api.list(appliedFilters.value)
    if (requestId !== listRequest.value) return
    response.value = result
    listCorrelationId.value = result.correlationId
  } catch (error) {
    if (requestId !== listRequest.value) return
    listError.value = getErrorMessage(error)
    listCorrelationId.value = error instanceof AlertApiError ? error.correlationId : null
  } finally {
    if (requestId === listRequest.value) listLoading.value = false
  }
}

async function loadDetail(id: string): Promise<void> {
  selectedLoadingId.value = id
  detailLoading.value = true
  detailError.value = null
  detailCorrelationId.value = null
  try {
    selectedAlert.value = await api.get(id)
    ownerDraft.value = selectedAlert.value.ownerSubjectId ?? ''
    await nextTick()
    closeRef.value?.focus()
  } catch (error) {
    detailError.value = getErrorMessage(error)
    detailCorrelationId.value = error instanceof AlertApiError ? error.correlationId : null
  } finally {
    if (selectedLoadingId.value === id) detailLoading.value = false
  }
}

function submitFilters(): void {
  appliedFilters.value = cloneFilters(filters)
  loadList()
}

function clearFilters(): void {
  Object.assign(filters, { status: '', severity: '', metric: '', campaign: '', dateFrom: '', dateTo: '', incidentStatus: '', owner: '' })
  submitFilters()
}

function requestAction(action: AlertAction): void {
  if (!selectedAlert.value || selectedAlert.value.status === 'unknown') return
  pendingAction.value = action
  reason.value = ''
  mutationError.value = null
  mutationMessage.value = null
  mutationCorrelationId.value = null
}

function requestIncidentAction(status: IncidentStatus): void {
  if (!selectedAlert.value) return
  pendingAction.value = `incident:${status}`
  reason.value = ''
  mutationError.value = null
  mutationMessage.value = null
  mutationCorrelationId.value = null
}

function cancelAction(): void {
  if (!mutationLoading.value) {
    pendingAction.value = null
    reason.value = ''
  }
}

async function confirmAction(): Promise<void> {
  if (!selectedAlert.value || !pendingAction.value || !canSubmitMutation.value) return
  const action = pendingAction.value
  mutationLoading.value = true
  mutationError.value = null
  mutationMessage.value = null
  try {
    const updated = action.startsWith('incident:')
      ? await api.changeIncident(selectedAlert.value.id, action.slice('incident:'.length) as IncidentStatus, reason.value.trim())
      : await api.mutate(selectedAlert.value.id, action, reason.value.trim())
    if (updated) {
      selectedAlert.value = updated
    } else {
      selectedAlert.value = action.startsWith('incident:')
        ? { ...selectedAlert.value, incidentStatus: action.slice('incident:'.length) as IncidentStatus, updatedAt: new Date().toISOString() }
        : { ...selectedAlert.value, status: statusAfterAction(action), updatedAt: new Date().toISOString() }
    }
    mutationMessage.value = action.startsWith('incident:')
      ? `Incident marked ${actionLabel(action).toLowerCase()} successfully.`
      : `Alert ${actionPastTense(action)} successfully.`
    pendingAction.value = null
    reason.value = ''
    await loadList()
  } catch (error) {
    mutationError.value = getErrorMessage(error)
    mutationCorrelationId.value = error instanceof AlertApiError ? error.correlationId : null
  } finally {
    mutationLoading.value = false
  }
}

function availableActions(alert: AlertDetail): AlertAction[] {
  if (alert.status === 'open') return ['acknowledge', 'resolve']
  if (alert.status === 'acknowledged') return ['resolve', 'reopen']
  if (alert.status === 'resolved') return ['reopen']
  return []
}

function availableIncidentActions(alert: AlertDetail): IncidentStatus[] {
  if (alert.incidentStatus === 'new') return ['acknowledged', 'investigating', 'resolved', 'false_positive']
  if (alert.incidentStatus === 'acknowledged') return ['investigating', 'mitigated', 'resolved', 'false_positive']
  if (alert.incidentStatus === 'investigating') return ['acknowledged', 'mitigated', 'resolved', 'false_positive']
  if (alert.incidentStatus === 'mitigated') return ['investigating', 'resolved']
  if (alert.incidentStatus === 'resolved' || alert.incidentStatus === 'false_positive') return ['new']
  return []
}

async function saveOwner(): Promise<void> {
  if (!selectedAlert.value || ownerSaving.value) return
  ownerSaving.value = true
  ownerError.value = null
  try {
    const updated = await api.assign(selectedAlert.value.id, ownerDraft.value.trim() || null)
    if (updated) selectedAlert.value = updated
    mutationMessage.value = ownerDraft.value.trim() ? 'Incident owner updated.' : 'Incident unassigned.'
    await loadList()
  } catch (error) {
    ownerError.value = getErrorMessage(error)
  } finally {
    ownerSaving.value = false
  }
}

async function saveNote(): Promise<void> {
  if (!selectedAlert.value || noteSaving.value || noteDraft.value.trim().length < 3) return
  noteSaving.value = true
  noteError.value = null
  try {
    const updated = await api.addNote(selectedAlert.value.id, noteDraft.value.trim())
    if (updated) selectedAlert.value = updated
    noteDraft.value = ''
    mutationMessage.value = 'Investigation note added.'
  } catch (error) {
    noteError.value = getErrorMessage(error)
  } finally {
    noteSaving.value = false
  }
}

function handleKeydown(event: KeyboardEvent): void {
  if (pendingAction.value) {
    if (event.key === 'Escape') {
      event.preventDefault()
      cancelAction()
      return
    }
    if (event.key === 'Tab') {
      trapFocus(event, dialogRef.value)
      return
    }
  }
  if (event.key === 'Escape' && selectedId.value) closeDetail()
  if (event.key === 'Tab' && selectedId.value) trapFocus(event, drawerRef.value)
}

function trapFocus(event: KeyboardEvent, container: HTMLElement | null): void {
  if (!container) return
  const focusable = Array.from(container.querySelectorAll<HTMLElement>('button:not(:disabled), input:not(:disabled), textarea:not(:disabled), select:not(:disabled), [tabindex]:not([tabindex="-1"])'))
  if (!focusable.length) return
  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

onMounted(() => {
  const initialId = new URLSearchParams(window.location.search).get('alert')
  window.addEventListener('keydown', handleKeydown)
  loadList()
  if (initialId) selectedId.value = initialId
})

onUnmounted(() => window.removeEventListener('keydown', handleKeydown))

watch(selectedId, (id) => {
  if (id) loadDetail(id)
})

watch(pendingAction, (action) => {
  if (action) nextTick(() => dialogRef.value?.querySelector<HTMLElement>('#mutation-reason')?.focus())
})
</script>

<template>
  <main class="alerts-page" aria-labelledby="alerts-heading">
    <header class="page-header">
      <div>
        <p class="eyebrow">Monitoring</p>
        <h1 id="alerts-heading">Incident inbox</h1>
        <p class="page-intro">Review campaign signals, investigate evidence, and track operational resolution.</p>
      </div>
      <button class="button button-secondary" type="button" :disabled="listLoading" @click="loadList">
        <RefreshCw :size="16" :class="{ spinning: listLoading }" aria-hidden="true" />
        Refresh
      </button>
    </header>

    <section class="filter-panel" aria-labelledby="filter-heading">
      <div class="filter-heading">
        <div>
          <h2 id="filter-heading"><Filter :size="17" aria-hidden="true" /> Filter incidents</h2>
          <p>Filters apply to the tenant-scoped incident inbox.</p>
        </div>
        <button v-if="hasFilters" class="text-button" type="button" @click="clearFilters">Clear filters</button>
      </div>
      <form class="filters" @submit.prevent="submitFilters">
        <label>
          <span>Status</span>
          <select v-model="filters.status">
            <option v-for="option in statusOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
          </select>
        </label>
        <label>
          <span>Incident state</span>
          <select v-model="filters.incidentStatus">
            <option v-for="option in incidentStatusOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
          </select>
        </label>
        <label>
          <span>Severity</span>
          <select v-model="filters.severity">
            <option v-for="option in severityOptions" :key="option.value" :value="option.value">{{ option.label }}</option>
          </select>
        </label>
        <label>
          <span>Metric</span>
          <span class="input-with-icon"><Search :size="16" aria-hidden="true" /><input v-model="filters.metric" type="search" placeholder="e.g. CPA" /></span>
        </label>
        <label>
          <span>Campaign</span>
          <input v-model="filters.campaign" type="search" placeholder="Campaign name or ID" />
        </label>
        <label>
          <span>Owner subject ID</span>
          <input v-model="filters.owner" type="search" placeholder="Optional owner" />
        </label>
        <label>
          <span>Detected from</span>
          <span class="input-with-icon"><CalendarDays :size="16" aria-hidden="true" /><input v-model="filters.dateFrom" type="date" /></span>
        </label>
        <label>
          <span>Detected to</span>
          <span class="input-with-icon"><CalendarDays :size="16" aria-hidden="true" /><input v-model="filters.dateTo" type="date" /></span>
        </label>
        <button class="button button-primary filter-submit" type="submit">Apply filters</button>
      </form>
    </section>

    <div v-if="showStaleBanner" class="state-banner state-stale" role="status">
      <Info :size="17" aria-hidden="true" />
      <span>
        Showing the last available results<span v-if="response?.lastSuccessfulRefresh"> from {{ formatDate(response.lastSuccessfulRefresh) }}</span>.
        <span v-if="response?.stale"> The server marked this data as stale.</span>
      </span>
    </div>
    <div v-if="response?.partial" class="state-banner state-partial" role="status">
      <AlertTriangle :size="17" aria-hidden="true" /> Some alert fields or pages are unavailable. Missing values are shown as Missing.
    </div>
    <div v-if="mutationMessage" class="state-banner state-success" role="status">
      <CheckCircle2 :size="17" aria-hidden="true" /> {{ mutationMessage }}
    </div>

    <section class="list-panel" aria-labelledby="list-heading" aria-live="polite">
      <div class="list-heading">
        <div>
        <h2 id="list-heading">Incident queue</h2>
          <p v-if="response?.totalCount !== null && response">{{ response.totalCount }} result{{ response.totalCount === 1 ? '' : 's' }}</p>
          <p v-else-if="!listLoading">Results returned by the alert service</p>
        </div>
        <LoaderCircle v-if="listLoading" class="loading-icon" :size="18" aria-label="Loading alerts" />
      </div>

      <div v-if="listError && alerts.length === 0" class="state-block state-error" role="alert">
        <CircleAlert :size="22" aria-hidden="true" />
        <h3>We could not load alerts</h3>
        <p>{{ listError }}</p>
        <p v-if="listCorrelationId" class="correlation">Correlation ID: {{ listCorrelationId }}</p>
        <button class="button button-secondary" type="button" @click="loadList"><RefreshCw :size="16" aria-hidden="true" /> Try again</button>
      </div>
      <div v-else-if="listLoading && alerts.length === 0" class="skeleton-table" aria-label="Loading alerts">
        <div v-for="index in 5" :key="index" class="skeleton-row"><span /><span /><span /><span /><span /></div>
      </div>
      <div v-else-if="!listLoading && alerts.length === 0" class="state-block state-empty">
        <Search :size="22" aria-hidden="true" />
        <h3>{{ hasFilters ? 'No alerts match these filters' : 'No alerts to review' }}</h3>
        <p>{{ hasFilters ? 'Adjust or clear a filter to search the full alert queue.' : 'New API-backed alerts will appear here when campaign evidence is available.' }}</p>
        <button v-if="hasFilters" class="button button-secondary" type="button" @click="clearFilters">Clear filters</button>
      </div>
      <div v-else class="table-wrap">
        <table>
             <caption class="sr-only">Incidents returned by the alert service</caption>
          <thead>
            <tr><th scope="col">Severity</th><th scope="col">Incident</th><th scope="col">Campaign</th><th scope="col">Observed / baseline</th><th scope="col">Detected</th><th scope="col">State</th><th scope="col">Owner</th></tr>
          </thead>
          <tbody>
            <tr v-for="alert in alerts" :key="alert.id" :class="{ selected: selectedId === alert.id }" :aria-selected="selectedId === alert.id" tabindex="0" @click="openDetail(alert)" @keydown.enter="openDetail(alert)" @keydown.space.prevent="openDetail(alert)">
              <td data-label="Severity"><AlertSeverityBadge :severity="alert.severity" /></td>
              <th scope="row" data-label="Alert"><span class="alert-title">{{ alert.title }}</span><span class="alert-id">{{ alert.id }}</span></th>
              <td data-label="Campaign">{{ alert.campaignName ?? alert.campaignId ?? 'Missing' }}</td>
              <td data-label="Observed / baseline"><span class="metric-value">{{ formatMetric(alert.observed) }}</span><span class="metric-baseline">Baseline: {{ formatMetric(alert.baseline) }}</span></td>
              <td data-label="Detected">{{ formatDate(alert.detectedAt) }}</td>
              <td data-label="State"><AlertStatusBadge :status="alert.incidentStatus" /></td>
              <td data-label="Owner">{{ alert.ownerSubjectId ?? 'Unassigned' }}</td>
            </tr>
          </tbody>
        </table>
        <p v-if="listError" class="inline-error" role="alert">{{ listError }} <span v-if="listCorrelationId">Correlation ID: {{ listCorrelationId }}</span></p>
      </div>
    </section>

    <aside v-if="selectedId" ref="drawerRef" class="detail-drawer" role="dialog" aria-modal="true" aria-labelledby="alert-detail-heading">
      <div class="drawer-header">
         <div><p class="eyebrow">Incident investigation</p><h2 id="alert-detail-heading">{{ selectedAlert?.title ?? 'Loading incident' }}</h2></div>
        <button ref="closeRef" class="icon-button" type="button" aria-label="Close alert detail" title="Close alert detail" @click="closeDetail"><X :size="19" aria-hidden="true" /></button>
      </div>
      <div v-if="detailLoading" class="drawer-loading" role="status"><LoaderCircle class="loading-icon" :size="22" /> Loading alert evidence…</div>
      <div v-else-if="detailError" class="state-block state-error" role="alert">
        <CircleAlert :size="22" aria-hidden="true" /><h3>Alert detail is unavailable</h3><p>{{ detailError }}</p>
        <p v-if="detailCorrelationId" class="correlation">Correlation ID: {{ detailCorrelationId }}</p>
        <button class="button button-secondary" type="button" @click="selectedId && loadDetail(selectedId)"><RefreshCw :size="16" aria-hidden="true" /> Try again</button>
      </div>
      <div v-else-if="selectedAlert" class="drawer-content">
         <div class="detail-status"><AlertSeverityBadge :severity="selectedAlert.severity" /><AlertStatusBadge :status="selectedAlert.incidentStatus" /></div>
        <p class="detail-explanation">{{ selectedAlert.explanation ?? 'No explanation was supplied by the alert service.' }}</p>

        <section class="detail-section" aria-labelledby="evidence-heading">
          <h3 id="evidence-heading">Evidence</h3>
          <div class="evidence-grid">
            <div><dt>Observed</dt><dd>{{ formatMetric(selectedAlert.observed) }}</dd></div>
            <div><dt>Baseline</dt><dd>{{ formatMetric(selectedAlert.baseline) }}</dd></div>
            <div><dt>Evidence window</dt><dd>{{ formatDateOnly(selectedAlert.evidenceWindowStart) }} – {{ formatDateOnly(selectedAlert.evidenceWindowEnd) }}</dd></div>
            <div><dt>Data sufficiency confidence</dt><dd>{{ formatConfidence(selectedAlert.confidence) }}<small>Not a probability</small></dd></div>
            <div><dt>Data sufficiency</dt><dd>{{ selectedAlert.dataSufficiency === 'unknown' ? 'Unavailable' : selectedAlert.dataSufficiency }}</dd></div>
            <div><dt>Detected</dt><dd>{{ formatDate(selectedAlert.detectedAt) }}</dd></div>
          </div>
        </section>

         <section class="detail-section" aria-labelledby="context-heading">
           <h3 id="context-heading">Incident context</h3>
           <dl class="context-list"><div><dt>Campaign</dt><dd>{{ selectedAlert.campaignName ?? selectedAlert.campaignId ?? 'Missing' }}</dd></div><div><dt>Metric</dt><dd>{{ selectedAlert.metricName ?? 'Missing' }}</dd></div><div><dt>Rule ID</dt><dd>{{ selectedAlert.ruleId ?? 'Unavailable' }}</dd></div><div><dt>Scoring version</dt><dd>{{ selectedAlert.scoringVersion ?? 'Unavailable' }}</dd></div></dl>
         </section>

         <section class="detail-section" aria-labelledby="ownership-heading">
           <h3 id="ownership-heading">Ownership</h3>
           <div class="inline-form">
             <label for="owner-subject">Owner subject ID</label>
             <input id="owner-subject" v-model="ownerDraft" maxlength="128" placeholder="Unassigned" :disabled="ownerSaving" />
             <button class="button button-secondary" type="button" :disabled="ownerSaving" @click="saveOwner">{{ ownerSaving ? 'Saving…' : 'Save owner' }}</button>
           </div>
           <p class="field-help">Current owner: {{ selectedAlert.ownerSubjectId ?? 'Unassigned' }}. The server validates tenant membership.</p>
           <p v-if="ownerError" class="inline-error" role="alert">{{ ownerError }}</p>
         </section>

         <section class="detail-section" aria-labelledby="notes-heading">
           <h3 id="notes-heading">Investigation notes</h3>
           <label for="investigation-note" class="sr-only">Investigation note</label>
           <textarea id="investigation-note" v-model="noteDraft" rows="3" maxlength="2000" placeholder="Record what was checked or learned." :disabled="noteSaving" />
           <div class="note-actions"><span class="field-help">{{ noteDraft.trim().length }}/2000 characters; minimum 3.</span><button class="button button-secondary" type="button" :disabled="noteSaving || noteDraft.trim().length < 3" @click="saveNote">{{ noteSaving ? 'Saving…' : 'Add note' }}</button></div>
           <p v-if="noteError" class="inline-error" role="alert">{{ noteError }}</p>
         </section>

         <section class="detail-section" aria-labelledby="timeline-heading">
           <h3 id="timeline-heading">Incident timeline</h3>
           <ol v-if="selectedAlert.timeline.length" class="timeline-list">
             <li v-for="entry in selectedAlert.timeline" :key="entry.id">
               <div class="timeline-heading"><strong>{{ entry.action }}</strong><time :datetime="entry.occurredAt ?? undefined">{{ formatDate(entry.occurredAt) }}</time></div>
               <span class="timeline-actor">{{ entry.actorSubjectId ?? 'Unavailable' }}</span>
               <p v-if="entry.note">{{ entry.note }}</p>
             </li>
           </ol>
           <p v-else class="muted">No investigation activity has been recorded.</p>
         </section>

        <section class="detail-section" aria-labelledby="checks-heading">
          <h3 id="checks-heading">Recommended checks</h3>
          <ul v-if="selectedAlert.recommendedChecks.length"><li v-for="check in selectedAlert.recommendedChecks" :key="check">{{ check }}</li></ul>
          <p v-else class="muted">No recommended checks were supplied.</p>
          <div v-if="selectedAlert.reasonCodes.length" class="reason-codes"><span v-for="code in selectedAlert.reasonCodes" :key="code">{{ code }}</span></div>
        </section>

        <div v-if="mutationError" class="state-banner state-error" role="alert"><CircleAlert :size="17" aria-hidden="true" /><span>{{ mutationError }}<span v-if="mutationCorrelationId"> Correlation ID: {{ mutationCorrelationId }}</span></span></div>
        <div v-if="mutationMessage" class="state-banner state-success" role="status"><Check :size="17" aria-hidden="true" /> {{ mutationMessage }}</div>
         <div class="action-row" v-if="availableIncidentActions(selectedAlert).length">
           <button v-for="status in availableIncidentActions(selectedAlert)" :key="status" class="button" :class="status === 'resolved' || status === 'false_positive' ? 'button-primary' : 'button-secondary'" type="button" @click="requestIncidentAction(status)">
             <CheckCircle2 :size="16" aria-hidden="true" /> {{ actionLabel(`incident:${status}`) }}
           </button>
         </div>
         <div class="action-row" v-if="availableActions(selectedAlert).length">
          <button v-for="action in availableActions(selectedAlert)" :key="action" class="button" :class="action === 'resolve' ? 'button-primary' : 'button-secondary'" type="button" @click="requestAction(action)">
            <RotateCcw v-if="action === 'reopen'" :size="16" aria-hidden="true" /><CheckCircle2 v-else :size="16" aria-hidden="true" />
            {{ actionLabel(action) }}
          </button>
        </div>
        <p v-else class="muted action-unavailable">No lifecycle actions are available for this alert.</p>
      </div>
    </aside>

    <div v-if="pendingAction" class="dialog-backdrop" role="presentation">
      <section ref="dialogRef" class="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirm-heading" aria-describedby="confirm-copy">
        <div class="dialog-icon"><AlertTriangle :size="21" aria-hidden="true" /></div>
         <h2 id="confirm-heading">{{ selectedActionLabel }} incident?</h2>
         <p id="confirm-copy">This records a lifecycle change for “{{ selectedAlert?.title }}”. Add the investigation context for the timeline.</p>
         <label for="mutation-reason">{{ pendingAction?.endsWith('resolved') ? 'Resolution summary' : pendingAction?.endsWith('false_positive') ? 'False-positive reason' : 'Reason' }} <span>(required)</span></label>
         <textarea id="mutation-reason" v-model="reason" rows="4" :maxlength="pendingAction?.startsWith('incident:') ? 2000 : 500" placeholder="What changed or was checked?" :disabled="mutationLoading" />
         <p class="field-help">{{ reason.trim().length }}/{{ pendingAction?.startsWith('incident:') ? 2000 : 500 }} characters; minimum 3.</p>
        <div class="dialog-actions"><button class="button button-secondary" type="button" :disabled="mutationLoading" @click="cancelAction">Cancel</button><button class="button button-primary" type="button" :disabled="!canSubmitMutation" @click="confirmAction"><LoaderCircle v-if="mutationLoading" class="loading-icon" :size="16" /><Check v-else :size="16" aria-hidden="true" /> Confirm {{ selectedActionLabel.toLowerCase() }}</button></div>
      </section>
    </div>
  </main>
</template>

<style scoped>
:global(*) { box-sizing: border-box; }
:global(body) { color: var(--text-primary, #182230); font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif; }
.alerts-page { background: var(--app-bg, #f5f6f8); min-height: 100%; padding: 24px; }
.page-header, .list-heading, .filter-heading, .drawer-header { align-items: flex-start; display: flex; justify-content: space-between; gap: 16px; }
.page-header { margin: 0 auto 24px; max-width: 1440px; }
.eyebrow { color: var(--text-secondary, #667085); font-size: 12px; font-weight: 700; letter-spacing: .08em; margin: 0 0 6px; text-transform: uppercase; }
h1, h2, h3, p { margin-top: 0; }
h1 { font-size: clamp(24px, 3vw, 28px); letter-spacing: -.02em; margin-bottom: 6px; }
h2 { font-size: 17px; margin-bottom: 5px; }
h3 { font-size: 15px; margin-bottom: 8px; }
.page-intro, .filter-heading p, .list-heading p, .muted { color: var(--text-secondary, #667085); font-size: 14px; margin-bottom: 0; }
.button, .text-button, .icon-button, select, input, textarea { font: inherit; }
.button { align-items: center; border: 1px solid transparent; border-radius: 6px; cursor: pointer; display: inline-flex; gap: 8px; justify-content: center; min-height: 40px; padding: 9px 14px; transition: background-color 150ms ease, border-color 150ms ease; }
.button:focus-visible, .text-button:focus-visible, .icon-button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible, tr:focus-visible { outline: 3px solid var(--focus, #2e90fa); outline-offset: 2px; }
.button:disabled { cursor: not-allowed; opacity: .55; }
.button-primary { background: var(--accent, #176b64); color: #fff; }
.button-primary:hover:not(:disabled) { background: var(--accent-hover, #12564f); }
.button-secondary { background: var(--surface, #fff); border-color: var(--border, #e4e7ec); color: var(--text-primary, #182230); }
.button-secondary:hover:not(:disabled) { background: var(--surface-subtle, #f8fafc); }
.text-button { background: transparent; border: 0; color: var(--accent, #176b64); cursor: pointer; font-size: 13px; font-weight: 600; padding: 4px; }
.filter-panel, .list-panel { background: var(--surface, #fff); border: 1px solid var(--border, #e4e7ec); border-radius: 8px; margin: 0 auto 16px; max-width: 1440px; }
.filter-panel { padding: 18px 20px 20px; }
.filter-heading { margin-bottom: 17px; }
.filter-heading h2 { align-items: center; display: flex; gap: 7px; }
.filters { align-items: end; display: grid; gap: 14px; grid-template-columns: repeat(4, minmax(0, 1fr)) auto; }
label { color: var(--text-primary, #182230); display: grid; font-size: 13px; font-weight: 600; gap: 6px; }
label > span:first-child { color: var(--text-secondary, #667085); font-size: 12px; }
input, select, textarea { background: var(--surface, #fff); border: 1px solid var(--border, #e4e7ec); border-radius: 6px; color: var(--text-primary, #182230); min-height: 40px; padding: 8px 10px; width: 100%; }
textarea { resize: vertical; }
.input-with-icon { align-items: center; display: flex; position: relative; }
.input-with-icon svg { color: var(--text-secondary, #667085); left: 10px; position: absolute; pointer-events: none; }
.input-with-icon input { padding-left: 32px; }
.filter-submit { white-space: nowrap; }
.state-banner { align-items: flex-start; border: 1px solid; border-radius: 6px; display: flex; gap: 9px; line-height: 1.45; margin: 0 auto 12px; max-width: 1440px; padding: 11px 13px; }
.state-stale { background: #f8fafc; border-color: var(--border, #e4e7ec); color: var(--text-secondary, #667085); }
.state-partial { background: #fffaf0; border-color: #f2cc8f; color: var(--warning, #a15c07); }
.state-success { background: #f0faf5; border-color: #a6e0bd; color: var(--success, #16794a); }
.state-error { background: #fff7f6; border-color: #f2b8b5; color: var(--danger, #b42318); }
.list-panel { overflow: hidden; }
.list-heading { align-items: center; border-bottom: 1px solid var(--border, #e4e7ec); padding: 18px 20px; }
.loading-icon { animation: spin 900ms linear infinite; }
.table-wrap { overflow-x: auto; }
table { border-collapse: collapse; min-width: 1000px; width: 100%; }
th, td { border-bottom: 1px solid var(--border, #e4e7ec); padding: 15px 14px; text-align: left; vertical-align: middle; }
thead { background: var(--surface-subtle, #f8fafc); }
thead th { color: var(--text-secondary, #667085); font-size: 11px; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; }
tbody tr { cursor: pointer; transition: background-color 150ms ease; }
tbody tr:hover, tbody tr.selected { background: #f2f8f7; }
tbody tr:last-child th, tbody tr:last-child td { border-bottom: 0; }
.alert-title, .alert-id, .metric-value, .metric-baseline { display: block; }
.alert-title { font-weight: 600; }
.alert-id, .metric-baseline { color: var(--text-secondary, #667085); font-size: 12px; margin-top: 4px; }
.metric-value { font-variant-numeric: tabular-nums; }
.inline-error { color: var(--danger, #b42318); font-size: 13px; margin: 0; padding: 12px 16px; }
.state-block { align-items: flex-start; display: flex; flex-direction: column; gap: 8px; min-height: 230px; justify-content: center; margin: 0 auto; max-width: 440px; padding: 38px 24px; text-align: left; }
.state-block > svg { color: var(--text-secondary, #667085); }
.state-block p { margin-bottom: 0; }
.correlation { font-family: ui-monospace, SFMono-Regular, Consolas, monospace; font-size: 12px; overflow-wrap: anywhere; }
.skeleton-table { padding: 8px 20px; }
.skeleton-row { display: grid; gap: 20px; grid-template-columns: 1fr 2fr 1fr 1.5fr 1fr; padding: 18px 0; }
.skeleton-row span { background: #eef1f4; border-radius: 4px; height: 18px; }
.detail-drawer { background: var(--surface, #fff); border-left: 1px solid var(--border, #e4e7ec); box-shadow: -12px 0 30px rgba(16, 24, 40, .08); display: flex; flex-direction: column; height: 100vh; max-width: 560px; overflow-y: auto; padding: 24px; position: fixed; right: 0; top: 0; width: min(100%, 560px); z-index: 20; }
.drawer-header { border-bottom: 1px solid var(--border, #e4e7ec); padding-bottom: 17px; }
.drawer-header h2 { font-size: 19px; line-height: 1.3; max-width: 430px; }
.icon-button { align-items: center; background: var(--surface, #fff); border: 1px solid var(--border, #e4e7ec); border-radius: 6px; color: var(--text-primary, #182230); cursor: pointer; display: inline-flex; height: 40px; justify-content: center; min-width: 40px; }
.drawer-loading { align-items: center; color: var(--text-secondary, #667085); display: flex; gap: 8px; justify-content: center; min-height: 220px; }
.drawer-content { padding-top: 20px; }
.detail-status { align-items: center; display: flex; flex-wrap: wrap; gap: 16px; }
.detail-explanation { font-size: 15px; line-height: 1.55; margin: 18px 0 24px; }
.detail-section { border-top: 1px solid var(--border, #e4e7ec); padding: 19px 0; }
.evidence-grid { display: grid; gap: 14px; grid-template-columns: repeat(2, minmax(0, 1fr)); }
.evidence-grid > div, .context-list > div { background: var(--surface-subtle, #f8fafc); border-radius: 6px; padding: 11px 12px; }
dt { color: var(--text-secondary, #667085); font-size: 12px; margin-bottom: 5px; }
dd { font-size: 14px; font-weight: 600; margin: 0; overflow-wrap: anywhere; }
dd small { color: var(--text-secondary, #667085); display: block; font-size: 11px; font-weight: 400; margin-top: 4px; }
.context-list { display: grid; gap: 8px; margin: 0; }
.context-list > div { align-items: baseline; display: flex; gap: 12px; justify-content: space-between; }
.context-list dt { margin: 0; }
.context-list dd { text-align: right; }
.inline-form { align-items: end; display: grid; gap: 8px; grid-template-columns: minmax(0, 1fr) auto; }
.inline-form label { grid-column: 1 / -1; }
.inline-form input { min-width: 0; }
.note-actions { align-items: center; display: flex; justify-content: space-between; gap: 12px; }
.note-actions .field-help { margin-bottom: 0; }
.timeline-list { list-style: none; margin: 0; padding: 0; }
.timeline-list li { border-left: 2px solid var(--border, #e4e7ec); margin-left: 5px; padding: 0 0 16px 16px; position: relative; }
.timeline-list li::before { background: var(--accent, #176b64); border: 3px solid var(--surface, #fff); border-radius: 50%; content: ''; height: 8px; left: -7px; position: absolute; top: 2px; width: 8px; }
.timeline-heading { align-items: baseline; display: flex; gap: 10px; justify-content: space-between; }
.timeline-heading time, .timeline-actor { color: var(--text-secondary, #667085); font-size: 12px; }
.timeline-actor { display: block; margin-top: 3px; }
.timeline-list p { font-size: 13px; line-height: 1.45; margin: 7px 0 0; overflow-wrap: anywhere; }
.detail-section ul { line-height: 1.5; margin: 0; padding-left: 19px; }
.detail-section li + li { margin-top: 7px; }
.reason-codes { display: flex; flex-wrap: wrap; gap: 6px; margin-top: 15px; }
.reason-codes span { background: #eef6f5; border-radius: 4px; color: var(--accent, #176b64); font-size: 12px; padding: 4px 7px; }
.action-row { display: flex; flex-wrap: wrap; gap: 9px; padding-bottom: 24px; }
.action-unavailable { border-top: 1px solid var(--border, #e4e7ec); padding-top: 18px; }
.dialog-backdrop { align-items: center; background: rgba(16, 24, 40, .42); display: flex; inset: 0; justify-content: center; padding: 20px; position: fixed; z-index: 30; }
.confirm-dialog { background: var(--surface, #fff); border-radius: 10px; box-shadow: 0 16px 48px rgba(16, 24, 40, .2); max-width: 460px; padding: 24px; width: 100%; }
.dialog-icon { align-items: center; background: #fff8eb; border-radius: 50%; color: var(--warning, #a15c07); display: flex; height: 40px; justify-content: center; margin-bottom: 15px; width: 40px; }
.confirm-dialog h2 { font-size: 19px; margin-bottom: 8px; }
.confirm-dialog p { color: var(--text-secondary, #667085); line-height: 1.5; }
.confirm-dialog label { margin-top: 18px; }
.confirm-dialog label span, .field-help { color: var(--text-secondary, #667085); font-size: 12px; font-weight: 400; }
.field-help { margin: 5px 0 17px; }
.dialog-actions { display: flex; justify-content: flex-end; gap: 9px; }
.sr-only { height: 1px; margin: -1px; overflow: hidden; position: absolute; width: 1px; clip: rect(0, 0, 0, 0); }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 1100px) { .filters { grid-template-columns: repeat(3, minmax(0, 1fr)); } .filter-submit { grid-column: span 3; justify-self: start; } }
@media (max-width: 640px) {
  .alerts-page { padding: 16px 12px; }
  .page-header { align-items: stretch; flex-direction: column; }
  .page-header .button { align-self: flex-start; }
  .filter-panel, .list-heading { padding: 16px; }
  .filters { grid-template-columns: 1fr; }
  .filter-submit { grid-column: auto; width: 100%; }
  .list-heading { align-items: flex-start; }
  .table-wrap { overflow: visible; }
  table, tbody, tr, td, th { display: block; min-width: 0; }
  thead { display: none; }
  tbody { padding: 0 14px; }
  tbody tr { border-bottom: 1px solid var(--border, #e4e7ec); padding: 15px 0; }
  tbody tr:last-child { border-bottom: 0; }
  tbody td, tbody th { border: 0; padding: 4px 0; }
  tbody td::before, tbody th::before { color: var(--text-secondary, #667085); content: attr(data-label); display: block; font-size: 11px; font-weight: 700; letter-spacing: .05em; margin-bottom: 3px; text-transform: uppercase; }
  tbody th:first-child::before { content: 'Alert'; }
  .detail-drawer { padding: 18px 16px; }
  .evidence-grid { grid-template-columns: 1fr; }
  .context-list > div { align-items: flex-start; flex-direction: column; gap: 4px; }
  .context-list dd { text-align: left; }
  .dialog-actions { flex-direction: column-reverse; }
  .dialog-actions .button { width: 100%; }
}
@media (prefers-reduced-motion: reduce) { *, *::before, *::after { scroll-behavior: auto !important; transition-duration: 0.01ms !important; animation-duration: 0.01ms !important; } }
</style>
