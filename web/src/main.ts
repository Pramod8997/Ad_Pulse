import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { createPinia } from 'pinia'
import { computed, createApp, defineComponent, h } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'

import { resetOidcTokenProvider } from './api/token-provider'
import type { Profile, Workspace } from './components/shell/navigation'
import AppShell from './layouts/AppShell.vue'
import { createAppRouter } from './router'
import { useAuthStore } from './stores/auth'
import { useWorkspaceStore } from './stores/workspace'
import './styles/index.css'

const pinia = createPinia()
const router = createAppRouter(pinia)
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      refetchOnWindowFocus: false,
      retry: 1,
      staleTime: 30_000,
    },
  },
})

function computeInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean)
  if (!parts.length) return 'AC'
  const first = parts[0]?.charAt(0) ?? ''
  const second = parts.length > 1 ? (parts[parts.length - 1]?.charAt(0) ?? '') : (parts[0]?.charAt(1) ?? '')
  return `${first}${second}`.toUpperCase() || 'AC'
}

const AppRoot = defineComponent({
  name: 'AppRoot',
  setup() {
    const route = useRoute()
    const appRouter = useRouter()
    const auth = useAuthStore()
    const workspaceStore = useWorkspaceStore()

    const profile = computed<Profile>(() => {
      const identity = auth.identity
      const name = identity?.displayName?.trim() || identity?.subject || 'Account'
      const email = identity?.email?.trim() || (identity?.subject ? `Subject: ${identity.subject}` : 'Signed-in account')
      const role = identity?.roles.length ? identity.roles.join(', ') : 'Workspace member'
      return {
        name,
        email,
        role,
        initials: computeInitials(name),
      }
    })

    const workspaces = computed<Workspace[]>(() => {
      const identity = auth.identity
      if (!identity?.tenantId) {
        return [{ id: 'current', name: 'Current workspace', role: 'Member' }]
      }
      return [{
        id: identity.tenantId,
        name: identity.tenantId,
        role: identity.roles[0] || 'Member',
      }]
    })

    const selectedWorkspace = computed<Workspace | undefined>(() => {
      return workspaces.value.find((item) => item.id === workspaceStore.selectedWorkspaceId) ?? workspaces.value[0]
    })

    function handleSignOut() {
      resetOidcTokenProvider()
      auth.signOut()
      workspaceStore.selectWorkspace(null)
      queryClient.clear()
      void appRouter.push('/sign-in')
    }

    function handleSelectWorkspace(nextWorkspace: Workspace) {
      workspaceStore.selectWorkspace(nextWorkspace.id)
      void queryClient.invalidateQueries()
    }

    function handleSearch(query: string) {
      void appRouter.push({ name: 'campaigns', query: { search: query } })
    }

    return () => route.meta.requiresAuth
      ? h(
          AppShell,
          {
            profile: profile.value,
            workspaces: workspaces.value,
            selectedWorkspace: selectedWorkspace.value,
            'onUpdate:selectedWorkspace': handleSelectWorkspace,
            onSignOut: handleSignOut,
            onSearch: handleSearch,
          },
          { default: () => h(RouterView) },
        )
      : h(RouterView)
  },
})

createApp(AppRoot)
  .use(pinia)
  .use(router)
  .use(VueQueryPlugin, { queryClient })
  .mount('#app')
