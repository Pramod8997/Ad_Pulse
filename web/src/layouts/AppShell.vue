<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import AppMobileDrawer from '../components/shell/AppMobileDrawer.vue'
import AppSearchCommand from '../components/ui/AppSearchCommand.vue'
import AppSidebar from '../components/shell/AppSidebar.vue'
import AppTopBar from '../components/shell/AppTopBar.vue'
import {
  defaultBreadcrumbs,
  type Breadcrumb,
  type Profile,
  type Workspace,
} from '../components/shell/navigation'

const props = withDefaults(
  defineProps<{
    workspaces?: readonly Workspace[]
    selectedWorkspace?: Workspace
    profile?: Profile
    breadcrumbs?: readonly Breadcrumb[]
    documentationHref?: string
  }>(),
  {
    workspaces: () => [{ id: 'current', name: 'Current workspace', role: 'Member' }],
    selectedWorkspace: undefined,
    profile: () => ({ name: 'Account', email: 'Signed-in account', role: 'Workspace member' }),
    breadcrumbs: undefined,
    documentationHref: undefined,
  },
)

const emit = defineEmits<{
  'update:selectedWorkspace': [workspace: Workspace]
  'sign-out': []
  search: [query: string]
}>()

const route = useRoute()
const mobileMenuOpen = ref(false)
const searchOpen = ref(false)
const selectedWorkspace = computed(() => props.selectedWorkspace || props.workspaces[0])
const breadcrumbs = computed(() => props.breadcrumbs?.length ? props.breadcrumbs : defaultBreadcrumbs(route.path))

const onGlobalKeydown = (event: KeyboardEvent) => {
  const isSearchShortcut = (event.metaKey || event.ctrlKey) && event.key.toLowerCase() === 'k'
  if (isSearchShortcut) {
    event.preventDefault()
    searchOpen.value = true
  }
}

const onSearch = (query: string) => {
  searchOpen.value = false
  emit('search', query)
}

onMounted(() => document.addEventListener('keydown', onGlobalKeydown))
onBeforeUnmount(() => document.removeEventListener('keydown', onGlobalKeydown))
</script>

<template>
  <div class="app-shell">
    <AppSidebar
      :workspaces="workspaces"
      :selected-workspace="selectedWorkspace"
      :profile="profile"
      :documentation-href="documentationHref"
      @update:selected-workspace="emit('update:selectedWorkspace', $event)"
      @sign-out="emit('sign-out')"
    />
    <AppTopBar
      :breadcrumbs="breadcrumbs"
      :search-open="searchOpen"
      :mobile-menu-open="mobileMenuOpen"
      @update:search-open="searchOpen = $event"
      @update:mobile-menu-open="mobileMenuOpen = $event"
    />
    <AppMobileDrawer
      :open="mobileMenuOpen"
      :workspaces="workspaces"
      :selected-workspace="selectedWorkspace"
      :profile="profile"
      :documentation-href="documentationHref"
      @update:open="mobileMenuOpen = $event"
      @update:selected-workspace="emit('update:selectedWorkspace', $event)"
      @sign-out="emit('sign-out')"
    />
    <AppSearchCommand
      :open="searchOpen"
      @update:open="searchOpen = $event"
      @search="onSearch"
    />
    <main class="app-shell__main" aria-label="Workspace content">
      <slot />
    </main>
  </div>
</template>

<style>
* {
  box-sizing: border-box;
}

html,
body,
#app {
  min-height: 100%;
  margin: 0;
}

body {
  background: var(--app-bg);
  color: var(--text-primary);
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  font-size: 14px;
  line-height: 20px;
}

button,
input,
select,
textarea {
  font: inherit;
}

.app-shell__main {
  min-height: 100vh;
  margin-left: 248px;
  padding: 88px 24px 32px;
}

@media (max-width: 767px) {
  .app-shell__main {
    margin-left: 0;
    padding: 80px 12px 24px;
  }
}

@media (prefers-reduced-motion: reduce) {
  *,
  *::before,
  *::after {
    scroll-behavior: auto !important;
    transition-duration: 0.01ms !important;
    animation-duration: 0.01ms !important;
  }
}
</style>
