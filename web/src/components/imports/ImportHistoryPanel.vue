<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import {
  ImportsApiError,
  type ImportHistoryItem,
  type ImportResult,
  type ImportsApi,
} from './importsApi';

const props = defineProps<{
  api: ImportsApi;
  refreshKey?: number;
}>();

const items = ref<ImportHistoryItem[]>([]);
const page = ref(1);
const cursors = ref<Array<string | null>>([null]);
const hasMore = ref(false);
const loading = ref(true);
const error = ref<ImportsApiError | null>(null);
const inspectingId = ref<string | null>(null);
const inspectedResult = ref<ImportResult | null>(null);
const inspectError = ref<ImportsApiError | null>(null);
let requestSequence = 0;

function asApiError(value: unknown, fallback: string): ImportsApiError {
  return value instanceof ImportsApiError ? value : new ImportsApiError(fallback);
}

function formatDateTime(value: string | null): string {
  if (!value) return '—';
  const parsed = new Date(value);
  return Number.isNaN(parsed.valueOf())
    ? value
    : new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(parsed);
}

function statusClass(status: ImportHistoryItem['status'] | ImportResult['status']): string {
  if (status === 'completed') return 'status-success';
  if (status === 'partial') return 'status-warning';
  if (status === 'validated') return 'status-info';
  return 'status-danger';
}

async function loadPage(targetPage: number): Promise<void> {
  const currentRequest = ++requestSequence;
  loading.value = true;
  error.value = null;

  try {
    const response = await props.api.list(cursors.value[targetPage - 1] ?? null);
    if (currentRequest !== requestSequence) return;
    items.value = response.items;
    hasMore.value = response.hasMore;
    page.value = targetPage;
    cursors.value[targetPage] = response.nextCursor;
  } catch (caught) {
    if (currentRequest !== requestSequence) return;
    error.value = asApiError(caught, 'Import history could not be loaded.');
  } finally {
    if (currentRequest === requestSequence) {
      loading.value = false;
    }
  }
}

async function inspectBatch(item: ImportHistoryItem): Promise<void> {
  inspectingId.value = item.id;
  inspectError.value = null;

  try {
    inspectedResult.value = await props.api.get(item.id);
  } catch (caught) {
    inspectError.value = asApiError(caught, 'Import batch details could not be loaded.');
  } finally {
    if (inspectingId.value === item.id) {
      inspectingId.value = null;
    }
  }
}

function closeInspection(): void {
  inspectedResult.value = null;
  inspectError.value = null;
}

function refresh(): void {
  cursors.value = [null];
  loadPage(1);
}

function goToPage(nextPage: number): void {
  if (loading.value || nextPage < 1 || nextPage === page.value) return;
  loadPage(nextPage);
}

watch(() => props.refreshKey, () => {
  refresh();
});

onMounted(() => {
  loadPage(1);
});
</script>

