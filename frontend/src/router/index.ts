import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: () => import('../views/DashboardView.vue') },
    { path: '/browser', component: () => import('../views/RecipeBrowserView.vue') },
    { path: '/recipe/:guid', component: () => import('../views/RecipeViewerView.vue') },
    { path: '/planner', component: () => import('../views/PlannerView.vue') },
    { path: '/export', component: () => import('../views/ExportView.vue') },
    { path: '/random', component: () => import('../views/RandomRecipeView.vue') },
    { path: '/settings', component: () => import('../views/SettingsView.vue') },
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
