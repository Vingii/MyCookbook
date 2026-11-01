import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('../views/RecipeBrowserView.vue') },
    { path: '/recipe/:guid', component: () => import('../views/RecipeViewerView.vue') },
    { path: '/planner', component: () => import('../views/PlannerView.vue') },
    { path: '/export', component: () => import('../views/ExportView.vue') },
    { path: '/settings', component: () => import('../views/SettingsView.vue') },
    { path: '/shared', component: () => import('../views/SharedView.vue') },
    // Cookbook share links from Settings point here (a leftover from the old UI); keep them working.
    { path: '/browser', redirect: (to) => ({ path: '/', query: to.query }) },
    { path: '/recipe/shared/:guid', component: () => import('../views/SharedRecipeView.vue'), meta: { public: true } },
    { path: '/unauthorized', component: () => import('../views/UnauthorizedView.vue'), meta: { public: true } },
  ],
})

router.beforeEach(async (to) => {
  if (to.meta.public) return
  const auth = useAuthStore()
  await auth.load()
  if (!auth.isAuthenticated) {
    return '/unauthorized'
  }
})

export default router
