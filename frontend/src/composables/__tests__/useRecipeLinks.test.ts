import { describe, it, expect } from 'vitest'
import {
  normalizeLinkText,
  parseLink,
  renderWithLinks,
  findLinkQuery,
  applyLinkSuggestion,
  suggestRecipeNames,
} from '../useRecipeLinks'
import type { RecipeLinkDto } from '../../api/types'

const TZATZIKI: RecipeLinkDto = { text: 'Tzatziki', name: 'Tzatziki', guid: 'guid-1' }

const escape = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
const href = (guid: string) => `/recipe/${guid}`

describe('normalizeLinkText', () => {
  it('lowercases, strips diacritics and collapses whitespace', () => {
    expect(normalizeLinkText('  Bramborová   KAŠE ')).toBe('bramborova kase')
  })

  it('matches the backend key for accented names', () => {
    expect(normalizeLinkText('Crème Brûlée')).toBe('creme brulee')
  })
})

describe('parseLink', () => {
  it('uses the same text for both halves when there is no pipe', () => {
    expect(parseLink('Tzatziki')).toEqual({ display: 'Tzatziki', reference: 'Tzatziki' })
  })

  it('splits display from reference on the pipe', () => {
    expect(parseLink('bramborovou kaší|Bramborová kaše')).toEqual({
      display: 'bramborovou kaší',
      reference: 'Bramborová kaše',
    })
  })

  it('splits on the last pipe so the display may contain one', () => {
    expect(parseLink('a|b|Tzatziki')).toEqual({ display: 'a|b', reference: 'Tzatziki' })
  })

  it('trims each half', () => {
    expect(parseLink('  dip  |  Tzatziki  ')).toEqual({ display: 'dip', reference: 'Tzatziki' })
  })

  it('falls back to the other half when one is missing', () => {
    expect(parseLink('|Tzatziki')).toEqual({ display: 'Tzatziki', reference: 'Tzatziki' })
    expect(parseLink('Tzatziki|')).toEqual({ display: 'Tzatziki', reference: 'Tzatziki' })
  })

  it('matches the backend on an empty link', () => {
    expect(parseLink('')).toEqual({ display: '', reference: '' })
  })
})

describe('renderWithLinks', () => {
  it('leaves text without links untouched', () => {
    expect(renderWithLinks('Simmer for 20 minutes.', [], escape, href)).toBe('Simmer for 20 minutes.')
  })

  it('renders a resolved link as an anchor', () => {
    const html = renderWithLinks('Serve with [[Tzatziki]].', [TZATZIKI], escape, href)
    expect(html).toBe('Serve with <a class="recipe-link" href="/recipe/guid-1">Tzatziki</a>.')
  })

  it('matches link text against the map case-insensitively', () => {
    const html = renderWithLinks('Serve with [[tzatziki]].', [TZATZIKI], escape, href)
    expect(html).toContain('href="/recipe/guid-1"')
    expect(html).toContain('>tzatziki<')
  })

  it('shows the display half and resolves on the reference half', () => {
    const html = renderWithLinks('Podávejte s [[tzatzikem|Tzatziki]].', [TZATZIKI], escape, href)
    expect(html).toBe('Podávejte s <a class="recipe-link" href="/recipe/guid-1">tzatzikem</a>.')
  })

  it('does not resolve on the display half', () => {
    const html = renderWithLinks('[[Tzatziki|Baklava]]', [TZATZIKI], escape, href)
    expect(html).toBe('<span class="recipe-link-missing">Tzatziki</span>')
  })

  it('leaves an empty [[]] as plain text', () => {
    expect(renderWithLinks('Nothing [[]] here', [TZATZIKI], escape, href)).toBe('Nothing [[]] here')
  })

  it('renders an unresolved link as muted text without brackets', () => {
    const html = renderWithLinks('Serve with [[Baklava]].', [TZATZIKI], escape, href)
    expect(html).toBe('Serve with <span class="recipe-link-missing">Baklava</span>.')
  })

  it('renders multiple links in one description', () => {
    const pita: RecipeLinkDto = { text: 'Pita', name: 'Pita', guid: 'guid-2' }
    const html = renderWithLinks('[[Tzatziki]] and [[Pita]]', [TZATZIKI, pita], escape, href)
    expect(html).toBe(
      '<a class="recipe-link" href="/recipe/guid-1">Tzatziki</a>' +
      ' and ' +
      '<a class="recipe-link" href="/recipe/guid-2">Pita</a>',
    )
  })

  it('escapes html in link labels', () => {
    const evil: RecipeLinkDto = { text: '<img>', name: '<img>', guid: 'guid-3' }
    const html = renderWithLinks('Use [[<img>]]', [evil], escape, href)
    expect(html).toContain('&lt;img&gt;')
    expect(html).not.toContain('<img>')
  })

  it('escapes quotes in the generated href', () => {
    const html = renderWithLinks('[[Tzatziki]]', [TZATZIKI], escape, () => '/recipe/x"onmouseover="evil()')
    expect(html).not.toContain('onmouseover="evil()"')
    expect(html).toContain('&quot;')
  })

  it('applies renderPlain to the text around links but not to the label', () => {
    const shout = (s: string) => s.toUpperCase()
    const html = renderWithLinks('serve with [[Tzatziki]] now', [TZATZIKI], shout, href)
    expect(html).toBe('SERVE WITH <a class="recipe-link" href="/recipe/guid-1">Tzatziki</a> NOW')
  })

  it('is reusable across calls (regex lastIndex is reset)', () => {
    const text = 'Serve with [[Tzatziki]].'
    const first = renderWithLinks(text, [TZATZIKI], escape, href)
    expect(renderWithLinks(text, [TZATZIKI], escape, href)).toBe(first)
  })
})

