import { h, defineComponent } from 'vue'
import type { Pinia } from 'pinia'
import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

import { useAuthStore } from './stores/auth'

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
    component: RouteOutlet,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/campaigns',
    name: 'campaigns',
    component: RouteOutlet,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/campaigns/:id',
    name: 'campaign-detail',
    component: RouteOutlet,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/alerts',
    name: 'alerts',
    component: RouteOutlet,
    meta: { requiresAuth: true },
  },
  {
    path: '/app/imports',
    name: 'imports',
    component: RouteOutlet,
    meta: { requiresAuth: true, requiredPermission: 'import' },
  },
  {
    path: '/app/audit',
    name: 'audit',
    component: RouteOutlet,
    meta: { requiresAuth: true, requiredPermission: 'audit' },
  },
  {
    path: '/app/settings',
    name: 'settings',
    component: RouteOutlet,
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
