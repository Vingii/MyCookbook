<template>
  <v-app :theme="ui.theme">
    <v-app-bar color="primary" flat>
      <v-btn variant="text" :to="'/'" class="text-h6 font-weight-bold">MyCookbook</v-btn>
      <v-btn variant="text" :to="'/planner'">{{ ui.t.navPlanner }}</v-btn>
      <v-spacer />
      <v-btn :icon="ui.theme === 'dark' ? 'mdi-weather-sunny' : 'mdi-weather-night'" variant="text" @click="ui.toggleTheme" />
      <v-btn variant="text" class="text-caption font-weight-bold" @click="ui.setLocale(ui.locale === 'en' ? 'cs' : 'en')">
        {{ ui.locale === 'en' ? 'EN' : 'CS' }}
      </v-btn>
      <v-btn icon="mdi-bug" variant="text" :title="ui.t.bugReport" @click="feedbackOpen = true" />
      <v-btn icon="mdi-history" variant="text" :title="ui.t.whatsNew" @click="openChangelogManually" />
      <v-btn variant="text" :to="'/settings'">{{ ui.t.navSettings }}</v-btn>
      <v-btn variant="text" href="/api/auth/logout">{{ ui.t.navLogout }}</v-btn>
    </v-app-bar>
    <v-main>
      <v-container>
        <router-view />
      </v-container>
    </v-main>

    <FeedbackDialog v-model="feedbackOpen" />
    <ChangelogDialog v-model="changelogOpen" :since-version="changelogSinceVersion" />
  </v-app>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useUiStore } from './stores/ui'
import { changelogApi } from './api/changelog'
import FeedbackDialog from './components/FeedbackDialog.vue'
import ChangelogDialog from './components/ChangelogDialog.vue'

const ui = useUiStore()
const feedbackOpen = ref(false)
const changelogOpen = ref(false)
const changelogSinceVersion = ref<string | null>(null)

function openChangelogManually() {
  changelogSinceVersion.value = null
  changelogOpen.value = true
}

onMounted(async () => {
  try {
    const [currentVersion, lastSeen] = await Promise.all([
      changelogApi.getVersion(),
      changelogApi.getLastSeen(),
    ])
    if (lastSeen !== currentVersion) {
      changelogSinceVersion.value = lastSeen
      changelogOpen.value = true
      await changelogApi.markAsSeen()
    }
  } catch {
    // Ignore changelog version check errors
  }
})
</script>
