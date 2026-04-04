import { createBdd } from 'playwright-bdd'
import { test } from './api'

export { test }
export const { Given, When, Then } = createBdd(test)
