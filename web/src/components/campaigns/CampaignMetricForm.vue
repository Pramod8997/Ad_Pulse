<script setup lang="ts">
import { computed, reactive, watch } from 'vue';
import type { DailyMetricWrite } from './campaignApi';

const props = defineProps<{
  currency: string;
  startsOn?: string;
  endsOn?: string;
  submitting?: boolean;
  serverError?: string | null;
}>();

const emit = defineEmits<{
  submit: [value: { metricDate: string; metric: DailyMetricWrite }];
  cancel: [];
}>();

type MetricField = 'metricDate' | 'spend' | 'revenue' | 'impressions' | 'clicks' | 'conversions';

const DECIMAL_PATTERN = /^\d{1,15}(\.\d{1,4})?$/;
const INTEGER_PATTERN = /^\d+$/;

const form = reactive<Record<MetricField, string>>({
  metricDate: '',
  spend: '',
  revenue: '',
  impressions: '',
  clicks: '',
  conversions: '',
});

const touched = reactive<Record<MetricField, boolean>>({
  metricDate: false,
  spend: false,
  revenue: false,
  impressions: false,
  clicks: false,
  conversions: false,
});

function isValidInteger(raw: string): boolean {
  const trimmed = raw.trim();
  return INTEGER_PATTERN.test(trimmed) && Number.isSafeInteger(Number(trimmed));
}

const errors = computed<Record<MetricField, string>>(() => {
  let metricDateError = '';
  if (!form.metricDate) {
    metricDateError = 'Metric date is required.';
  } else if (
    (props.startsOn && form.metricDate < props.startsOn)
    || (props.endsOn && form.metricDate > props.endsOn)
  ) {
    metricDateError = `Metric date must be between ${props.startsOn ?? 'start'} and ${props.endsOn ?? 'end'}.`;
  }

  const impressionsValid = isValidInteger(form.impressions);
  const clicksValid = isValidInteger(form.clicks);
  const conversionsValid = isValidInteger(form.conversions);

  let clicksError = '';
  if (!clicksValid) {
    clicksError = 'Clicks must be a non-negative whole number.';
  } else if (impressionsValid && Number(form.clicks.trim()) > Number(form.impressions.trim())) {
    clicksError = 'Clicks cannot exceed impressions.';
  }

  let conversionsError = '';
  if (!conversionsValid) {
    conversionsError = 'Conversions must be a non-negative whole number.';
  } else if (clicksValid && Number(form.conversions.trim()) > Number(form.clicks.trim())) {
    conversionsError = 'Conversions cannot exceed clicks.';
  }

  return {
    metricDate: metricDateError,
    spend: !DECIMAL_PATTERN.test(form.spend.trim())
      ? 'Spend must be a non-negative amount with up to four decimal places.'
      : '',
    revenue: !DECIMAL_PATTERN.test(form.revenue.trim())
      ? 'Revenue must be a non-negative amount with up to four decimal places.'
      : '',
    impressions: !impressionsValid ? 'Impressions must be a non-negative whole number.' : '',
    clicks: clicksError,
    conversions: conversionsError,
  };
});

const valid = computed(() => Object.values(errors.value).every((message) => !message));

watch(() => [props.startsOn, props.endsOn], () => {
  (Object.keys(touched) as MetricField[]).forEach((field) => {
    touched[field] = false;
  });
});

function mark(field: MetricField): void {
  touched[field] = true;
}

function fieldError(field: MetricField): string {
  return touched[field] ? errors.value[field] : '';
}

function submit(): void {
  (Object.keys(touched) as MetricField[]).forEach((field) => {
    touched[field] = true;
  });

  if (!valid.value) {
    return;
  }

  emit('submit', {
    metricDate: form.metricDate,
    metric: {
      spend: Number(form.spend.trim()),
      revenue: Number(form.revenue.trim()),
      impressions: Number(form.impressions.trim()),
      clicks: Number(form.clicks.trim()),
      conversions: Number(form.conversions.trim()),
    },
  });
}
</script>

