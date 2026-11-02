<script setup lang="ts">
import type { ImportResult } from './importsApi';

defineProps<{
  result: ImportResult;
}>();

const emit = defineEmits<{
  startOver: [];
}>();

function headingFor(status: ImportResult['status']): string {
  if (status === 'completed') return 'Import completed';
  if (status === 'partial') return 'Import completed with rejected rows';
  return 'Import could not be completed';
}

function statusClass(status: ImportResult['status']): string {
  if (status === 'completed') return 'status-success';
  if (status === 'partial') return 'status-warning';
  return 'status-danger';
}

function completedAtLabel(value: string | null): string {
  if (!value) return 'time not provided by the API';
  const date = new Date(value);
  return Number.isNaN(date.valueOf()) ? value : date.toLocaleString();
}
</script>

<template>
  <section class="panel" aria-labelledby="result-heading" aria-live="polite">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">Step 3</p>
        <h2 id="result-heading">{{ headingFor(result.status) }}</h2>
      </div>
      <span class="status" :class="statusClass(result.status)">{{ result.status }}</span>
    </div>

    <p class="file-name" :title="result.fileName">{{ result.fileName }}</p>

    <div class="callout" :class="`callout-${result.status}`">
      <strong v-if="result.status === 'completed'">All accepted rows are now part of this tenant’s metrics.</strong>
      <strong v-else-if="result.status === 'partial'">Accepted rows were imported; rejected rows were not written.</strong>
      <strong v-else>No rows were written by this import.</strong>
      <p>Completed {{ completedAtLabel(result.completedAt) }}.</p>
    </div>

    <dl class="summary-grid" aria-label="Import result counts">
      <div>
        <dt>Total rows</dt>
        <dd>{{ result.totalRows.toLocaleString() }}</dd>
      </div>
      <div>
        <dt>Imported</dt>
        <dd class="text-success">{{ result.acceptedRows.toLocaleString() }}</dd>
      </div>
      <div>
        <dt>Rejected</dt>
        <dd :class="{ 'text-danger': result.rejectedRows > 0 }">{{ result.rejectedRows.toLocaleString() }}</dd>
      </div>
    </dl>

    <div v-if="result.rejectedRows > 0" class="rejection-block">
      <h3>Rejected rows</h3>
      <p class="supporting-copy">Only server-safe row-level details are shown. Raw uploaded row values are never rendered here.</p>
      <div class="table-wrap">
        <table>
          <caption class="sr-only">Rejected rows from the completed import</caption>
          <thead>
            <tr>
              <th scope="col">Row</th>
              <th scope="col">Error code</th>
              <th scope="col">Message</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="rejection in result.rejections" :key="`${rejection.rowNumber}-${rejection.errorCode}`">
              <th scope="row">{{ rejection.rowNumber }}</th>
              <td><code>{{ rejection.errorCode }}</code></td>
              <td>{{ rejection.message }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="result.rejectionsTruncated || result.rejections.length < result.rejectedRows" class="muted-note">
        Showing the safe rejection details returned by the server. The count above includes every rejected row.
      </p>
      <p v-if="result.rejections.length === 0" class="empty-note">The server did not return row-level details for these rejections.</p>
    </div>

    <div class="actions">
      <button type="button" class="button button-primary" @click="emit('startOver')">Import another CSV</button>
    </div>
  </section>
</template>

<style scoped>
.panel {
  padding: 24px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
}

.panel-heading,
.actions {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
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
p { margin-top: 0; }

h2 {
  margin-bottom: 0;
  color: var(--text-primary, #182230);
  font-size: 18px;
}

h3 {
  margin-bottom: 4px;
  color: var(--text-primary, #182230);
  font-size: 16px;
}

.status {
  padding: 5px 8px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 650;
}

.status-success { background: #ecfdf3; color: var(--success, #16794a); }
.status-warning { background: #fffaeb; color: var(--warning, #a15c07); }
.status-danger { background: #fef3f2; color: var(--danger, #b42318); }

.file-name {
  margin: 20px 0 16px;
  overflow: hidden;
  color: var(--text-primary, #182230);
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.callout {
  margin: 16px 0;
  padding: 12px 14px;
  border-left: 3px solid var(--info, #175cd3);
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-secondary, #667085);
}

.callout p { margin: 4px 0 0; }
.callout strong { color: var(--text-primary, #182230); }
.callout-completed { border-left-color: var(--success, #16794a); }
.callout-partial { border-left-color: var(--warning, #a15c07); }
.callout-failed { border-left-color: var(--danger, #b42318); }

.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 12px;
  margin: 0 0 24px;
}

.summary-grid div {
  padding: 16px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: var(--surface-subtle, #f8fafc);
}

dt { color: var(--text-secondary, #667085); font-size: 12px; }
dd {
  margin: 6px 0 0;
  color: var(--text-primary, #182230);
  font-size: 24px;
  font-variant-numeric: tabular-nums;
  font-weight: 650;
}

.text-success { color: var(--success, #16794a); }
.text-danger { color: var(--danger, #b42318); }

.rejection-block { margin-top: 20px; }
.supporting-copy,
.muted-note,
.empty-note { color: var(--text-secondary, #667085); font-size: 13px; }
.supporting-copy { margin-bottom: 12px; }
.muted-note,
.empty-note { margin: 8px 0 0; }

.table-wrap {
  overflow-x: auto;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
}

table {
  width: 100%;
  min-width: 560px;
  border-collapse: collapse;
  font-size: 13px;
}

th,
td {
  padding: 10px 12px;
  border-bottom: 1px solid var(--border, #e4e7ec);
  text-align: left;
  vertical-align: top;
}

thead th { background: var(--surface-subtle, #f8fafc); color: var(--text-secondary, #667085); font-size: 12px; }
tbody tr:last-child th,
tbody tr:last-child td { border-bottom: 0; }

code { color: var(--text-primary, #182230); font-size: 12px; }

.actions { align-items: center; margin-top: 24px; }
.button {
  min-height: 44px;
  padding: 10px 14px;
  border: 1px solid transparent;
  border-radius: 6px;
  background: var(--accent, #176b64);
  color: #fff;
  font: inherit;
  font-weight: 650;
  cursor: pointer;
}

.button:hover { background: var(--accent-hover, #12564f); }
.button:focus-visible { outline: 3px solid var(--focus, #2e90fa); outline-offset: 2px; }

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

@media (max-width: 600px) {
  .panel { padding: 16px; }
  .panel-heading,
  .actions { flex-direction: column; }
  .actions .button { width: 100%; }
  .summary-grid { grid-template-columns: 1fr; }
}
</style>
