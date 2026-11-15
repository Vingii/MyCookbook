/** BCP 47 tag used for dates and numbers in each UI language (matches Vuetify's own mapping). */
export function localeTag(locale: string) {
  return locale === 'cs' ? 'cs-CZ' : 'en-US'
}

/** First day of the week for a locale tag, as in Date.getDay(): 0 = Sunday, 1 = Monday. */
export function firstDayOfWeek(tag: string): number {
  try {
    const loc = new Intl.Locale(tag) as Intl.Locale & {
      getWeekInfo?: () => { firstDay: number }
      weekInfo?: { firstDay: number }
    }
    const info = loc.getWeekInfo?.() ?? loc.weekInfo
    // Intl numbers weekdays 1 (Monday) to 7 (Sunday).
    if (info) return info.firstDay % 7
  } catch {
    // Intl.Locale unavailable or the tag is invalid: fall through.
  }
  return /-US$/i.test(tag) ? 0 : 1
}

/** YYYY-MM-DD in local time (toISOString would shift the day around midnight). */
export function isoDate(d: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

/** Parses YYYY-MM-DD as a local date. */
export function parseIsoDate(s: string) {
  const [y, m, d] = s.split('-').map(Number)
  return new Date(y!, m! - 1, d!)
}

/** The whole next week: from the first day of the week strictly after today, to six days later. */
export function nextWeekRange(today: Date, firstDay: number) {
  const ahead = ((firstDay - today.getDay() + 7) % 7) || 7
  const start = new Date(today.getFullYear(), today.getMonth(), today.getDate() + ahead)
  const end = new Date(start.getFullYear(), start.getMonth(), start.getDate() + 6)
  return { from: isoDate(start), to: isoDate(end) }
}
