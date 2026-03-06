<template>
  <nav style="padding: 0.5rem 1rem; background: #1a1a2e; color: white; display: flex; gap: 1rem; align-items: center;">
    <strong>MyCookbook</strong>
    <router-link to="/" style="color: white;">Dashboard</router-link>
    <router-link to="/browser" style="color: white;">Recipes</router-link>
    <router-link to="/planner" style="color: white;">Planner</router-link>
    <router-link to="/random" style="color: white;">Random</router-link>
    <router-link to="/export" style="color: white;">Export</router-link>
    <div style="margin-left: auto;">
      <a href="/Account/Manage" style="color: white;">Account</a>
      &nbsp;|&nbsp;
      <a href="/Account/Logout" style="color: white;" @click.prevent="logout">Logout</a>
    </div>
  </nav>
  <main style="padding: 1rem 2rem;">
    <router-view />
  </main>
</template>

<script setup lang="ts">
import client from './api/client'

async function logout() {
  const form = new FormData()
  form.append('returnUrl', '/')
  await client.post('/Account/Logout', form).catch(() => {})
  window.location.href = '/Account/Login'
}
</script>
