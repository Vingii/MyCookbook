import { useRoute } from 'vue-router'
import { escapeHtml } from './useIngredientHighlighter'
import type { RecipeLinkDto } from '../api/types'

/**
 * Matches a wiki-style link. The canonical form is `[[display text|Recipe name]]`; the shorthand
 * `[[Recipe name]]` means both are the same and gets expanded when the step is saved.
 * Must stay in sync with `MyCookbook/Utils/RecipeLinks.cs`.
 */
const LINK_REGEX = /\[\[([^[\]\r\n]*)\]\]/g

export interface ParsedLink {
  /** What the reader sees. */
  display: string
  /** The recipe name to resolve. */
  reference: string
}

/**
 * Splits the text between the brackets on its last pipe. Either side may be omitted, in which case
 * it falls back to the other — `[[Tzatziki]]` and `[[|Tzatziki]]` are equivalent.
 */
export function parseLink(inner: string): ParsedLink {
  const pipe = inner.lastIndexOf('|')
  if (pipe < 0) {
    const both = inner.trim()
    return { display: both, reference: both }
  }

  const display = inner.slice(0, pipe).trim()
  const reference = inner.slice(pipe + 1).trim()
  return { display: display || reference, reference: reference || display }
}

/**
 * Case-, whitespace- and diacritics-insensitive key used to match link text against recipe names,
 * so that `[[tzatziki]]` finds a recipe named "Tzatziki".
 */
export function normalizeLinkText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/\p{Diacritic}/gu, '')
    .replace(/\s+/g, ' ')
    .trim()
    .toLowerCase()
}

export function buildLinkMap(links: RecipeLinkDto[]): Map<string, RecipeLinkDto> {
  return new Map(links.map((l) => [normalizeLinkText(l.text), l]))
}

function escapeAttr(s: string): string {
  return escapeHtml(s).replace(/"/g, '&quot;')
}

/**
 * Renders text containing `[[wiki links]]` to HTML. Plain runs are passed through `renderPlain`
 * (which does the ingredient highlighting), resolved links become anchors, and links that point at
 * a recipe that does not exist render as muted text so the editing user can see they are broken.
 */
export function renderWithLinks(
  text: string,
  links: RecipeLinkDto[],
  renderPlain: (part: string) => string,
  href: (guid: string) => string,
): string {
  const map = buildLinkMap(links)
  const parts: string[] = []
  let lastIndex = 0
  let match: RegExpExecArray | null

  LINK_REGEX.lastIndex = 0
  while ((match = LINK_REGEX.exec(text)) !== null) {
    const { display, reference } = parseLink(match[1] ?? '')
    if (!reference) continue // `[[]]` is not a link; leave it for the plain-text run below

    if (match.index > lastIndex) parts.push(renderPlain(text.slice(lastIndex, match.index)))

    const target = map.get(normalizeLinkText(reference))
    if (target) {
      parts.push(`<a class="recipe-link" href="${escapeAttr(href(target.guid))}">${escapeHtml(display)}</a>`)
    } else {
      parts.push(`<span class="recipe-link-missing">${escapeHtml(display)}</span>`)
    }

    lastIndex = match.index + match[0].length
  }

  if (lastIndex < text.length) parts.push(renderPlain(text.slice(lastIndex)))
  return parts.join('')
}

export interface LinkQuery {
  /** Index of the opening `[[`. */
  start: number
  /** Index just past the text to replace, including a closing `]]` when the caret sits before one. */
  end: number
  /** The reference the user is part-way through typing. */
  query: string
  /** Display text to keep when the link already has a `|`; undefined for a bare `[[query`. */
  display?: string
}

/**
 * Detects that the caret sits in the reference position of an unclosed `[[`, which is what opens
 * the autocomplete. Returns null while the caret is in the display half of a `[[display|ref]]`,
 * so editing the label does not pop up a dropdown that would overwrite it.
 */
export function findLinkQuery(text: string, caret: number): LinkQuery | null {
  const before = text.slice(0, caret)
  const start = before.lastIndexOf('[[')
  if (start === -1) return null

  const inner = before.slice(start + 2)
  if (/[[\]\r\n]/.test(inner)) return null

  // Typing inside an already-closed link should replace the whole thing, not nest another one.
  const end = text.startsWith(']]', caret) ? caret + 2 : caret

  const pipe = inner.lastIndexOf('|')
  if (pipe >= 0) return { start, end, query: inner.slice(pipe + 1), display: inner.slice(0, pipe) }

  // No pipe yet before the caret — but if one follows, the caret is in the display half.
  const rest = text.slice(caret)
  const close = rest.search(/\]\]|\[\[|[\r\n]/)
  if ((close === -1 ? rest : rest.slice(0, close)).includes('|')) return null

  return { start, end, query: inner }
}

/** Replaces the in-progress link with a complete one and reports where the caret goes. */
export function applyLinkSuggestion(text: string, range: LinkQuery, name: string): { text: string; caret: number } {
  const inserted = range.display === undefined ? `[[${name}]]` : `[[${range.display}|${name}]]`
  return {
    text: text.slice(0, range.start) + inserted + text.slice(range.end),
    caret: range.start + inserted.length,
  }
}

/** Recipe names matching what the user typed after `[[`, prefix matches first. */
export function suggestRecipeNames(names: string[], query: string, limit = 8): string[] {
  const key = normalizeLinkText(query)
  if (!key) return names.slice(0, limit)

  const prefix: string[] = []
  const contains: string[] = []
  for (const name of names) {
    const normalized = normalizeLinkText(name)
    if (normalized.startsWith(key)) prefix.push(name)
    else if (normalized.includes(key)) contains.push(name)
  }
  return [...prefix, ...contains].slice(0, limit)
}

/**
 * Builds the href for a recipe link, preserving the context the current recipe is being viewed in:
 * the public share page keeps linking to public share pages, and read-only browsing of someone
 * else's cookbook keeps its `user` / `shareToken` query parameters.
 */
export function useRecipeHref() {
  const route = useRoute()

  return (guid: string): string => {
    if (route.path.startsWith('/recipe/shared/')) return `/recipe/shared/${guid}`

    const params = new URLSearchParams()
    if (route.query.user) params.set('user', String(route.query.user))
    if (route.query.shareToken) params.set('shareToken', String(route.query.shareToken))
    const query = params.toString()
    return `/recipe/${guid}${query ? `?${query}` : ''}`
  }
}