<template>
  <section class="form-panel" aria-labelledby="metric-form-title">
    <div class="form-panel__header">
      <div>
        <h3 id="metric-form-title">Record daily metric</h3>
        <p>Creates or replaces the daily metric for the selected date in {{ currency }}.</p>
      </div>
      <button type="button" class="close-button" aria-label="Close metric form" @click="emit('cancel')">
        Close
      </button>
    </div>

    <form novalidate @submit.prevent="submit">
      <div class="form-grid">
        <div class="field">
          <label for="metric-date">Metric date</label>
          <input
            id="metric-date"
            v-model="form.metricDate"
            type="date"
            :min="startsOn || undefined"
            :max="endsOn || undefined"
            :disabled="submitting"
            :aria-invalid="!!fieldError('metricDate')"
            :aria-describedby="fieldError('metricDate') ? 'metric-date-error' : undefined"
            @blur="mark('metricDate')"
          />
          <span v-if="fieldError('metricDate')" id="metric-date-error" class="field-error">
            {{ fieldError('metricDate') }}
          </span>
        </div>

        <div class="field">
          <label for="metric-spend">Spend ({{ currency }})</label>
          <input
            id="metric-spend"
            v-model="form.spend"
            inputmode="decimal"
            placeholder="0.00"
            :disabled="submitting"
            :aria-invalid="!!fieldError('spend')"
            :aria-describedby="fieldError('spend') ? 'metric-spend-error' : undefined"
            @blur="mark('spend')"
          />
          <span v-if="fieldError('spend')" id="metric-spend-error" class="field-error">
            {{ fieldError('spend') }}
          </span>
        </div>

        <div class="field">
          <label for="metric-revenue">Revenue ({{ currency }})</label>
          <input
            id="metric-revenue"
            v-model="form.revenue"
            inputmode="decimal"
            placeholder="0.00"
            :disabled="submitting"
            :aria-invalid="!!fieldError('revenue')"
            :aria-describedby="fieldError('revenue') ? 'metric-revenue-error' : undefined"
            @blur="mark('revenue')"
          />
          <span v-if="fieldError('revenue')" id="metric-revenue-error" class="field-error">
            {{ fieldError('revenue') }}
          </span>
        </div>

        <div class="field">
          <label for="metric-impressions">Impressions</label>
          <input
            id="metric-impressions"
            v-model="form.impressions"
            inputmode="numeric"
            placeholder="0"
            :disabled="submitting"
            :aria-invalid="!!fieldError('impressions')"
            :aria-describedby="fieldError('impressions') ? 'metric-impressions-error' : undefined"
            @blur="mark('impressions')"
          />
          <span v-if="fieldError('impressions')" id="metric-impressions-error" class="field-error">
            {{ fieldError('impressions') }}
          </span>
        </div>

        <div class="field">
          <label for="metric-clicks">Clicks</label>
          <input
            id="metric-clicks"
            v-model="form.clicks"
            inputmode="numeric"
            placeholder="0"
            :disabled="submitting"
            :aria-invalid="!!fieldError('clicks')"
            :aria-describedby="fieldError('clicks') ? 'metric-clicks-error' : undefined"
            @blur="mark('clicks')"
          />
          <span v-if="fieldError('clicks')" id="metric-clicks-error" class="field-error">
            {{ fieldError('clicks') }}
          </span>
        </div>

        <div class="field">
          <label for="metric-conversions">Conversions</label>
          <input
            id="metric-conversions"
            v-model="form.conversions"
            inputmode="numeric"
            placeholder="0"
            :disabled="submitting"
            :aria-invalid="!!fieldError('conversions')"
            :aria-describedby="fieldError('conversions') ? 'metric-conversions-error' : undefined"
            @blur="mark('conversions')"
          />
          <span v-if="fieldError('conversions')" id="metric-conversions-error" class="field-error">
            {{ fieldError('conversions') }}
          </span>
        </div>
      </div>

      <p v-if="serverError" class="server-error" role="alert">{{ serverError }}</p>

      <div class="form-actions">
        <button type="button" class="button button--quiet" :disabled="submitting" @click="emit('cancel')">
          Cancel
        </button>
        <button type="submit" class="button button--primary" :disabled="submitting">
          {{ submitting ? 'Saving metric…' : 'Save metric' }}
        </button>
      </div>
    </form>
  </section>
</template>

<style scoped>
.form-panel {
  padding: 20px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
}

.form-panel__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 18px;
}

h3 {
  margin: 0;
  color: var(--text-primary, #182230);
  font-size: 16px;
}

.form-panel__header p {
  margin: 4px 0 0;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.close-button {
  min-height: 36px;
  padding: 6px 10px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: transparent;
  color: var(--text-secondary, #667085);
  cursor: pointer;
  font: inherit;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}

.field {
  display: grid;
  gap: 5px;
  align-content: start;
}

label {
  color: var(--text-secondary, #667085);
  font-size: 12px;
  font-weight: 600;
}

input {
  min-height: 40px;
  padding: 8px 10px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
  font: inherit;
}

input:focus-visible,
button:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent);
  outline-offset: 1px;
  border-color: var(--focus, #2e90fa);
}

[aria-invalid='true'] {
  border-color: var(--danger, #b42318);
}

.field-error,
.server-error {
  color: var(--danger, #b42318);
  font-size: 12px;
}

.server-error {
  margin: 16px 0 0;
  padding: 10px 12px;
  border: 1px solid #f2b8b5;
  border-radius: 6px;
  background: #fff6f5;
}

.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}

.button {
  min-height: 40px;
  padding: 8px 14px;
  border-radius: 6px;
  cursor: pointer;
  font: inherit;
  font-weight: 600;
}

.button--quiet {
  border: 1px solid var(--border, #e4e7ec);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
}

.button--primary {
  border: 1px solid var(--accent, #176b64);
  background: var(--accent, #176b64);
  color: #fff;
}

button:disabled,
input:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}

@media (max-width: 720px) {
  .form-grid {
    grid-template-columns: 1fr;
  }

  .form-actions {
    flex-direction: column-reverse;
  }

  .button {
    width: 100%;
  }
}
</style>
