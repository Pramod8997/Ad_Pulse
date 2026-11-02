<script setup lang="ts">
import { computed, inject, onMounted, ref } from 'vue'
import AuditFilters, { type AuditFilterModel } from '../components/audit/AuditFilters.vue'
import AuditPagination from '../components/audit/AuditPagination.vue'
import AuditTable, { type AuditEvent } from '../components/audit/AuditTable.vue'
import { createAuditApi, type AuditListQuery, type AuditPage } from '../components/audit/auditApi'

export interface TenantApiError {
  status?: number
  correlationId?: string
  message?: string
}

export interface AuditApi {
  list(query: AuditListQuery, options?: { signal?: AbortSignal }): Promise<AuditPage>
}

export interface TenantApi {
  audit: AuditApi
}

type LoadState = 'loading' | 'ready' | 'empty' | 'error' | 'forbidden' | 'unauthorized'

const injectedApi = inject<TenantApi | undefined>('adpulse-api')
const api = injectedApi?.audit ? injectedApi : { audit: createAuditApi() }
const pageSize = 25
const page = ref(1)
const filters = ref<AuditFilterModel>({ from: '', to: '', actor: '', action: '', entity: '' })
const validationMessage = ref('')
const events = ref<AuditEvent[]>([])
const state = ref<LoadState>('loading')
const failure = ref<TenantApiError | null>(null)
const lastSuccessfulAt = ref('')
const stale = ref(false)
const partial = ref(false)
const isRefreshing = ref(false)
const cursors = ref<Record<number, string>>({})
const hasMore = ref(false)
let requestNumber = 0
let activeController: AbortController | undefined

const hasEvents = computed(() => events.value.length > 0)
const isInitialLoading = computed(() => state.value === 'loading' && !hasEvents.value)
const canUseAudit = computed(() => state.value !== 'forbidden' && state.value !== 'unauthorized')
const hasFilters = computed(() => Object.values(filters.value).some(Boolean))

function errorDetails(error: unknown): TenantApiError {
  const candidate = (error ?? {}) as { status?: number; response?: { status?: number }; correlationId?: string; message?: string }
  const status = candidate.status ?? candidate.response?.status
  if (status === 401) {
    return { status, message: 'Your session is not authorized to view audit activity.' }
  }
  if (status === 403) {
    return { status, message: 'You do not have permission to view this tenant’s audit log.' }
  }
  return {
    status,
    correlationId: candidate.correlationId,
    message: 'Audit activity could not be loaded. Try again or contact support if the problem persists.',
  }
}

function queryForCurrentPage(): AuditListQuery {
  const query: AuditListQuery = { limit: pageSize }
  if (cursors.value[page.value]) query.cursor = cursors.value[page.value]
  // Tenant scope is supplied by the authenticated API client, never by this query.
  if (filters.value.from) query.from = filters.value.from
  if (filters.value.to) query.to = filters.value.to
  if (filters.value.actor.trim()) query.actorSubjectId = filters.value.actor.trim()
  if (filters.value.action.trim()) query.action = filters.value.action.trim()
  if (filters.value.entity.trim()) query.entityType = filters.value.entity.trim()
  return query
}

async function load() {
  activeController?.abort()
  const controller = new AbortController()
  activeController = controller
  const currentRequest = ++requestNumber
  isRefreshing.value = true
  if (!hasEvents.value) state.value = 'loading'
  failure.value = null

  try {
    const result = await api.audit.list(queryForCurrentPage(), { signal: controller.signal })
    if (currentRequest !== requestNumber) return
    events.value = result.items
    hasMore.value = result.hasMore
    if (result.nextCursor) cursors.value[page.value + 1] = result.nextCursor
    partial.value = result.partial === true
    lastSuccessfulAt.value = result.updatedAt ?? new Date().toISOString()
    stale.value = result.stale === true || isOlderThanFiveMinutes(result.updatedAt)
    state.value = result.items.length === 0 ? 'empty' : 'ready'
  } catch (error) {
    if (controller.signal.aborted || currentRequest !== requestNumber) return
    failure.value = errorDetails(error)
    if (hasEvents.value) stale.value = true
    if (failure.value.status === 401) state.value = 'unauthorized'
    else if (failure.value.status === 403) state.value = 'forbidden'
    else state.value = 'error'
  } finally {
    if (currentRequest === requestNumber) isRefreshing.value = false
  }
}

