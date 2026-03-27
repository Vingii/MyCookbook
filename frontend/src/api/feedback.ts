import client from './client'

export const feedbackApi = {
  send: (message: string, files?: File[]) => {
    const form = new FormData()
    form.append('message', message)
    for (const file of files ?? []) {
      form.append('files', file)
    }
    return client.post('/feedback', form)
  },
}
