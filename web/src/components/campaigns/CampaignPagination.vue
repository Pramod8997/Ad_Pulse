<script setup lang="ts">
const props = defineProps<{ page: number; pageSize: number; totalCount: number | null; hasNext: boolean; disabled?: boolean }>();
const emit = defineEmits<{ previous: []; next: [] }>();
function firstItem() { return props.page === 1 && props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1; }
function lastItem() { return props.totalCount == null ? props.page * props.pageSize : Math.min(props.page * props.pageSize, props.totalCount); }
</script>

<template>
  <nav class="pagination" aria-label="Campaign pages"><span class="pagination__summary">{{ firstItem() }}–{{ lastItem() }}<template v-if="totalCount != null"> of {{ totalCount }}</template><template v-else> results</template></span><div class="pagination__actions"><button type="button" :disabled="disabled || page <= 1" @click="emit('previous')">Previous</button><span aria-live="polite">Page {{ page }}</span><button type="button" :disabled="disabled || !hasNext" @click="emit('next')">Next</button></div></nav>
</template>

<style scoped>
.pagination { display: flex; align-items: center; justify-content: space-between; gap: 16px; padding: 14px 0 2px; color: var(--text-secondary, #667085); font-size: 13px; }
.pagination__actions { display: flex; align-items: center; gap: 12px; }
button { min-height: 36px; padding: 6px 12px; border: 1px solid var(--border, #e4e7ec); border-radius: 6px; background: var(--surface, #fff); color: var(--text-primary, #182230); cursor: pointer; font: inherit; }
button:focus-visible { outline: 3px solid color-mix(in srgb, var(--focus, #2e90fa) 35%, transparent); outline-offset: 1px; }
button:disabled { cursor: not-allowed; opacity: .5; }
@media (max-width: 480px) { .pagination { align-items: flex-start; flex-direction: column; } }
</style>
