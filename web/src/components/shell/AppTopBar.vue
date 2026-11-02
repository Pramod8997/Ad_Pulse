<script setup lang="ts">
import { computed } from 'vue'
import { ChevronRight, Menu, Search } from 'lucide-vue-next'
import { RouterLink } from 'vue-router'
import type { Breadcrumb } from './navigation'
import AppIconButton from '../ui/AppIconButton.vue'

const props = withDefaults(
  defineProps<{
    breadcrumbs?: readonly Breadcrumb[]
    searchOpen?: boolean
    mobileMenuOpen?: boolean
    searchLabel?: string
  }>(),
  {
    breadcrumbs: () => [],
    searchOpen: false,
    mobileMenuOpen: false,
    searchLabel: 'Search workspace',
  },
)

const emit = defineEmits<{
  'update:searchOpen': [open: boolean]
  'update:mobileMenuOpen': [open: boolean]
}>()

const currentLabel = computed(() => props.breadcrumbs.at(-1)?.label || 'Workspace')
</script>

<template>
  <header class="app-top-bar">
    <div class="app-top-bar__context">
      <AppIconButton
        class="app-top-bar__menu"
        label="Open navigation"
        :icon="Menu"
        :pressed="mobileMenuOpen"
        @click="emit('update:mobileMenuOpen', true)"
      />
      <nav class="app-top-bar__breadcrumbs" aria-label="Breadcrumb">
        <template v-for="(breadcrumb, index) in breadcrumbs" :key="`${breadcrumb.label}-${index}`">
          <ChevronRight v-if="index > 0" :size="15" :stroke-width="1.8" aria-hidden="true" />
          <RouterLink v-if="breadcrumb.to && index < breadcrumbs.length - 1" :to="breadcrumb.to">
            {{ breadcrumb.label }}
          </RouterLink>
          <span v-else :aria-current="index === breadcrumbs.length - 1 ? 'page' : undefined">{{ breadcrumb.label }}</span>
        </template>
        <span v-if="!breadcrumbs.length" aria-current="page">{{ currentLabel }}</span>
      </nav>
    </div>

    <button
      class="app-top-bar__search"
      type="button"
      :aria-expanded="searchOpen"
      :aria-label="searchLabel"
      @click="emit('update:searchOpen', true)"
    >
      <Search :size="17" :stroke-width="1.8" aria-hidden="true" />
      <span>{{ searchLabel }}</span>
      <kbd>Ctrl K</kbd>
    </button>
  </header>
</template>

<style scoped>
.app-top-bar {
  height: 64px;
  position: fixed;
  z-index: 10;
  top: 0;
  right: 0;
  left: 248px;
  padding: 0 24px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 24px;
  border-bottom: 1px solid var(--border, #e4e7ec);
  background: color-mix(in srgb, var(--surface, #fff) 94%, transparent);
  color: var(--text-primary, #182230);
}

.app-top-bar__context,
.app-top-bar__breadcrumbs {
  min-width: 0;
  display: flex;
  align-items: center;
}

.app-top-bar__context {
  gap: 12px;
}

.app-top-bar__breadcrumbs {
  gap: 7px;
  overflow: hidden;
  color: var(--text-secondary, #667085);
  font-size: 13px;
  white-space: nowrap;
}

.app-top-bar__breadcrumbs span:last-child {
  overflow: hidden;
  color: var(--text-primary, #182230);
  font-weight: 700;
  text-overflow: ellipsis;
}

.app-top-bar__breadcrumbs a {
  overflow: hidden;
  color: inherit;
  text-overflow: ellipsis;
  text-decoration: none;
}

.app-top-bar__breadcrumbs a:hover {
  color: var(--accent, #176b64);
}

.app-top-bar__breadcrumbs a:focus-visible,
.app-top-bar__search:focus-visible {
  outline: 3px solid var(--focus, #2e90fa);
  outline-offset: 2px;
}

.app-top-bar__search {
  min-width: 220px;
  height: 38px;
  padding: 0 10px;
  display: flex;
  align-items: center;
  gap: 9px;
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 6px;
  background: var(--surface, #fff);
  color: var(--text-secondary, #667085);
  font: inherit;
  font-size: 13px;
  text-align: left;
  cursor: pointer;
}

.app-top-bar__search:hover {
  border-color: color-mix(in srgb, var(--accent, #176b64) 35%, var(--border, #e4e7ec));
}

.app-top-bar__search span {
  flex: 1;
}

kbd {
  border: 1px solid var(--border, #e4e7ec);
  border-radius: 4px;
  padding: 2px 5px;
  color: var(--text-secondary, #667085);
  font-family: inherit;
  font-size: 10px;
  white-space: nowrap;
}

.app-top-bar__menu {
  display: none;
}

@media (max-width: 767px) {
  .app-top-bar {
    left: 0;
    padding: 0 12px;
  }

  .app-top-bar__menu {
    display: inline-flex;
  }

  .app-top-bar__search {
    min-width: 40px;
    width: 40px;
    justify-content: center;
  }

  .app-top-bar__search span,
  .app-top-bar__search kbd {
    display: none;
  }
}
</style>
