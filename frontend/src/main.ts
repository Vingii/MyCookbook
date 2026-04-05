import { createApp } from 'vue'
import { createPinia } from 'pinia'
import { storeToken } from './api/client'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import './style.css'
import App from './App.vue'
import router from './router'

const vuetify = createVuetify({
  components,
  directives,
  theme: {
    themes: {
      light: { colors: { primary: '#594AE2' } },
      dark:  { colors: {
          background: '#32333d',
          surface: '#32333d',
          primary: '#7C6FF0' } },
    },
  },
})

const params = new URLSearchParams(window.location.search)
const urlToken = params.get('token')
if (urlToken) {
  storeToken(urlToken)
  params.delete('token')
  const newSearch = params.toString()
  const newUrl = window.location.pathname + (newSearch ? '?' + newSearch : '') + window.location.hash
  window.history.replaceState({}, '', newUrl)
}

createApp(App).use(createPinia()).use(router).use(vuetify).mount('#app')
