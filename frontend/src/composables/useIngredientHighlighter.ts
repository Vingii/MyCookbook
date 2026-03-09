// Maps every inflected form to the full set of inflections for that base word.
type InflectionMap = Map<string, Set<string>>

const cache = new Map<string, InflectionMap>()

async function loadDict(filename: string): Promise<InflectionMap> {
  if (cache.has(filename)) return cache.get(filename)!

  const response = await fetch(`/dictionaries/${filename}`)
  const text = await response.text()
  const map: InflectionMap = new Map()

  for (const line of text.split('\n')) {
    if (!line.trim()) continue
    try {
      const { Inflections } = JSON.parse(line) as { Word: string; Inflections: string[] }
      const lower = Inflections.map((i) => i.toLowerCase())
      const set = new Set(lower)
      for (const inf of lower) {
        const existing = map.get(inf)
        if (existing) {
          for (const i of set) existing.add(i)
        } else {
          map.set(inf, new Set(set))
        }
      }
    } catch { /* skip malformed lines */ }
  }

  cache.set(filename, map)
  return map
}

let dict: InflectionMap | null = null
let loading: Promise<void> | null = null

function ensureLoaded(): Promise<void> {
  if (dict) return Promise.resolve()
  if (loading) return loading
  loading = Promise.all([loadDict('cs-nouns.jsonl'), loadDict('cs-adj.jsonl')]).then(([nouns, adjs]) => {
    const merged: InflectionMap = new Map(nouns)
    for (const [word, inflections] of adjs) {
      const existing = merged.get(word)
      if (existing) {
        for (const i of inflections) existing.add(i)
      } else {
        merged.set(word, new Set(inflections))
      }
    }
    dict = merged
  })
  return loading
}

export async function getHighlightWords(ingredientNames: string[]): Promise<Set<string>> {
  await ensureLoaded()
  const highlights = new Set<string>()
  const words = ingredientNames.flatMap((name) =>
    name.toLowerCase().split(/\s+/).filter((w) => w.length > 2)
  )
  for (const word of words) {
    const inflections = dict!.get(word)
    if (inflections) {
      for (const inf of inflections) highlights.add(inf)
    } else {
      highlights.add(word)
    }
  }
  return highlights
}

export function highlightText(text: string, highlights: Set<string>): string {
  if (!highlights.size) return escapeHtml(text)

  // Split on Unicode letter runs so Czech diacritics work correctly.
  const wordRegex = /\p{L}+/gu
  const parts: string[] = []
  let lastIndex = 0
  let match: RegExpExecArray | null

  while ((match = wordRegex.exec(text)) !== null) {
    if (match.index > lastIndex) {
      parts.push(escapeHtml(text.slice(lastIndex, match.index)))
    }
    const word = match[0]
    if (highlights.has(word.toLowerCase())) {
      parts.push(`<mark>${escapeHtml(word)}</mark>`)
    } else {
      parts.push(escapeHtml(word))
    }
    lastIndex = match.index + word.length
  }

  if (lastIndex < text.length) {
    parts.push(escapeHtml(text.slice(lastIndex)))
  }

  return parts.join('')
}

function escapeHtml(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
}
