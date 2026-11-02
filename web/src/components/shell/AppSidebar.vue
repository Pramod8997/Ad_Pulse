<script setup lang="ts">
import { computed } from 'vue'
import { ExternalLink } from 'lucide-vue-next'
import { RouterLink, useRoute } from 'vue-router'
import AppProfileMenu from './AppProfileMenu.vue'
import AppWorkspaceSwitcher from './AppWorkspaceSwitcher.vue'
import {
  isNavigationItemActive,
  primaryNavigation,
  utilityNavigation,
  type NavigationItem,
  type Profile,
  type Workspace,
} from './navigation'

const props = withDefaults(
  defineProps<{
    workspaces: readonly Workspace[]
    selectedWorkspace: Workspace
    profile: Profile
    documentationHref?: string
    mobile?: boolean
  }>(),
  { mobile: false },
)

const emit = defineEmits<{
  'update:selectedWorkspace': [workspace: Workspace]
  'sign-out': []
  navigate: []
}>()

const route = useRoute()
const navigationPath = computed(() => route.path)

const isActive = (item: NavigationItem) => isNavigationItemActive(item, navigationPath.value)

const onNavigate = () => {
  if (props.mobile) emit('navigate')
}
</script>

<template>
  <aside class="app-sidebar" :class="{ 'app-sidebar--mobile': mobile }" aria-label="Workspace navigation">
    <div class="app-sidebar__top">
      <RouterLink class="app-sidebar__wordmark" to="/app" aria-label="AdPulse overview" @click="onNavigate">AdPulse</RouterLink>
      <AppWorkspaceSwitcher
        :workspaces="workspaces"
        :selected-workspace="selectedWorkspace"
        @update:selected-workspace="emit('update:selectedWorkspace', $event)"
      />
    </div>

    <nav class="app-sidebar__nav" aria-label="Primary navigation">
      <p class="app-sidebar__label">Workspace</p>
      <RouterLink
        v-for="item in primaryNavigation"
        :key="item.to"
        class="app-sidebar__link"
        :class="{ 'is-active': isActive(item) }"
        :to="item.to"
        :aria-current="isActive(item) ? 'page' : undefined"
        @click="onNavigate"
      >
        <component :is="item.icon" :size="18" :stroke-width="1.8" aria-hidden="true" />
        <span>{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="app-sidebar__bottom">
      <nav class="app-sidebar__nav" aria-label="Workspace settings">
        <RouterLink
          v-for="item in utilityNavigation"
          :key="item.to"
          class="app-sidebar__link"
          :class="{ 'is-active': isActive(item) }"
          :to="item.to"
          :aria-current="isActive(item) ? 'page' : undefined"
          @click="onNavigate"
        >
          <component :is="item.icon" :size="18" :stroke-width="1.8" aria-hidden="true" />
          <span>{{ item.label }}</span>
        </RouterLink>
        <a v-if="documentationHref" class="app-sidebar__link" :href="documentationHref" @click="onNavigate">
          <ExternalLink :size="18" :stroke-width="1.8" aria-hidden="true" />
          <span>Documentation</span>
        </a>
      </nav>
      <AppProfileMenu :profile="profile" @sign-out="emit('sign-out')" />
    </div>
  </aside>
</template>

<style scoped>
.app-sidebar {
  width: 248px;
  height: 100vh;
  position: fixed;
  z-index: 20;
  inset: 0 auto 0 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--border, #e4e7ec);
  background: var(--surface, #fff);
  color: var(--text-primary, #182230);
}

.app-sidebar--mobile {
  width: min(320px, calc(100vw - 48px));
  position: relative;
  inset: auto;
  box-shadow: 8px 0 28px rgb(24 34 48 / 12%);
}

.app-sidebar__top {
  flex: 0 0 auto;
}

.app-sidebar__wordmark {
  min-height: 64px;
  padding: 0 24px;
  display: flex;
  align-items: center;
  border-bottom: 1px solid var(--border, #e4e7ec);
  color: var(--text-primary, #182230);
  font-size: 20px;
  font-weight: 750;
  letter-spacing: -0.02em;
  text-decoration: none;
}

.app-sidebar__wordmark:focus-visible,
.app-sidebar__link:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: -3px;
}

.app-sidebar__nav {
  padding: 22px 12px 0;
}

.app-sidebar__label {
  margin: 0 12px 8px;
  color: var(--text-secondary, #667085);
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}

.app-sidebar__link {
  min-height: 42px;
  position: relative;
  padding: 9px 12px;
  display: flex;
  align-items: center;
  gap: 11px;
  border-radius: 6px;
  color: var(--text-secondary, #667085);
  font-size: 13px;
  font-weight: 600;
  text-decoration: none;
  transition: background-color 160ms ease, color 160ms ease;
}

.app-sidebar__link:hover {
  background: var(--surface-subtle, #f8fafc);
  color: var(--text-primary, #182230);
}

.app-sidebar__link.is-active {
  background: color-mix(in srgb, var(--accent, #176b64) 8%, var(--surface, #fff));
  color: var(--accent, #176b64);
}

.app-sidebar__link.is-active::before {
  width: 2px;
  position: absolute;
  top: 6px;
  bottom: 6px;
  left: 0;
  border-radius: 2px;
  background: var(--accent, #176b64);
  content: '';
}

.app-sidebar__bottom {
  margin-top: auto;
}

.app-sidebar__bottom > .app-sidebar__nav {
  padding-top: 12px;
}

@media (max-width: 767px) {
  .app-sidebar:not(.app-sidebar--mobile) {
    display: none;
  }
}
</style>
