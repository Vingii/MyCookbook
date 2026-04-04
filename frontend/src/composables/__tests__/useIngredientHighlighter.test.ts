import { describe, it, expect, vi, beforeEach } from 'vitest'

// highlightText and getHighlightWords are tested here.
// The composable has a module-level dict cache (Map). Tests that call getHighlightWords
// need to reset the module between them to start with a clean cache.

describe('highlightText', () => {
  // Import once for the pure highlightText tests — no dict cache involved
  let highlightText: (text: string, highlights: Set<string>) => string

  beforeEach(async () => {
    vi.resetModules()
    const mod = await import('../useIngredientHighlighter')
    highlightText = mod.highlightText
  })

  it('returns escaped html when highlight set is empty', () => {
    expect(highlightText('<b>hello</b>', new Set())).toBe('&lt;b&gt;hello&lt;/b&gt;')
  })

  it('wraps matched word in <mark>', () => {
    expect(highlightText('add flour now', new Set(['flour']))).toBe('add <mark>flour</mark> now')
  })

  it('passes through unmatched words unchanged', () => {
    expect(highlightText('add sugar', new Set(['flour']))).toBe('add sugar')
  })

  it('matches case-insensitively', () => {
    expect(highlightText('Add Flour', new Set(['flour']))).toBe('Add <mark>Flour</mark>')
  })

  it('matches Czech diacritics correctly', () => {
    expect(highlightText('přidej mouku', new Set(['mouku']))).toBe('přidej <mark>mouku</mark>')
  })

  it('preserves punctuation around highlighted words', () => {
    expect(highlightText('flour, salt', new Set(['flour']))).toBe('<mark>flour</mark>, salt')
  })

  it('escapes ampersands in text', () => {
    expect(highlightText('a & b', new Set())).toBe('a &amp; b')
  })

  it('escapes html in matched words', () => {
    // A word that contains < would be split by the letter-only regex so escaping
    // is tested via the non-letter gap between words
    expect(highlightText('flour > sugar', new Set(['flour']))).toBe('<mark>flour</mark> &gt; sugar')
  })
})

describe('getHighlightWords', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.restoreAllMocks()
  })

  function makeFetchMock(nounJsonl: string, adjJsonl: string = '') {
    return vi.fn().mockImplementation((url: string) => {
      const body = url.includes('cs-nouns') ? nounJsonl : adjJsonl
      return Promise.resolve({
        text: () => Promise.resolve(body),
      })
    })
  }

  it('returns all inflections for a word found in the dictionary', async () => {
    vi.stubGlobal(
      'fetch',
      makeFetchMock(
        JSON.stringify({ Word: 'mouka', Inflections: ['mouka', 'mouky', 'mouce'] }) + '\n'
      )
    )
    const { getHighlightWords } = await import('../useIngredientHighlighter')

    const result = await getHighlightWords(['mouka'])

    expect(result.has('mouka')).toBe(true)
    expect(result.has('mouky')).toBe(true)
    expect(result.has('mouce')).toBe(true)
  })

  it('returns the word itself when not found in the dictionary', async () => {
    vi.stubGlobal('fetch', makeFetchMock(''))
    const { getHighlightWords } = await import('../useIngredientHighlighter')

    const result = await getHighlightWords(['quinoa'])

    expect(result.has('quinoa')).toBe(true)
  })

  it('filters out words with 2 or fewer characters', async () => {
    vi.stubGlobal('fetch', makeFetchMock(''))
    const { getHighlightWords } = await import('../useIngredientHighlighter')

    const result = await getHighlightWords(['na pánev'])

    expect(result.has('na')).toBe(false)
    expect(result.has('pánev')).toBe(true)
  })

  it('looks up each word in a multi-word ingredient name', async () => {
    vi.stubGlobal('fetch', makeFetchMock(''))
    const { getHighlightWords } = await import('../useIngredientHighlighter')

    const result = await getHighlightWords(['wheat flour'])

    expect(result.has('wheat')).toBe(true)
    expect(result.has('flour')).toBe(true)
  })

  it('calls fetch only once per dictionary file across multiple calls', async () => {
    const fetchMock = makeFetchMock('')
    vi.stubGlobal('fetch', fetchMock)
    const { getHighlightWords } = await import('../useIngredientHighlighter')

    await getHighlightWords(['apple'])
    await getHighlightWords(['pear'])

    // Two dict files (cs-nouns + cs-adj), each fetched once regardless of number of calls
    expect(fetchMock).toHaveBeenCalledTimes(2)
  })
})
