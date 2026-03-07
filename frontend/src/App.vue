<template>
  <v-app :theme="ui.theme">
    <v-app-bar color="primary" flat>
      <v-btn variant="text" :to="'/'" class="text-h6 font-weight-bold">MyCookbook</v-btn>
      <v-btn variant="text" :to="'/browser'">{{ ui.t.navRecipes }}</v-btn>
      <v-btn variant="text" :to="'/planner'">{{ ui.t.navPlanner }}</v-btn>
      <v-spacer />
      <v-btn :icon="ui.theme === 'dark' ? 'mdi-weather-sunny' : 'mdi-weather-night'" variant="text" @click="ui.toggleTheme" />
      <v-btn variant="text" class="text-caption font-weight-bold" @click="ui.setLocale(ui.locale === 'en' ? 'cs' : 'en')">
        {{ ui.locale === 'en' ? 'EN' : 'CS' }}
      </v-btn>
      <v-btn variant="text" :to="'/settings'">{{ ui.t.navSettings }}</v-btn>
      <v-btn variant="text" href="/api/auth/logout">{{ ui.t.navLogout }}</v-btn>
    </v-app-bar>
    <v-main>
      <v-container>
        <router-view />
      </v-container>
    </v-main>
  </v-app>
</template>

<script setup lang="ts">
import { useUiStore } from './stores/ui'
const ui = useUiStore()
</script>
