<script setup lang="ts">
import { computed } from 'vue';

const props = defineProps<{ status: string }>();
const label = computed(() => props.status ? props.status.replace(/[-_]/g, ' ') : 'Unknown');
const tone = computed(() => {
  const value = props.status.toLowerCase();
  if (['active', 'running', 'live'].includes(value)) return 'positive';
  if (['paused', 'draft', 'scheduled'].includes(value)) return 'attention';
  if (['archived', 'failed', 'cancelled'].includes(value)) return 'muted';
  return 'neutral';
});
</script>

<template>
  <span class="status-badge" :class="`status-badge--${tone}`">{{ label }}</span>
</template>

<style scoped>
.status-badge { display: inline-flex; align-items: center; min-height: 24px; padding: 2px 8px; border: 1px solid var(--border, #e4e7ec); border-radius: 999px; color: var(--text-secondary, #667085); font-size: 12px; font-weight: 600; line-height: 16px; text-transform: capitalize; }
.status-badge--positive { border-color: #a6e3c1; background: #effaf3; color: var(--success, #16794a); }
.status-badge--attention { border-color: #f3d5a4; background: #fff8eb; color: var(--warning, #a15c07); }
.status-badge--muted { background: var(--surface-subtle, #f8fafc); }
.status-badge--neutral { background: #f5f8ff; color: var(--info, #175cd3); }
</style>