<template>
  <section class="panel history-panel" aria-labelledby="import-history-heading">
    <div class="history-header">
      <div>
        <p class="eyebrow">History</p>
        <h2 id="import-history-heading">Recent import batches</h2>
        <p class="supporting-copy">Review prior CSV uploads and inspect server-safe row rejections.</p>
      </div>
      <button
        type="button"
        class="button button-secondary"
        :disabled="loading"
        :aria-busy="loading"
        @click="refresh"
      >
        {{ loading ? 'Refreshing…' : 'Refresh' }}
      </button>
    </div>

    <div v-if="error" class="error-banner" role="alert">
      <div>
        <strong>Import history unavailable</strong>
        <p>{{ error.message }}</p>
        <p v-if="error.correlationId" class="correlation">Correlation ID: <code>{{ error.correlationId }}</code></p>
      </div>
      <button type="button" class="button button-secondary" @click="loadPage(page)">Retry</button>
    </div>

    <div v-else-if="loading && !items.length" class="loading-list" role="status" aria-live="polite" aria-label="Loading import history">
      <div v-for="index in 3" :key="index" class="skeleton-row">
        <span /><span /><span /><span />
      </div>
    </div>

    <div v-else-if="!items.length" class="empty-state">
      <h3>No import batches recorded yet</h3>
      <p>Uploaded and confirmed CSV imports for this tenant will appear here.</p>
    </div>

    <template v-else>
      <div class="table-wrap" :aria-busy="loading">
        <table>
          <caption class="sr-only">Recent import batches</caption>
          <thead>
            <tr>
              <th scope="col">File</th>
              <th scope="col">Uploaded</th>
              <th scope="col">Status</th>
              <th scope="col" class="numeric">Accepted</th>
              <th scope="col" class="numeric">Rejected</th>
              <th scope="col"><span class="sr-only">Actions</span></th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in items" :key="item.id">
              <th scope="row" class="file-cell">{{ item.fileName }}</th>
              <td>{{ formatDateTime(item.uploadedAt) }}</td>
              <td><span class="status" :class="statusClass(item.status)">{{ item.status }}</span></td>
              <td class="numeric">{{ item.acceptedRows.toLocaleString() }}</td>
              <td class="numeric" :class="{ 'text-danger': item.rejectedRows > 0 }">{{ item.rejectedRows.toLocaleString() }}</td>
              <td class="actions-cell">
                <button
                  type="button"
                  class="button button-secondary button-compact"
                  :disabled="inspectingId === item.id"
                  :aria-busy="inspectingId === item.id"
                  @click="inspectBatch(item)"
                >
                  {{ inspectingId === item.id ? 'Loading…' : 'Inspect' }}
                </button>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <nav class="pagination" aria-label="Import history pages">
        <span class="pagination-page" aria-live="polite">Page {{ page }}</span>
        <div class="pagination-actions">
          <button
            type="button"
            class="button button-secondary button-compact"
            :disabled="loading || page <= 1"
            @click="goToPage(page - 1)"
          >
            Previous
          </button>
          <button
            type="button"
            class="button button-secondary button-compact"
            :disabled="loading || !hasMore"
            @click="goToPage(page + 1)"
          >
            Next
          </button>
        </div>
      </nav>
    </template>

    <div v-if="inspectError" class="error-banner inspect-error" role="alert">
      <div>
        <strong>Could not inspect import batch</strong>
        <p>{{ inspectError.message }}</p>
        <p v-if="inspectError.correlationId" class="correlation">Correlation ID: <code>{{ inspectError.correlationId }}</code></p>
      </div>
      <button type="button" class="button button-secondary button-compact" @click="closeInspection">Dismiss</button>
    </div>

    <section v-if="inspectedResult" class="inspect-panel" aria-labelledby="inspect-heading" aria-live="polite">
      <div class="inspect-header">
        <div>
          <p class="eyebrow">Batch details</p>
          <h3 id="inspect-heading">{{ inspectedResult.fileName }}</h3>
          <p class="supporting-copy">
            Status: <span class="status" :class="statusClass(inspectedResult.status)">{{ inspectedResult.status }}</span>
            · Uploaded {{ formatDateTime(inspectedResult.completedAt) }}
            · {{ inspectedResult.acceptedRows.toLocaleString() }} accepted,
            {{ inspectedResult.rejectedRows.toLocaleString() }} rejected
          </p>
        </div>
        <button type="button" class="button button-secondary button-compact" @click="closeInspection">Close</button>
      </div>

      <div v-if="inspectedResult.rejections.length" class="table-wrap">
        <table>
          <caption class="sr-only">Row errors for inspected import batch</caption>
          <thead>
            <tr>
              <th scope="col">Row</th>
              <th scope="col">Error code</th>
              <th scope="col">Message</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="rejection in inspectedResult.rejections" :key="`${rejection.rowNumber}-${rejection.errorCode}`">
              <th scope="row">{{ rejection.rowNumber }}</th>
              <td><code>{{ rejection.errorCode }}</code></td>
              <td>{{ rejection.message }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-else class="supporting-copy">No row errors were recorded for this import.</p>
    </section>
  </section>
</template>

<style scoped>
.panel {
  margin-top: 24px;
  padding: 24px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
}

.history-header,
.inspect-header,
.pagination {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
}

.history-header {
  margin-bottom: 18px;
}

.eyebrow {
  margin: 0 0 4px;
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h2,
h3,
p {
  margin-top: 0;
}

h2 {
  margin-bottom: 4px;
  color: var(--text-primary, #182230);
  font-size: 18px;
}

h3 {
  margin-bottom: 6px;
  color: var(--text-primary, #182230);
  font-size: 15px;
}

.supporting-copy {
  margin-bottom: 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.button {
  min-height: 40px;
  padding: 8px 14px;
  border: 1px solid transparent;
  border-radius: 6px;
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.button-compact {
  min-height: 36px;
  padding: 6px 12px;
  font-size: 13px;
}

.button-secondary {
  border-color: var(--border, #e4e7ec);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
}

.button-secondary:hover:not(:disabled) {
  background: var(--surface-subtle, #f8fafc);
}

.button:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent);
  outline-offset: 2px;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
}

table {
  width: 100%;
  min-width: 620px;
  border-collapse: collapse;
  font-size: 13px;
}

th,
td {
  padding: 11px 12px;
  border-bottom: 1px solid var(--border, #e4e7ec);
  text-align: left;
  vertical-align: middle;
}

thead th {
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 650;
  white-space: nowrap;
}

tbody tr:last-child th,
tbody tr:last-child td {
  border-bottom: 0;
}

.file-cell {
  font-weight: 600;
  color: var(--text-primary, #182230);
}

.numeric {
  text-align: right;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

.text-danger {
  color: var(--danger, #b42318);
  font-weight: 600;
}

.actions-cell {
  text-align: right;
  white-space: nowrap;
}

.status {
  display: inline-flex;
  align-items: center;
  padding: 3px 8px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 650;
  text-transform: capitalize;
}

.status-success { background: #ecfdf3; color: var(--success, #16794a); }
.status-warning { background: #fffaeb; color: var(--warning, #a15c07); }
.status-info { background: #eff8ff; color: var(--info, #175cd3); }
.status-danger { background: #fef3f2; color: var(--danger, #b42318); }

.pagination {
  align-items: center;
  margin-top: 14px;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.pagination-actions {
  display: flex;
  gap: 8px;
}

.error-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
  border: 1px solid #fecdca;
  border-left: 3px solid var(--danger, #b42318);
  border-radius: 6px;
  background: #fef3f2;
}

.error-banner p {
  margin: 4px 0 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.inspect-error {
  margin-top: 16px;
}

.correlation {
  font-size: 12px;
}

code {
  color: var(--text-primary, #182230);
  font-size: 12px;
}

.loading-list {
  display: grid;
  gap: 10px;
}

.skeleton-row {
  display: grid;
  grid-template-columns: 2fr 1fr 1fr 1fr;
  gap: 12px;
  padding: 12px 0;
  border-bottom: 1px solid var(--border, #e4e7ec);
}

.skeleton-row span {
  height: 14px;
  border-radius: 4px;
  background: var(--surface-subtle, #f8fafc);
}

.empty-state {
  padding: 28px 16px;
  border: 1px dashed var(--border, #e4e7ec);
  border-radius: 6px;
  text-align: center;
}

.empty-state p {
  margin-bottom: 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.inspect-panel {
  margin-top: 20px;
  padding: 18px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: var(--surface-subtle, #f8fafc);
}

.inspect-header {
  margin-bottom: 14px;
}

.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

@media (max-width: 640px) {
  .panel { padding: 16px; }
  .history-header,
  .inspect-header,
  .error-banner,
  .pagination {
    flex-direction: column;
    align-items: stretch;
  }
  .pagination-actions .button {
    flex: 1;
  }
}
</style>
