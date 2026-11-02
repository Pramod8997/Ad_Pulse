import type { Component } from 'vue'
import {
  Bell,
  ChartNoAxesCombined,
  FileClock,
  FileInput,
  Settings,
} from 'lucide-vue-next'

export interface NavigationItem {
  label: string
  to: string
  icon: Component
}

export interface Workspace {
  id: string
  name: string
  role?: string
}

export interface Profile {
  name: string
  email: string
  role?: string
  initials?: string
}

export interface Breadcrumb {
  label: string
  to?: string
}

export type SignOut = () => void | Promise<void>

export const primaryNavigation: readonly NavigationItem[] = [
  { label: 'Overview', to: '/app', icon: ChartNoAxesCombined },
  { label: 'Campaigns', to: '/app/campaigns', icon: ChartNoAxesCombined },
  { label: 'Alerts', to: '/app/alerts', icon: Bell },
  { label: 'Imports', to: '/app/imports', icon: FileInput },
  { label: 'Audit log', to: '/app/audit', icon: FileClock },
]

export const utilityNavigation: readonly NavigationItem[] = [
  { label: 'Settings', to: '/app/settings', icon: Settings },
]

export const isNavigationItemActive = (item: NavigationItem, path: string) =>
  item.to === '/app'
    ? path === item.to
    : path === item.to || path.startsWith(`${item.to}/`)

export const defaultBreadcrumbs = (path: string): Breadcrumb[] => {
  const item = [...primaryNavigation, ...utilityNavigation].find((candidate) =>
    isNavigationItemActive(candidate, path),
  )

  return item ? [{ label: item.label }] : [{ label: 'Workspace' }]
}
