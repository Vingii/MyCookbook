import client from './client'

export interface ShoppingListItem {
  name: string
  amount: string
  recipes: string[]
}

/** Per-user Home Assistant webhook that adds the shopping list to an HA to-do list. */
export const homeAssistantApi = {
  get: () =>
    client.get<{ webhookUrl: string | null }>('/homeassistant').then((r) => r.data),

  /** Whether a webhook is set up; works with a read-only API token too. */
  status: () =>
    client.get<{ configured: boolean }>('/homeassistant/status').then((r) => r.data),

  save: (webhookUrl: string) =>
    client.put('/homeassistant', { webhookUrl }),

  remove: () =>
    client.delete('/homeassistant'),

  sendShoppingList: (items: ShoppingListItem[]) =>
    client.post('/homeassistant/shopping-list', { items }),
}