function isOlderThanFiveMinutes(value?: string) {
  if (!value) return false
  const timestamp = Date.parse(value)
  return !Number.isNaN(timestamp) && Date.now() - timestamp > 5 * 60 * 1000
}

function applyFilters() {
  validationMessage.value = ''
  if (filters.value.from && filters.value.to && filters.value.from > filters.value.to) {
    validationMessage.value = 'The start date must be on or before the end date.'
    return
  }
  page.value = 1
  cursors.value = {}
  load()
}

function clearFilters() {
  validationMessage.value = ''
  filters.value = { from: '', to: '', actor: '', action: '', entity: '' }
  page.value = 1
  cursors.value = {}
  load()
}

function changePage(nextPage: number) {
  page.value = nextPage
  load()
}

function formatRefreshTime(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.valueOf()) ? 'an unknown time' : date.toLocaleString()
}

onMounted(load)
</script>

<template>
  <main class="audit-page" aria-labelledby="audit-title">
    <header class="page-header">
      <div>
        <p class="eyebrow">Workspace governance</p>
        <h1 id="audit-title">Audit log</h1>
        <p class="lede">Read-only activity recorded for your authorized tenant workspace.</p>
      </div>
      <button
        type="button"
        class="refresh-button"
        :disabled="!canUseAudit || isRefreshing"
        :aria-busy="isRefreshing"
        @click="load"
      >
        {{ isRefreshing ? 'Refreshing…' : 'Refresh' }}
      </button>
    </header>

    <section v-if="state === 'forbidden' || state === 'unauthorized'" class="state-panel authorization-state" role="alert">
      <h2>{{ state === 'forbidden' ? 'Audit access is restricted' : 'Sign-in is required' }}</h2>
      <p>{{ failure?.message }}</p>
      <p class="disabled-help">Filtering and pagination are disabled until this access issue is resolved.</p>
    </section>

    <template v-else>
      <section class="panel" aria-labelledby="audit-filters-title">
        <div class="section-heading">
          <div>
            <h2 id="audit-filters-title">Filter activity</h2>
            <p>Results are scoped by the server to the current authorized workspace.</p>
          </div>
        </div>
        <AuditFilters
          v-model="filters"
          :disabled="!canUseAudit"
          :validation-message="validationMessage"
          @apply="applyFilters"
          @reset="clearFilters"
        />
      </section>

      <p v-if="failure && (hasEvents || state === 'error')" class="inline-error" role="alert">
        {{ failure.message }}
        <span v-if="failure.correlationId"> Reference: {{ failure.correlationId }}</span>
        <button type="button" class="inline-action" :disabled="isRefreshing" @click="load">Retry</button>
      </p>

      <section class="panel results-panel" aria-labelledby="audit-results-title">
        <div class="section-heading results-heading">
          <div>
            <h2 id="audit-results-title">Recorded activity</h2>
            <p v-if="lastSuccessfulAt" :class="['refresh-note', { 'stale-note': stale }]" role="status">
              {{ stale ? 'Showing stale data. ' : '' }}Last successful refresh {{ formatRefreshTime(lastSuccessfulAt) }}.
            </p>
          </div>
          <span v-if="partial" class="status-label">Partial response</span>
        </div>

        <AuditTable v-if="isInitialLoading || hasEvents" :events="events" :busy="isInitialLoading" :partial="partial" />

        <div v-else-if="state === 'empty'" class="empty-state">
          <h3>No audit activity found</h3>
          <p>{{ hasFilters ? 'Try clearing one or more filters.' : 'No audit records have been recorded for this workspace yet.' }}</p>
          <button v-if="hasFilters" type="button" class="secondary-button" @click="clearFilters">Clear filters</button>
        </div>

        <div v-else-if="state === 'error'" class="empty-state error-empty" role="alert">
          <h3>Audit log unavailable</h3>
          <p>{{ failure?.message }}</p>
          <button type="button" class="secondary-button" :disabled="isRefreshing" @click="load">Try again</button>
        </div>

        <AuditPagination
          v-if="hasEvents || state === 'empty'"
          :page="page"
          :has-more="hasMore"
      :has-previous="page > 1"
          :disabled="!canUseAudit || isRefreshing"
          @page-change="changePage"
        />
      </section>
    </template>
  </main>
