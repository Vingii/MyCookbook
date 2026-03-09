import client from './client'

export interface ChangelogEntry {
  version: string
  releaseDate?: string
  rawHtml: string
}

export const changelogApi = {
  getEntries: () => client.get<ChangelogEntry[]>('/changelog').then((r) => r.data),
  getVersion: () => client.get<string>('/changelog/version').then((r) => r.data),
}
