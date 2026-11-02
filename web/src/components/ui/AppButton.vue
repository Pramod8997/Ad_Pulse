<script setup lang="ts">
withDefaults(
  defineProps<{
    type?: 'button' | 'submit' | 'reset'
    variant?: 'primary' | 'secondary' | 'ghost' | 'danger'
    disabled?: boolean
    loading?: boolean
  }>(),
  {
    type: 'button',
    variant: 'secondary',
    disabled: false,
    loading: false,
  },
)
</script>

<template>
  <button
    class="app-button"
    :class="`app-button--${variant}`"
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
  >
    <span v-if="loading" class="app-button__status" aria-hidden="true"></span>
    <span><slot /></span>
  </button>
</template>

<style scoped>
.app-button {
  min-height: 40px;
  border: 1px solid transparent;
  border-radius: 6px;
  padding: 0 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  line-height: 20px;
  cursor: pointer;
  transition: background-color 160ms ease, border-color 160ms ease, color 160ms ease;
}

.app-button:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.app-button:disabled {
  cursor: not-allowed;
  opacity: 0.56;
}

.app-button--primary {
  background: var(--accent, #176b64);
  color: var(--surface, #fff);
}

.app-button--primary:hover:not(:disabled) {
  background: var(--accent-hover, #12564f);
}

.app-button--secondary {
  background: var(--surface, #fff);
  border-color: var(--border, #e4e7ec);
  color: var(--text-primary, #182230);
}

.app-button--secondary:hover:not(:disabled) {
  background: var(--surface-subtle, #f8fafc);
}

.app-button--ghost {
  background: transparent;
  color: var(--text-secondary, #667085);
}

.app-button--ghost:hover:not(:disabled) {
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-primary, #182230);
}

.app-button--danger {
  background: var(--danger, #b42318);
  color: var(--surface, #fff);
}

.app-button--danger:hover:not(:disabled) {
  filter: brightness(0.92);
}

.app-button__status {
  width: 14px;
  height: 14px;
  border: 2px solid currentColor;
  border-right-color: transparent;
  border-radius: 50%;
}

@media (prefers-reduced-motion: no-preference) {
  .app-button__status {
    animation: app-button-spin 700ms linear infinite;
  }
}

@keyframes app-button-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
