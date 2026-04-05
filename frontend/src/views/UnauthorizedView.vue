<template>
  <v-container>
    <v-row justify="center" align="center" style="height: 60vh;">
      <v-col cols="12" sm="8" md="6">
        <v-alert type="warning" :title="ui.t.notAuthenticated">
          {{ ui.t.notAuthenticatedDesc }}
        </v-alert>
        <v-btn class="mt-4" color="primary" href="/api/auth/login" block>
          {{ ui.t.login }}
        </v-btn>
      </v-col>
    </v-row>
  </v-container>
</template>

<script setup lang="ts">
import { useUiStore } from '../stores/ui'
const ui = useUiStore()
</script>
