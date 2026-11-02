import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query'
import { createPinia } from 'pinia'
import { createApp, defineComponent, h } from 'vue'
import { RouterView, useRoute } from 'vue-router'

import { createAppRouter } from './router'
import AppShell from './layouts/AppShell.vue'
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

const AppRoot = defineComponent({
  name: 'AppRoot',
  setup() {
    const route = useRoute()
    return () => route.meta.requiresAuth
      ? h(AppShell, null, { default: () => h(RouterView) })
      : h(RouterView)
  },
})

createApp(AppRoot)
  .use(pinia)
  .use(router)
  .use(VueQueryPlugin, { queryClient })
  .mount('#app')