describe('findLinkQuery', () => {
  it('returns null when there is no open bracket', () => {
    expect(findLinkQuery('Serve with tzatziki', 19)).toBeNull()
  })

  it('finds an empty query right after typing [[', () => {
    const text = 'Serve with [['
    expect(findLinkQuery(text, text.length)).toEqual({ start: 11, end: 13, query: '' })
  })

  it('returns what has been typed since the [[', () => {
    const text = 'Serve with [[tza'
    expect(findLinkQuery(text, text.length)).toEqual({ start: 11, end: 16, query: 'tza' })
  })

  it('returns null once the link is closed and the caret moved past it', () => {
    const text = 'Serve with [[Tzatziki]] now'
    expect(findLinkQuery(text, text.length)).toBeNull()
  })

  it('extends the range over a closing ]] the caret sits in front of', () => {
    const text = 'Serve with [[tza]]'
    expect(findLinkQuery(text, 16)).toEqual({ start: 11, end: 18, query: 'tza' })
  })

  it('does not span a newline', () => {
    const text = 'Serve with [[\ntza'
    expect(findLinkQuery(text, text.length)).toBeNull()
  })

  it('completes the reference half and remembers the display half', () => {
    const text = 'Serve with [[tzatzikem|Tza'
    expect(findLinkQuery(text, text.length)).toEqual({
      start: 11,
      end: 26,
      query: 'Tza',
      display: 'tzatzikem',
    })
  })

  it('offers everything right after the pipe is typed', () => {
    const text = '[[tzatzikem|'
    expect(findLinkQuery(text, text.length)).toEqual({ start: 0, end: 12, query: '', display: 'tzatzikem' })
  })

  it('stays quiet while the caret is in the display half', () => {
    // Caret sits just after "tza" in "[[tza|Tzatziki]]".
    expect(findLinkQuery('[[tza|Tzatziki]]', 5)).toBeNull()
  })

  it('still fires when the pipe belongs to a later link', () => {
    // The `|` after the caret is inside the *next* link, not this one.
    expect(findLinkQuery('[[tza and [[b|Pita]]', 5)).not.toBeNull()
  })
})

describe('applyLinkSuggestion', () => {
  it('completes a partially typed link and puts the caret after it', () => {
    const text = 'Serve with [[tza'
    const range = findLinkQuery(text, text.length)!
    expect(applyLinkSuggestion(text, range, 'Tzatziki')).toEqual({
      text: 'Serve with [[Tzatziki]]',
      caret: 23,
    })
  })

  it('replaces an existing closing ]] instead of nesting a second one', () => {
    const text = 'Serve with [[tza]] now'
    const range = findLinkQuery(text, 16)!
    expect(applyLinkSuggestion(text, range, 'Tzatziki').text).toBe('Serve with [[Tzatziki]] now')
  })

  it('keeps the display half when completing the reference', () => {
    const text = 'Serve with [[tzatzikem|Tza]] now'
    const range = findLinkQuery(text, 26)!
    expect(applyLinkSuggestion(text, range, 'Tzatziki').text).toBe('Serve with [[tzatzikem|Tzatziki]] now')
  })
})

describe('suggestRecipeNames', () => {
  const names = ['Baklava', 'Gyros', 'Tzatziki', 'Pita with tzatziki']

  it('returns everything (up to the limit) for an empty query', () => {
    expect(suggestRecipeNames(names, '', 2)).toEqual(['Baklava', 'Gyros'])
  })

  it('ranks prefix matches above substring matches', () => {
    expect(suggestRecipeNames(names, 'tza')).toEqual(['Tzatziki', 'Pita with tzatziki'])
  })

  it('ignores case and diacritics', () => {
    expect(suggestRecipeNames(['Bramborová kaše'], 'bramborova')).toEqual(['Bramborová kaše'])
  })

  it('returns nothing when nothing matches', () => {
    expect(suggestRecipeNames(names, 'zzz')).toEqual([])
  })

  it('caps the number of suggestions', () => {
    expect(suggestRecipeNames(names, 'a', 2)).toHaveLength(2)
  })
})
