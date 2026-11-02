<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { Building2, Check, ChevronDown } from 'lucide-vue-next'
import type { Workspace } from './navigation'

const props = defineProps<{
  workspaces: readonly Workspace[]
  selectedWorkspace: Workspace
}>()

const emit = defineEmits<{
  'update:selectedWorkspace': [workspace: Workspace]
}>()

const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const open = ref(false)

const close = () => {
  open.value = false
  trigger.value?.focus()
}

const selectWorkspace = (workspace: Workspace) => {
  emit('update:selectedWorkspace', workspace)
  close()
}

const moveOption = (event: KeyboardEvent, index: number) => {
  if (!['ArrowDown', 'ArrowUp', 'Home', 'End'].includes(event.key)) return
  const options = root.value?.querySelectorAll<HTMLButtonElement>('[role="option"]')
  if (!options?.length) return
  event.preventDefault()
  const nextIndex = event.key === 'Home'
    ? 0
    : event.key === 'End'
      ? options.length - 1
      : (index + (event.key === 'ArrowDown' ? 1 : -1) + options.length) % options.length
  options[nextIndex]?.focus()
}

const onDocumentClick = (event: MouseEvent) => {
  if (root.value && !root.value.contains(event.target as Node)) open.value = false
}

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && open.value) close()
}

onMounted(() => {
  document.addEventListener('click', onDocumentClick)
  document.addEventListener('keydown', onKeydown)
})

onBeforeUnmount(() => {
  document.removeEventListener('click', onDocumentClick)
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <div ref="root" class="workspace-switcher">
    <button
      ref="trigger"
      class="workspace-switcher__trigger"
      type="button"
      aria-haspopup="listbox"
      :aria-expanded="open"
      aria-label="Switch workspace"
      @click="open = !open"
    >
      <span class="workspace-switcher__icon" aria-hidden="true"><Building2 :size="17" :stroke-width="1.8" /></span>
      <span class="workspace-switcher__copy">
        <span class="workspace-switcher__name">{{ selectedWorkspace.name }}</span>
        <span class="workspace-switcher__role">{{ selectedWorkspace.role || 'Workspace' }}</span>
      </span>
      <ChevronDown class="workspace-switcher__chevron" :class="{ 'is-open': open }" :size="16" :stroke-width="1.8" aria-hidden="true" />
    </button>

    <div v-if="open" class="workspace-switcher__menu" role="listbox" aria-label="Available workspaces">
      <p class="workspace-switcher__menu-label">Workspaces</p>
      <button
        v-for="(workspace, index) in workspaces"
        :key="workspace.id"
        class="workspace-switcher__option"
        :class="{ 'is-selected': workspace.id === selectedWorkspace.id }"
        type="button"
        role="option"
        :aria-selected="workspace.id === selectedWorkspace.id"
        @keydown="moveOption($event, index)"
        @click="selectWorkspace(workspace)"
      >
        <span class="workspace-switcher__option-copy">
          <span>{{ workspace.name }}</span>
          <small>{{ workspace.role || 'Workspace' }}</small>
        </span>
        <Check v-if="workspace.id === selectedWorkspace.id" :size="16" :stroke-width="2" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>

<style scoped>
.workspace-switcher {
  position: relative;
  margin: 20px 12px 0;
}

.workspace-switcher__trigger {
  width: 100%;
  min-height: 60px;
  padding: 10px;
  display: flex;
  align-items: center;
  gap: 10px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-primary, #182230);
  text-align: left;
  cursor: pointer;
}

.workspace-switcher__trigger:hover {
  border-color: color-mix(in srgb, var(--accent, #176b64) 35%, var(--border, #e4e7ec));
}

.workspace-switcher__trigger:focus-visible,
.workspace-switcher__option:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.workspace-switcher__icon {
  width: 30px;
  height: 30px;
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 6px;
  background: var(--surface, #fff);
  color: var(--accent, #176b64);
}

.workspace-switcher__copy,
.workspace-switcher__option-copy {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.workspace-switcher__name,
.workspace-switcher__option-copy span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 13px;
  font-weight: 700;
}

.workspace-switcher__role,
.workspace-switcher__option-copy small {
  margin-top: 2px;
  color: var(--text-secondary, #667085);
  font-size: 11px;
  line-height: 16px;
}

.workspace-switcher__chevron {
  flex: 0 0 auto;
  color: var(--text-secondary, #667085);
  transition: transform 160ms ease;
}

.workspace-switcher__chevron.is-open {
  transform: rotate(180deg);
}

.workspace-switcher__menu {
  position: absolute;
  z-index: 30;
  top: calc(100% + 8px);
  left: 0;
  right: 0;
  overflow: hidden;
  padding: 6px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
  box-shadow: 0 12px 28px rgb(24 34 48 / 12%);
}

.workspace-switcher__menu-label {
  margin: 4px 8px 6px;
  color: var(--text-secondary, #667085);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}

.workspace-switcher__option {
  width: 100%;
  min-height: 46px;
  padding: 8px;
  display: flex;
  align-items: center;
  gap: 8px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--text-primary, #182230);
  text-align: left;
  cursor: pointer;
}

.workspace-switcher__option:hover,
.workspace-switcher__option.is-selected {
  background: color-mix(in srgb, var(--accent, #176b64) 8%, var(--surface, #fff));
}

.workspace-switcher__option.is-selected > svg {
  color: var(--accent, #176b64);
}
</style>
