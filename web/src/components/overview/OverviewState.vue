<script setup lang="ts">
const props = withDefaults(defineProps<{
  kind: 'loading' | 'empty' | 'error'
  title?: string
  message?: string
  correlationId?: string | null
  retry?: (() => void | Promise<unknown>) | null
}>(), {
  title: undefined,
  message: undefined,
  correlationId: null,
  retry: null,
})

const emit = defineEmits<{
  retry: []
}>()

async function onRetry() {
  if (props.retry) await props.retry()
  emit('retry')
}
</script>

<template>
  <div v-if="kind === 'loading'" class="overview-state overview-state--loading" aria-label="Loading overview" aria-live="polite">
    <span class="overview-skeleton overview-skeleton--title" />
    <span class="overview-skeleton overview-skeleton--line" />
    <span class="overview-skeleton overview-skeleton--line overview-skeleton--short" />
  </div>

  <section v-else class="overview-state" :class="`overview-state--${kind}`" :aria-labelledby="`overview-state-${kind}`">
    <h2 :id="`overview-state-${kind}`">{{ title }}</h2>
    <p>{{ message }}</p>
    <p v-if="correlationId" class="overview-state__correlation">Reference: {{ correlationId }}</p>
    <button v-if="kind === 'error' && (retry || $attrs.onRetry)" type="button" class="overview-button overview-button--secondary" @click="onRetry">
      Try again
    </button>
  </section>
</template>

<style scoped>
.overview-state {
  display: grid;
  gap: 8px;
  min-height: 156px;
  align-content: center;
  justify-items: start;
  padding: 24px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
  color: var(--text-secondary, #667085);
}

.overview-state h2 {
  margin: 0;
  color: var(--text-primary, #182230);
  font-size: 16px;
  font-weight: 600;
}

.overview-state p { margin: 0; max-width: 60ch; }
.overview-state__correlation { font-size: 12px; }
.overview-state--error { border-color: color-mix(in srgb, var(--danger, #b42318) 30%, var(--border, #e4e7ec)); }
.overview-state--error h2 { color: var(--danger, #b42318); }
.overview-state--loading { min-height: 112px; }
.overview-skeleton { display: block; height: 14px; border-radius: 4px; background: var(--surface-subtle, #f8fafc); }
.overview-skeleton--title { width: 42%; height: 18px; }
.overview-skeleton--line { width: 80%; }
.overview-skeleton--short { width: 55%; }
.overview-button { min-height: 40px; padding: 8px 14px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); font: inherit; cursor: pointer; }
.overview-button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 45%, transparent); outline-offset: 2px; }
</style>
