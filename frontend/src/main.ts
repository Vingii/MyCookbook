import { createApp } from 'vue'
import { createPinia } from 'pinia'
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
      dark:  { colors: { primary: '#7C6FF0' } },
    },
  },
})

createApp(App).use(createPinia()).use(router).use(vuetify).mount('#app')
