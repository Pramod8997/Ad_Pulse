import { h, defineComponent } from 'vue'
import type { Pinia } from 'pinia'
import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { useAuthStore } from './stores/auth'
import AlertsView from './views/AlertsView.vue'
import AuditView from './views/AuditView.vue'
import CampaignDetailView from './views/CampaignDetailView.vue'
import CampaignsView from './views/CampaignsView.vue'
import ImportsView from './views/ImportsView.vue'
import OverviewView from './views/OverviewView.vue'
import SettingsView from './views/SettingsView.vue'

declare module 'vue-router' {
  interface RouteMeta {
    requiresAuth?: boolean
    requiredPermission?: string
    requiredRole?: string
  }
}

const RouteOutlet = defineComponent({
  name: 'RouteOutlet',
  setup() {
    return () => h('main', { id: 'main-content', class: 'route-outlet', tabindex: '-1' })
  },
})

const routes: RouteRecordRaw[] = [
  { path: '/', redirect: { name: 'overview' } },
  { path: '/sign-in', name: 'sign-in', component: RouteOutlet },
  { path: '/forbidden', name: 'forbidden', component: RouteOutlet },
  {
    path: '/app',
    name: 'overview',
    component: OverviewView,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/campaigns',
    name: 'campaigns',
    component: CampaignsView,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/campaigns/:id',
    name: 'campaign-detail',
    component: CampaignDetailView,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/alerts',
    name: 'alerts',
    component: AlertsView,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/imports',
    name: 'imports',
    component: ImportsView,
    meta: { requiresAuth: true, requiredPermission: 'import' },
  },
  {
    path: '/app/audit',
    name: 'audit',
    component: AuditView,
    meta: { requiresAuth: true, requiredPermission: 'audit' },
  },
  {
    path: '/app/settings',
    name: 'settings',
    component: SettingsView,
    meta: { requiresAuth: true, requiredRole: 'tenant-admin' },
  },
]

export function createAppRouter(pinia: Pinia) {
  const router = createRouter({
    history: createWebHistory(),
    routes,
    scrollBehavior: () => ({ top: 0 }),
  })

  router.beforeEach((to) => {
    const auth = useAuthStore(pinia)

    if (to.name === 'sign-in' && auth.isAuthenticated) {
      return { name: 'overview' }
    }

    if (!to.meta.requiresAuth) {
      return true
    }

    if (!auth.isAuthenticated) {
      return { name: 'sign-in', query: { redirect: to.fullPath } }
    }

    if (to.meta.requiredRole && !auth.hasRole(to.meta.requiredRole)) {
      return { name: 'forbidden' }
    }

    if (to.meta.requiredPermission && !auth.hasPermission(to.meta.requiredPermission)) {
      return { name: 'forbidden' }
    }

    return true
  })

  return router
}
