<script setup lang="ts">
import type { ImportPreview } from './importsApi';

defineProps<{
  preview: ImportPreview;
  busy: boolean;
  stale: boolean;
}>();

const emit = defineEmits<{
  confirm: [];
  chooseFile: [];
}>();
</script>

<template>
  <section class="panel" aria-labelledby="preview-heading">
    <div class="panel-heading">
      <div>
        <p class="eyebrow">Step 2</p>
        <h2 id="preview-heading">Review the server preview</h2>
      </div>
      <span v-if="busy" class="status status-info" role="status" aria-live="polite">Confirming import</span>
      <span v-else-if="stale" class="status status-warning">Preview expired</span>
    </div>

    <div v-if="stale" class="callout callout-warning" role="alert">
      <strong>This preview is no longer valid.</strong>
      <p>Select the CSV again to create a fresh preview before importing.</p>
    </div>

    <p class="file-name" :title="preview.fileName">{{ preview.fileName }}</p>

    <dl class="summary-grid" aria-label="Preview row counts">
      <div>
        <dt>Total rows</dt>
        <dd>{{ preview.totalRows.toLocaleString() }}</dd>
      </div>
      <div>
        <dt>Ready to import</dt>
        <dd class="text-success">{{ preview.acceptedRows.toLocaleString() }}</dd>
      </div>
      <div>
        <dt>Rejected</dt>
        <dd :class="{ 'text-danger': preview.rejectedRows > 0 }">{{ preview.rejectedRows.toLocaleString() }}</dd>
      </div>
    </dl>

    <div v-if="preview.rejectedRows > 0" class="rejection-block">
      <div class="section-heading">
        <div>
          <h3>Rows needing attention</h3>
          <p>Only row numbers, error codes and server-safe messages are shown. Uploaded row values are not displayed.</p>
        </div>
      </div>
      <div class="table-wrap">
        <table>
          <caption class="sr-only">Rejected CSV rows</caption>
          <thead>
            <tr>
              <th scope="col">Row</th>
              <th scope="col">Error code</th>
              <th scope="col">Message</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="rejection in preview.rejections" :key="`${rejection.rowNumber}-${rejection.errorCode}`">
              <th scope="row">{{ rejection.rowNumber }}</th>
              <td><code>{{ rejection.errorCode }}</code></td>
              <td>{{ rejection.message }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <p v-if="preview.rejectionsTruncated || preview.rejections.length < preview.rejectedRows" class="muted-note">
        Showing the safe rejection details returned by the server. The count above includes every rejected row.
      </p>
      <p v-if="preview.rejections.length === 0" class="empty-note">The server did not return row-level details for these rejections.</p>
    </div>
    <p v-else class="callout callout-success">All rows passed validation and are ready to import.</p>

    <p v-if="preview.acceptedRows === 0" class="callout callout-warning" role="status">
      No valid rows are available. Correct the CSV and select it again before confirming.
    </p>

    <div class="actions">
      <button type="button" class="button button-secondary" :disabled="busy" @click="emit('chooseFile')">Choose another file</button>
      <button
        type="button"
        class="button button-primary"
        :disabled="busy || stale || preview.acceptedRows === 0"
        :aria-busy="busy"
        @click="emit('confirm')"
      >
        {{ busy ? 'Confirming…' : 'Confirm import' }}
      </button>
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
.section-heading,
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
p {
  margin-top: 0;
}

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
  font-weight: 600;
}

.status-info {
  background: #eff8ff;
  color: var(--info, #175cd3);
}

.status-warning {
  background: #fffaeb;
  color: var(--warning, #a15c07);
}

.file-name {
  margin: 20px 0 16px;
  overflow: hidden;
  color: var(--text-primary, #182230);
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

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

dt {
  color: var(--text-secondary, #667085);
  font-size: 12px;
}

dd {
  margin: 6px 0 0;
  color: var(--text-primary, #182230);
  font-size: 24px;
  font-variant-numeric: tabular-nums;
  font-weight: 650;
}

.text-success { color: var(--success, #16794a); }
.text-danger { color: var(--danger, #b42318); }

.callout {
  margin: 16px 0 0;
  padding: 12px 14px;
  border-left: 3px solid var(--info, #175cd3);
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-secondary, #667085);
}

.callout p {
  margin: 4px 0 0;
}

.callout-success {
  border-left-color: var(--success, #16794a);
}

.callout-warning {
  border-left-color: var(--warning, #a15c07);
}

.callout strong {
  color: var(--text-primary, #182230);
}

.rejection-block {
  margin-top: 20px;
}

.section-heading p,
.muted-note,
.empty-note {
  margin-bottom: 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.table-wrap {
  margin-top: 12px;
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

thead th {
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 650;
}

tbody tr:last-child th,
tbody tr:last-child td {
  border-bottom: 0;
}

code {
  color: var(--text-primary, #182230);
  font-size: 12px;
}

.muted-note,
.empty-note {
  margin-top: 8px;
}

.actions {
  align-items: center;
  margin-top: 24px;
}

.button {
  min-height: 44px;
  padding: 10px 14px;
  border: 1px solid transparent;
  border-radius: 6px;
  font: inherit;
  font-weight: 650;
  cursor: pointer;
}

.button:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

.button-primary {
  background: var(--accent, #176b64);
  color: #fff;
}

.button-primary:hover:not(:disabled) { background: var(--accent-hover, #12564f); }

.button-secondary {
  border-color: var(--border, #e4e7ec);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
}

.button-secondary:hover:not(:disabled) { background: var(--surface-subtle, #f8fafc); }

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
