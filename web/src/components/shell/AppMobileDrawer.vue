<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { X } from 'lucide-vue-next'
import AppIconButton from '../ui/AppIconButton.vue'
import AppSidebar from './AppSidebar.vue'
import type { Profile, Workspace } from './navigation'

const props = defineProps<{
  open: boolean
  workspaces: readonly Workspace[]
  selectedWorkspace: Workspace
  profile: Profile
  documentationHref?: string
}>()

const emit = defineEmits<{
  'update:open': [open: boolean]
  'update:selectedWorkspace': [workspace: Workspace]
  'sign-out': []
}>()

const closeButton = ref<InstanceType<typeof AppIconButton> | null>(null)
const panel = ref<HTMLElement | null>(null)
const previousFocus = ref<HTMLElement | null>(null)
const close = () => emit('update:open', false)

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && props.open) {
    close()
    return
  }

  if (event.key !== 'Tab' || !panel.value) return
  const focusable = [...panel.value.querySelectorAll<HTMLElement>(
    'button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])',
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
      document.body.classList.add('app-drawer-open')
      await nextTick()
      closeButton.value?.focus()
    } else {
      document.body.classList.remove('app-drawer-open')
      previousFocus.value?.focus()
      previousFocus.value = null
    }
  },
  { immediate: true },
)

watch(
  () => props.open,
  (open) => {
    if (open) document.addEventListener('keydown', onKeydown)
    else document.removeEventListener('keydown', onKeydown)
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  document.body.classList.remove('app-drawer-open')
  document.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="mobile-drawer" @click.self="close">
      <div ref="panel" class="mobile-drawer__panel" role="dialog" aria-modal="true" aria-label="Mobile workspace navigation">
        <div class="mobile-drawer__header">
          <span class="mobile-drawer__title">Navigation</span>
          <AppIconButton ref="closeButton" label="Close navigation" :icon="X" @click="close" />
        </div>
        <AppSidebar
          mobile
          :workspaces="workspaces"
          :selected-workspace="selectedWorkspace"
          :profile="profile"
          :documentation-href="documentationHref"
          @update:selected-workspace="emit('update:selectedWorkspace', $event)"
          @sign-out="emit('sign-out')"
          @navigate="close"
        />
      </div>
    </div>
  </Teleport>
</template>

<style>
body.app-drawer-open {
  overflow: hidden;
}
</style>

<style scoped>
.mobile-drawer {
  position: fixed;
  z-index: 60;
  inset: 0;
  display: flex;
  background: rgb(24 34 48 / 42%);
}

.mobile-drawer__panel {
  width: min(320px, calc(100vw - 48px));
  height: 100%;
  overflow-y: auto;
  background: var(--surface, #fff);
}

.mobile-drawer__header {
  min-height: 64px;
  padding: 0 12px 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid var(--border, #e4e7ec);
}

.mobile-drawer__title {
  color: var(--text-primary, #182230);
  font-size: 16px;
  font-weight: 700;
}
</style>
