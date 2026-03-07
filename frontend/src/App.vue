<template>
  <nav style="padding: 0.5rem 1rem; background: #1a1a2e; color: white; display: flex; gap: 1rem; align-items: center;">
    <router-link to="/" style="color: white; text-decoration: none;"><strong>MyCookbook</strong></router-link>
    <router-link to="/planner" style="color: white;">Planner</router-link>
    <div style="margin-left: auto; display: flex; gap: 1rem; align-items: center;">
      <router-link to="/settings" style="color: white;">Settings</router-link>
      <a href="/api/auth/logout" style="color: white;">Logout</a>
    </div>
  </nav>
  <main style="padding: 1rem 2rem;">
    <router-view />
  </main>
</template>

<script setup lang="ts">
</script>
