<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import ImportPreviewPanel from '../components/imports/ImportPreviewPanel.vue';
import ImportResultPanel from '../components/imports/ImportResultPanel.vue';
import ImportStepIndicator from '../components/imports/ImportStepIndicator.vue';
import {
  createImportsApi,
  IMPORT_POLICY,
  ImportsApiError,
  type ImportPolicy,
  type ImportsApi,
  type ImportPreview,
  type ImportResult,
} from '../components/imports/importsApi';

const props = defineProps<{
  api?: ImportsApi;
}>();

const api = computed(() => props.api ?? createImportsApi());
const policy = ref<ImportPolicy | null>(null);
const selectedFile = ref<File | null>(null);
const preview = ref<ImportPreview | null>(null);
const result = ref<ImportResult | null>(null);
const fileInput = ref<HTMLInputElement | null>(null);
const error = ref<ImportsApiError | null>(null);
const errorStage = ref<'policy' | 'template' | 'preview' | 'confirm' | null>(null);
const loadingPolicy = ref(true);
const downloadingTemplate = ref(false);
const loadingPreview = ref(false);
const confirming = ref(false);
const forceStalePreview = ref(false);
const clock = ref(Date.now());
let clockTimer: number | undefined;

const previewIsStale = computed(() => {
  if (!preview.value) {
    return false;
  }

  const expiresAt = Date.parse(preview.value.expiresAt);
  return forceStalePreview.value || (Number.isFinite(expiresAt) && expiresAt <= clock.value);
});

const currentStep = computed<'select' | 'preview' | 'result'>(() => {
  if (result.value) return 'result';
  if (preview.value) return 'preview';
  return 'select';
});

