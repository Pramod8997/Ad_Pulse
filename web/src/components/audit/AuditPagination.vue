<script setup lang="ts">
const props = withDefaults(defineProps<{
  page: number
  hasMore: boolean
  hasPrevious: boolean
  disabled?: boolean
}>(), {
  disabled: false,
})

const emit = defineEmits<{
  pageChange: [page: number]
}>()

function goTo(page: number) {
  if (!props.disabled && page >= 1 && page !== props.page) {
    emit('pageChange', page)
  }
}
</script>

<template>
  <nav class="pagination" aria-label="Audit log pages">
    <p>
      <span>Page {{ page }}</span>
    </p>
    <div class="pagination-actions">
      <button
        type="button"
        :disabled="disabled || !hasPrevious"
        aria-label="Go to previous audit page"
        @click="goTo(page - 1)"
      >
        Previous
      </button>
      <button
        type="button"
        :disabled="disabled || !hasMore"
        aria-label="Go to next audit page"
        @click="goTo(page + 1)"
      >
        Next
      </button>
    </div>
  </nav>
</template>

<style scoped>
.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  color: var(--text-secondary);
  font-size: 13px;
}

.pagination p {
  margin: 0;
}

.pagination-actions {
  display: flex;
  gap: 8px;
}

button {
  min-height: 40px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--surface);
  color: var(--text-primary);
  cursor: pointer;
  font: inherit;
  font-weight: 600;
  padding: 8px 12px;
}

button:hover:not(:disabled) {
  background: var(--surface-subtle);
}

button:focus-visible {
  outline: 3px solid var(--focus);
  outline-offset: 2px;
}

button:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}

@media (max-width: 520px) {
  .pagination {
    align-items: stretch;
    flex-direction: column;
  }

  .pagination-actions {
    width: 100%;
  }

  .pagination-actions button {
    flex: 1;
  }
}
</style>
