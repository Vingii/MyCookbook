<template>
  <v-app :theme="ui.theme">
    <v-app-bar color="primary" flat>
      <v-btn variant="text" :to="'/'" class="text-h6 font-weight-bold">MyCookbook</v-btn>
      <v-btn variant="text" :to="'/planner'">{{ ui.t.navPlanner }}</v-btn>
      <v-btn variant="text" :to="'/shared'">{{ ui.t.navShared }}</v-btn>
      <v-spacer />
      <!-- The full bar needs ~660px, so narrow screens get the secondary actions in an overflow menu. -->
      <v-menu v-if="compactBar" location="bottom end">
        <template #activator="{ props: menuProps }">
          <v-btn v-bind="menuProps" icon="mdi-dots-vertical" variant="text" :aria-label="ui.t.navMenu" />
        </template>
        <v-list density="compact">
          <v-list-item
            :prepend-icon="ui.theme === 'dark' ? 'mdi-weather-sunny' : 'mdi-weather-night'"
            :title="ui.theme === 'dark' ? ui.t.lightMode : ui.t.darkMode"
            @click="ui.toggleTheme"
          />
          <v-list-item prepend-icon="mdi-translate" :title="ui.locale === 'en' ? 'Čeština' : 'English'" @click="toggleLocale" />
          <v-list-item prepend-icon="mdi-bug" :title="ui.t.bugReport" @click="feedbackOpen = true" />
          <v-list-item prepend-icon="mdi-history" :title="ui.t.whatsNew" @click="openChangelogManually" />
          <v-list-item prepend-icon="mdi-cog" :title="ui.t.navSettings" :to="'/settings'" />
          <v-list-item prepend-icon="mdi-logout" :title="ui.t.navLogout" href="/api/auth/logout" />
        </v-list>
      </v-menu>
      <template v-else>
        <v-btn :icon="ui.theme === 'dark' ? 'mdi-weather-sunny' : 'mdi-weather-night'" variant="text" @click="ui.toggleTheme" />
        <v-btn variant="text" class="text-caption font-weight-bold" @click="toggleLocale">
          {{ ui.locale === 'en' ? 'EN' : 'CS' }}
        </v-btn>
        <v-btn icon="mdi-bug" variant="text" :title="ui.t.bugReport" @click="feedbackOpen = true" />
        <v-btn icon="mdi-history" variant="text" :title="ui.t.whatsNew" @click="openChangelogManually" />
        <v-btn variant="text" :to="'/settings'">{{ ui.t.navSettings }}</v-btn>
        <v-btn variant="text" href="/api/auth/logout">{{ ui.t.navLogout }}</v-btn>
      </template>
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
import { ref, computed, onMounted } from 'vue'
import { useDisplay } from 'vuetify'
import { useUiStore } from './stores/ui'
import { changelogApi } from './api/changelog'
import FeedbackDialog from './components/FeedbackDialog.vue'
import ChangelogDialog from './components/ChangelogDialog.vue'

const ui = useUiStore()
const { width } = useDisplay()
// Measured: the full bar is ~660px wide in Czech (the longest labels), so collapse a little above that.
const compactBar = computed(() => width.value < 700)
const feedbackOpen = ref(false)
const changelogOpen = ref(false)
const changelogSinceVersion = ref<string | null>(null)

function toggleLocale() {
  ui.setLocale(ui.locale === 'en' ? 'cs' : 'en')
}

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
