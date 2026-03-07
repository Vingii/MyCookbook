<template>
  <v-app>
    <v-app-bar color="primary" flat>
      <v-btn variant="text" :to="'/'" class="text-h6 font-weight-bold">MyCookbook</v-btn>
      <v-btn variant="text" :to="'/planner'">Planner</v-btn>
      <v-spacer />
      <v-btn variant="text" :to="'/settings'">Settings</v-btn>
      <v-btn variant="text" href="/api/auth/logout">Logout</v-btn>
    </v-app-bar>
    <v-main>
      <v-container>
        <router-view />
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
</script>
