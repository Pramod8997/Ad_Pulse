<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { Search, X } from 'lucide-vue-next'
import AppIconButton from './AppIconButton.vue'

const props = withDefaults(
  defineProps<{
    open: boolean
    placeholder?: string
  }>(),
  { placeholder: 'Search campaigns, alerts, and reports' },
)

const emit = defineEmits<{
  'update:open': [open: boolean]
  search: [query: string]
}>()

const panel = ref<HTMLElement | null>(null)
const input = ref<HTMLInputElement | null>(null)
const query = ref('')
const previousFocus = ref<HTMLElement | null>(null)

const close = () => emit('update:open', false)

const submit = () => {
  const value = query.value.trim()
  if (value) emit('search', value)
}

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape') {
    close()
    return
  }

  if (event.key !== 'Tab' || !panel.value) return
  const focusable = [...panel.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), input:not([disabled]), [href], select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
  )]
  if (!focusable.length) return

  const first = focusable[0]
  const last = focusable[focusable.length - 1]
  if (event.shiftKey && document.activeElement === first) {
    event.preventDefault()
    last.focus()
  } else if (!event.shiftKey && document.activeElement === last) {
    event.preventDefault()
    first.focus()
  }
}

watch(
  () => props.open,
  async (open) => {
    if (open) {
      previousFocus.value = document.activeElement instanceof HTMLElement ? document.activeElement : null
      await nextTick()
      input.value?.focus()
    } else {
      query.value = ''
      previousFocus.value?.focus()
      previousFocus.value = null
    }
  },
)

watch(
  () => props.open,
  (open) => {
    if (open) document.addEventListener('keydown', onKeydown)
    else document.removeEventListener('keydown', onKeydown)
  },
  { immediate: true },
)

onBeforeUnmount(() => document.removeEventListener('keydown', onKeydown))
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="search-command" @click.self="close">
      <section
        ref="panel"
        class="search-command__panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="search-command-title"
      >
        <div class="search-command__heading">
          <div>
            <p id="search-command-title" class="search-command__title">Search workspace</p>
            <p class="search-command__hint">Search is scoped to the current workspace.</p>
          </div>
          <AppIconButton label="Close search" :icon="X" @click="close" />
        </div>
        <form class="search-command__form" @submit.prevent="submit">
          <Search :size="19" :stroke-width="1.8" aria-hidden="true" />
          <input
            ref="input"
            v-model="query"
            type="search"
            :placeholder="placeholder"
            aria-label="Search workspace"
            autocomplete="off"
          />
          <kbd>Enter</kbd>
        </form>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.search-command {
  position: fixed;
  z-index: 70;
  inset: 0;
  display: flex;
  align-items: flex-start;
  justify-content: center;
  padding: 96px 16px 24px;
  background: rgb(24 34 48 / 42%);
}

.search-command__panel {
  width: min(640px, 100%);
  overflow: hidden;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 10px;
  background: var(--surface, #fff);
  box-shadow: 0 20px 50px rgb(24 34 48 / 18%);
}

.search-command__heading {
  min-height: 72px;
  padding: 16px 16px 12px 20px;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  border-bottom: 1px solid var(--border, #e4e7ec);
}

.search-command__title,
.search-command__hint {
  margin: 0;
}

.search-command__title {
  color: var(--text-primary, #182230);
  font-size: 15px;
  font-weight: 700;
}

.search-command__hint {
  margin-top: 4px;
  color: var(--text-secondary, #667085);
  font-size: 12px;
}

.search-command__form {
  min-height: 64px;
  padding: 12px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--text-secondary, #667085);
}

.search-command__form input {
  min-width: 0;
  flex: 1;
  border: 0;
  outline: 0;
  background: transparent;
  color: var(--text-primary, #182230);
  font: inherit;
  font-size: 16px;
}

.search-command__form input::placeholder {
  color: var(--text-secondary, #667085);
}

kbd {
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 4px;
  padding: 3px 6px;
  color: var(--text-secondary, #667085);
  font-family: inherit;
  font-size: 11px;
  white-space: nowrap;
}
</style>
