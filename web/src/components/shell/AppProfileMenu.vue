<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { ChevronUp, LogOut } from 'lucide-vue-next'
import type { Profile } from './navigation'
import AppAvatar from '../ui/AppAvatar.vue'

defineProps<{
  profile: Profile
}>()

const emit = defineEmits<{
  'sign-out': []
}>()

const root = ref<HTMLElement | null>(null)
const trigger = ref<HTMLButtonElement | null>(null)
const open = ref(false)

const close = () => {
  open.value = false
  trigger.value?.focus()
}

const onDocumentClick = (event: MouseEvent) => {
  if (root.value && !root.value.contains(event.target as Node)) open.value = false
}

const onKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && open.value) close()
}

const signOut = () => {
  open.value = false
  emit('sign-out')
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
  <div ref="root" class="profile-menu">
    <button
      ref="trigger"
      class="profile-menu__trigger"
      type="button"
      :aria-expanded="open"
      aria-haspopup="menu"
      aria-label="Open profile menu"
      @click="open = !open"
    >
      <AppAvatar :name="profile.name" :initials="profile.initials" size="sm" />
      <span class="profile-menu__identity">
        <span class="profile-menu__name">{{ profile.name }}</span>
        <span class="profile-menu__email">{{ profile.email }}</span>
      </span>
      <ChevronUp class="profile-menu__chevron" :class="{ 'is-open': open }" :size="16" :stroke-width="1.8" aria-hidden="true" />
    </button>

    <div v-if="open" class="profile-menu__panel" role="menu" aria-label="Profile actions">
      <div class="profile-menu__summary">
        <AppAvatar :name="profile.name" :initials="profile.initials" />
        <div>
          <p>{{ profile.name }}</p>
          <span>{{ profile.role || 'Workspace member' }}</span>
        </div>
      </div>
      <button class="profile-menu__sign-out" type="button" role="menuitem" @click="signOut">
        <LogOut :size="17" :stroke-width="1.8" aria-hidden="true" />
        <span>Sign out</span>
      </button>
    </div>
  </div>
</template>

<style scoped>
.profile-menu {
  position: relative;
  margin: auto 12px 16px;
}

.profile-menu__trigger {
  width: 100%;
  min-height: 52px;
  padding: 8px;
  display: flex;
  align-items: center;
  gap: 9px;
  border: 1px solid transparent;
  border-radius: 8px;
  background: transparent;
  color: var(--text-primary, #182230);
  text-align: left;
  cursor: pointer;
}

.profile-menu__trigger:hover {
  border-color: var(--border, #e4e7ec);
  background: var(--surface-subtle, #f8fafc);
}

.profile-menu__trigger:focus-visible,
.profile-menu__sign-out:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.profile-menu__identity {
  min-width: 0;
  flex: 1;
  display: flex;
  flex-direction: column;
}

.profile-menu__name,
.profile-menu__email {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.profile-menu__name {
  font-size: 13px;
  font-weight: 700;
}

.profile-menu__email {
  margin-top: 2px;
  color: var(--text-secondary, #667085);
  font-size: 11px;
}

.profile-menu__chevron {
  flex: 0 0 auto;
  color: var(--text-secondary, #667085);
  transform: rotate(180deg);
  transition: transform 160ms ease;
}

.profile-menu__chevron.is-open {
  transform: rotate(0);
}

.profile-menu__panel {
  position: absolute;
  z-index: 30;
  right: 0;
  bottom: calc(100% + 8px);
  width: min(280px, calc(100vw - 32px));
  overflow: hidden;
  padding: 8px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 8px;
  background: var(--surface, #fff);
  box-shadow: 0 12px 28px rgb(24 34 48 / 12%);
}

.profile-menu__summary {
  padding: 8px;
  display: flex;
  align-items: center;
  gap: 10px;
}

.profile-menu__summary p,
.profile-menu__summary span {
  margin: 0;
}

.profile-menu__summary p {
  color: var(--text-primary, #182230);
  font-size: 13px;
  font-weight: 700;
}

.profile-menu__summary span {
  display: block;
  margin-top: 2px;
  color: var(--text-secondary, #667085);
  font-size: 11px;
}

.profile-menu__sign-out {
  width: 100%;
  min-height: 40px;
  padding: 8px;
  display: flex;
  align-items: center;
  gap: 9px;
  border: 0;
  border-radius: 6px;
  background: transparent;
  color: var(--danger, #b42318);
  font: inherit;
  font-size: 13px;
  font-weight: 600;
  text-align: left;
  cursor: pointer;
}

.profile-menu__sign-out:hover {
  background: color-mix(in srgb, var(--danger, #b42318) 8%, var(--surface, #fff));
}
</style>