const isBusy = computed(() => loadingPreview.value || confirming.value || downloadingTemplate.value);

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} bytes`;
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatFileName(file: File): string {
  return file.name.trim().length > 0 ? file.name : 'selected CSV file';
}

function asApiError(value: unknown, fallback: string): ImportsApiError {
  if (value instanceof ImportsApiError) {
    return value;
  }

  return new ImportsApiError(fallback);
}

function clearError(): void {
  error.value = null;
  errorStage.value = null;
}

function setError(value: unknown, fallback: string, stage: NonNullable<typeof errorStage.value>): void {
  error.value = asApiError(value, fallback);
  errorStage.value = stage;
}

async function loadPolicy(): Promise<void> {
  loadingPolicy.value = true;
  clearError();
  policy.value = IMPORT_POLICY;
  loadingPolicy.value = false;
}

async function downloadTemplate(): Promise<void> {
  downloadingTemplate.value = true;
  clearError();

  try {
    const download = await api.value.downloadTemplate();
    const objectUrl = URL.createObjectURL(download.blob);
    const link = document.createElement('a');
    link.href = objectUrl;
    link.download = download.fileName;
    link.click();
    URL.revokeObjectURL(objectUrl);
  } catch (value) {
    setError(value, 'The CSV template could not be downloaded. Try again.', 'template');
  } finally {
    downloadingTemplate.value = false;
  }
}

function resetInput(): void {
  if (fileInput.value) {
    fileInput.value.value = '';
  }
}

function focusFileInput(): void {
  fileInput.value?.focus();
}

function chooseAnotherFile(): void {
  selectedFile.value = null;
  preview.value = null;
  result.value = null;
  forceStalePreview.value = false;
  clearError();
  resetInput();
  focusFileInput();
}

function validateFile(file: File): string | null {
  if (!policy.value) {
    return 'Import rules are still loading. Try again in a moment.';
  }

  if (!file.name.toLowerCase().endsWith('.csv')) {
    return 'Select a CSV file with a .csv extension.';
  }

  if (file.size === 0) {
    return 'The selected CSV is empty. Choose a file with a header and at least one data row.';
  }

  if (file.size > policy.value.maxFileBytes) {
    return `The selected file is ${formatBytes(file.size)}. The maximum is ${formatBytes(policy.value.maxFileBytes)}.`;
  }

  return null;
}

async function requestPreview(file: File): Promise<void> {
  selectedFile.value = file;
  preview.value = null;
  result.value = null;
  forceStalePreview.value = false;

  const validationMessage = validateFile(file);
  if (validationMessage) {
    setError(new ImportsApiError(validationMessage), validationMessage, 'preview');
    return;
  }

  clearError();
  loadingPreview.value = true;

  try {
    preview.value = await api.value.preview(file);
  } catch (value) {
    setError(value, 'The CSV could not be previewed. Check the file and try again.', 'preview');
  } finally {
    loadingPreview.value = false;
  }
}

async function onFileSelected(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  resetInput();

  if (file) {
    await requestPreview(file);
  }
}

async function confirmImport(): Promise<void> {
  if (!preview.value || previewIsStale.value || preview.value.acceptedRows === 0) {
    return;
  }

  confirming.value = true;
  clearError();
  forceStalePreview.value = false;

  try {
    result.value = await api.value.confirm(preview.value.previewId);
    preview.value = null;
    selectedFile.value = null;
    resetInput();
  } catch (value) {
    const apiError = asApiError(value, 'The import could not be confirmed. Try again.');
    if (apiError.status === 409 || apiError.status === 410) {
      forceStalePreview.value = true;
    }
    setError(apiError, 'The import could not be confirmed. Try again.', 'confirm');
  } finally {
    confirming.value = false;
  }
}

async function retry(): Promise<void> {
  if (errorStage.value === 'policy' || errorStage.value === 'template') {
    if (errorStage.value === 'template') {
      await downloadTemplate();
    } else {
      await loadPolicy();
    }
    return;
  }

  if (errorStage.value === 'preview' && selectedFile.value) {
    await requestPreview(selectedFile.value);
    return;
  }

  if (errorStage.value === 'confirm') {
    if (previewIsStale.value) {
      chooseAnotherFile();
      return;
    }
    await confirmImport();
  }
}

function errorTitle(): string {
  if (errorStage.value === 'policy') return 'Import rules unavailable';
  if (errorStage.value === 'template') return 'Template download failed';
  if (errorStage.value === 'confirm') return 'Import confirmation failed';
  return 'Preview unavailable';
}

function errorDescription(): string {
  return error.value?.message ?? 'Something went wrong while contacting the import service.';
}

function errorCorrelation(): string | null {
  return error.value?.correlationId ?? null;
}

onMounted(async () => {
  clockTimer = window.setInterval(() => {
    clock.value = Date.now();
  }, 1000);
  await loadPolicy();
});

onBeforeUnmount(() => {
  if (clockTimer !== undefined) {
    window.clearInterval(clockTimer);
  }
});
</script>

<template>
  <main class="imports-page">
    <header class="page-header">
      <div>
        <p class="eyebrow">Data ingestion</p>
        <h1>Import daily metrics</h1>
        <p class="page-intro">Upload the provided CSV template to validate and add tenant-scoped campaign metrics.</p>
      </div>
      <button
        type="button"
        class="button button-secondary template-button"
        :disabled="loadingPolicy || downloadingTemplate"
        :aria-busy="downloadingTemplate"
        @click="downloadTemplate"
      >
        {{ downloadingTemplate ? 'Preparing template…' : 'Download CSV template' }}
      </button>
    </header>

    <section v-if="loadingPolicy" class="panel loading-panel" aria-busy="true" aria-live="polite">
      <div class="skeleton skeleton-title"></div>
      <div class="skeleton skeleton-line"></div>
      <div class="skeleton skeleton-line short"></div>
      <p class="loading-copy">Loading the current import rules…</p>
    </section>

    <section v-else-if="errorStage === 'policy'" class="panel state-panel" role="alert">
      <p class="state-kicker">Unable to start</p>
      <h2>{{ errorTitle() }}</h2>
      <p>{{ errorDescription() }}</p>
      <p v-if="errorCorrelation()" class="correlation">Correlation ID: <code>{{ errorCorrelation() }}</code></p>
      <button type="button" class="button button-primary" @click="retry">Retry loading import rules</button>
    </section>

    <template v-else-if="policy">
      <ImportStepIndicator :current="currentStep" />

      <section class="policy-panel" aria-labelledby="import-rules-heading">
        <div>
          <p class="eyebrow">Before you upload</p>
          <h2 id="import-rules-heading">Use the expected CSV format</h2>
        </div>
        <dl class="rules-grid">
          <div>
            <dt>File limit</dt>
            <dd>{{ formatBytes(policy.maxFileBytes) }} maximum</dd>
          </div>
          <div>
            <dt>Row limit</dt>
            <dd>{{ policy.maxRows.toLocaleString() }} data rows maximum</dd>
          </div>
          <div>
            <dt>Date semantics</dt>
            <dd>{{ policy.dateSemantics }}</dd>
          </div>
          <div>
            <dt>Currency semantics</dt>
            <dd>{{ policy.currencySemantics }}</dd>
          </div>
        </dl>
        <p class="required-columns">
          <strong>Expected columns:</strong>
          <span v-if="policy.requiredColumns.length">{{ policy.requiredColumns.join(', ') }}</span>
          <span v-else>See the downloaded template.</span>
          The workflow validates the expected columns; it does not remap them.
        </p>
      </section>

      <section v-if="errorStage && errorStage !== 'policy'" class="error-banner" role="alert">
        <div>
          <strong>{{ errorTitle() }}</strong>
          <p>{{ errorDescription() }}</p>
          <p v-if="errorCorrelation()" class="correlation">Correlation ID: <code>{{ errorCorrelation() }}</code></p>
        </div>
        <button type="button" class="button button-secondary" @click="retry">
          {{ errorStage === 'confirm' && previewIsStale ? 'Choose another file' : 'Retry' }}
        </button>
      </section>

      <section v-if="loadingPreview" class="panel loading-panel preview-loading" aria-busy="true" aria-live="polite">
        <div class="spinner" aria-hidden="true"></div>
        <div>
          <h2>Validating {{ selectedFile ? formatFileName(selectedFile) : 'your CSV' }}</h2>
          <p class="loading-copy">The server is checking the CSV structure and row values. No data is written during preview.</p>
        </div>
      </section>

      <ImportPreviewPanel
        v-else-if="preview"
        :preview="preview"
        :busy="confirming"
        :stale="previewIsStale"
        @confirm="confirmImport"
        @choose-file="chooseAnotherFile"
      />

      <ImportResultPanel v-else-if="result" :result="result" @start-over="chooseAnotherFile" />

      <section v-else class="panel select-panel" aria-labelledby="select-file-heading">
        <div class="select-copy">
          <p class="eyebrow">Step 1</p>
          <h2 id="select-file-heading">Select a CSV to preview</h2>
          <p>No file is uploaded until you choose one. Preview validates the file before the confirm step writes any rows.</p>
        </div>
        <div class="file-picker">
          <label for="csv-file">CSV file</label>
          <input id="csv-file" ref="fileInput" type="file" accept=".csv,text/csv" :disabled="isBusy" @change="onFileSelected">
          <p class="input-hint">CSV only, up to {{ formatBytes(policy.maxFileBytes) }} and {{ policy.maxRows.toLocaleString() }} data rows.</p>
        </div>
        <p class="empty-note">No CSV selected yet.</p>
      </section>
    </template>
  </main>
</template>

<style scoped>
.imports-page {
  max-width: 1120px;
  margin: 0 auto;
  padding: 32px 24px 48px;
  color: var(--text-primary, #182230);
  font: 14px/1.45 Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}

.page-header,
.policy-panel,
.error-banner,
.select-panel,
.loading-panel {
  display: flex;
  gap: 24px;
  align-items: flex-start;
  justify-content: space-between;
}

.page-header {
  margin-bottom: 24px;
}

.eyebrow,
.state-kicker {
  margin: 0 0 4px;
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

h1,
h2,
p { margin-top: 0; }

h1 {
  margin-bottom: 8px;
  font-size: 28px;
  letter-spacing: -0.02em;
}

h2 {
  margin-bottom: 8px;
  font-size: 18px;
}

.page-intro {
  max-width: 680px;
  margin-bottom: 0;
  color: var(--text-secondary, #667085);
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

.button:focus-visible,
input:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.button:disabled,
input:disabled {
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

.template-button { flex: 0 0 auto; }

.panel,
.policy-panel {
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
}

.policy-panel {
  align-items: stretch;
  margin: 24px 0;
  padding: 20px 24px;
}

.policy-panel > div:first-child {
  min-width: 190px;
}

.rules-grid {
  display: grid;
  flex: 1;
  grid-template-columns: repeat(2, minmax(150px, 1fr));
  gap: 12px 24px;
  margin: 0;
}

dt {
  color: var(--text-secondary, #667085);
  font-size: 12px;
}

dd {
  margin: 3px 0 0;
  color: var(--text-primary, #182230);
  font-size: 13px;
  font-weight: 600;
}

.required-columns {
  flex: 1 1 100%;
  margin: 0;
  padding-top: 16px;
  border-top: 1px solid var(--border, #e4e7ec);
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.required-columns strong { color: var(--text-primary, #182230); }

.error-banner {
  align-items: center;
  margin: 24px 0;
  padding: 14px 16px;
  border: 1px solid #fecdca;
  border-left: 3px solid var(--danger, #b42318);
  border-radius: 6px;
  background: #fef3f2;
}

.error-banner p { margin: 4px 0 0; color: var(--text-secondary, #667085); }
.error-banner .button { flex: 0 0 auto; }

.correlation {
  font-size: 12px;
}

code {
  color: var(--text-primary, #182230);
  font-size: 12px;
}

.select-panel,
.loading-panel {
  padding: 24px;
}

.select-copy {
  max-width: 590px;
}

.select-copy p:last-child,
.loading-copy {
  margin-bottom: 0;
  color: var(--text-secondary, #667085);
}

.file-picker {
  display: grid;
  min-width: min(360px, 100%);
  gap: 8px;
}

.file-picker label {
  color: var(--text-primary, #182230);
  font-weight: 650;
}

input[type='file'] {
  width: 100%;
  min-height: 44px;
  padding: 8px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
  font: inherit;
}

.input-hint,
.empty-note {
  margin: 0;
  color: var(--text-secondary, #667085);
  font-size: 12px;
}

.empty-note {
  flex-basis: 100%;
  margin-top: -8px;
}

.loading-panel {
  position: relative;
  overflow: hidden;
  min-height: 150px;
}

.loading-panel:not(.preview-loading) {
  display: block;
}

.skeleton {
  height: 14px;
  margin-bottom: 12px;
  border-radius: 4px;
  background: var(--surface-subtle, #f8fafc);
}

.skeleton-title { width: 35%; height: 22px; }
.skeleton-line { width: 75%; }
.skeleton-line.short { width: 45%; }

.preview-loading {
  align-items: center;
}

.spinner {
  width: 24px;
  height: 24px;
  flex: 0 0 auto;
  border: 3px solid var(--border, #e4e7ec);
  border-top-color: var(--accent, #176b64);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

.state-panel {
  padding: 24px;
}

.state-panel p:not(.state-kicker) {
  max-width: 640px;
  color: var(--text-secondary, #667085);
}

.state-panel .button { margin-top: 8px; }

@keyframes spin {
  to { transform: rotate(360deg); }
}

@media (prefers-reduced-motion: reduce) {
  .spinner { animation: none; }
}

@media (max-width: 760px) {
  .imports-page { padding: 24px 16px 40px; }
  .page-header,
  .policy-panel,
  .select-panel,
  .loading-panel { flex-direction: column; }
  .template-button { width: 100%; }
  .policy-panel { padding: 16px; }
  .policy-panel > div:first-child { min-width: 0; }
  .rules-grid { width: 100%; }
  .file-picker { width: 100%; min-width: 0; }
  .error-banner { align-items: stretch; flex-direction: column; }
  .error-banner .button { width: 100%; }
}

@media (max-width: 460px) {
  h1 { font-size: 24px; }
  .rules-grid { grid-template-columns: 1fr; }
  .select-panel,
  .loading-panel { padding: 16px; }
}
</style>