</template>

<style scoped>
.audit-page {
  display: grid;
  gap: 20px;
  max-width: 1440px;
  margin: 0 auto;
  padding: 24px;
  color: var(--text-primary);
}

.page-header,
.section-heading,
.results-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.eyebrow {
  margin: 0 0 6px;
  color: var(--accent);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h1,
h2,
h3,
p {
  margin-top: 0;
}

h1 {
  margin-bottom: 6px;
  font-size: clamp(24px, 3vw, 28px);
  line-height: 1.2;
}

h2 {
  margin-bottom: 4px;
  font-size: 17px;
}

h3 {
  margin-bottom: 8px;
  font-size: 16px;
}

.lede,
.section-heading p,
.empty-state p,
.state-panel p {
  margin-bottom: 0;
  color: var(--text-secondary);
  font-size: 14px;
}

.panel,
.state-panel {
  display: grid;
  gap: 18px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  padding: 20px;
}

.state-panel {
  border-color: color-mix(in srgb, var(--danger) 35%, var(--border));
}

.refresh-button,
.secondary-button,
.inline-action {
  min-height: 40px;
  border: 1px solid var(--accent);
  border-radius: 6px;
  background: var(--accent);
  color: var(--surface);
  cursor: pointer;
  font: inherit;
  font-weight: 600;
  padding: 8px 14px;
}

.refresh-button:hover:not(:disabled),
.inline-action:hover:not(:disabled) {
  background: var(--accent-hover);
}

.secondary-button {
  border-color: var(--border);
  background: var(--surface);
  color: var(--text-primary);
}

.secondary-button:hover:not(:disabled) {
  background: var(--surface-subtle);
}

button:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.6;
}

.disabled-help,
.refresh-note {
  margin-bottom: 0;
  color: var(--text-secondary);
  font-size: 13px;
}

.inline-error {
  margin: 0;
  border-left: 3px solid var(--danger);
  background: var(--surface-subtle);
  color: var(--danger);
  font-size: 13px;
  padding: 12px 14px;
}

.inline-action {
  min-height: auto;
  margin-left: 8px;
  padding: 4px 8px;
}

.stale-note {
  color: var(--warning);
}

.status-label {
  border-radius: 999px;
  background: var(--surface-subtle);
  color: var(--warning);
  font-size: 12px;
  font-weight: 600;
  padding: 5px 9px;
  white-space: nowrap;
}

.empty-state {
  border: 1px dashed var(--border);
  border-radius: 6px;
  padding: 28px 20px;
  text-align: center;
}

.empty-state .secondary-button {
  margin-top: 16px;
}

.error-empty {
  border-color: var(--danger);
  text-align: left;
}

@media (max-width: 640px) {
  .audit-page {
    padding: 16px 12px;
  }

  .page-header,
  .results-heading {
    align-items: stretch;
    flex-direction: column;
  }

  .refresh-button {
    width: 100%;
  }

  .panel,
  .state-panel {
    padding: 16px;
  }
}
</style>
