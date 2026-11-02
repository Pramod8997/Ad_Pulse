<script setup lang="ts">
type ImportStep = 'select' | 'preview' | 'result';

const props = defineProps<{
  current: ImportStep;
}>();

const steps: readonly { id: ImportStep; label: string }[] = [
  { id: 'select', label: 'Select file' },
  { id: 'preview', label: 'Review preview' },
  { id: 'result', label: 'Import result' },
];

function stepState(step: ImportStep): 'current' | 'complete' | 'upcoming' {
  const currentIndex = steps.findIndex((item) => item.id === props.current);
  const stepIndex = steps.findIndex((item) => item.id === step);
  if (stepIndex === currentIndex) {
    return 'current';
  }

  return stepIndex < currentIndex ? 'complete' : 'upcoming';
}
</script>

<template>
  <ol class="step-list" aria-label="Import progress">
    <li v-for="step in steps" :key="step.id" class="step" :data-state="stepState(step.id)">
      <span class="step-number" aria-hidden="true">{{ steps.indexOf(step) + 1 }}</span>
      <span>
        <span class="step-label">{{ step.label }}</span>
        <span v-if="stepState(step.id) === 'current'" class="sr-only">Current step</span>
        <span v-else-if="stepState(step.id) === 'complete'" class="sr-only">Complete</span>
      </span>
    </li>
  </ol>
</template>

<style scoped>
.step-list {
  display: flex;
  flex-wrap: wrap;
  gap: 12px 24px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.step {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  color: var(--text-secondary, #667085);
  font-size: 13px;
}

.step[data-state='current'] {
  color: var(--text-primary, #182230);
  font-weight: 600;
}

.step[data-state='complete'] {
  color: var(--success, #16794a);
}

.step-number {
  display: inline-grid;
  width: 24px;
  height: 24px;
  place-items: center;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 50%;
  background: var(--surface, #fff);
  font-variant-numeric: tabular-nums;
  font-size: 12px;
}

.step[data-state='current'] .step-number {
  border-color: var(--accent, #176b64);
  background: var(--accent, #176b64);
  color: #fff;
}

.step[data-state='complete'] .step-number {
  border-color: var(--success, #16794a);
}

.step-label {
  white-space: nowrap;
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
</style>
