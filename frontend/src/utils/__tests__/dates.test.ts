import { describe, it, expect } from 'vitest'
import { firstDayOfWeek, isoDate, localeTag, nextWeekRange, parseIsoDate } from '../dates'

describe('dates', () => {
  it('maps UI languages to locale tags', () => {
    expect(localeTag('cs')).toBe('cs-CZ')
    expect(localeTag('en')).toBe('en-US')
  })

  it('knows the first day of the week', () => {
    expect(firstDayOfWeek('cs-CZ')).toBe(1)
    expect(firstDayOfWeek('en-US')).toBe(0)
    expect(firstDayOfWeek('en-GB')).toBe(1)
  })

  it('round-trips local ISO dates', () => {
    expect(isoDate(new Date(2026, 0, 5, 23, 59))).toBe('2026-01-05')
    expect(isoDate(parseIsoDate('2026-02-28'))).toBe('2026-02-28')
  })

  it.each([
    // [today, first day, from, to]
    ['2026-11-15', 1, '2026-11-16', '2026-11-22'], // Sunday, Monday weeks → tomorrow
    ['2026-11-16', 1, '2026-11-23', '2026-11-29'], // Monday → the following Monday
    ['2026-11-18', 1, '2026-11-23', '2026-11-29'], // Wednesday
    ['2026-11-15', 0, '2026-11-22', '2026-11-28'], // Sunday, Sunday weeks → a week later
    ['2026-11-21', 0, '2026-11-22', '2026-11-28'], // Saturday
    ['2026-12-30', 1, '2027-01-04', '2027-01-10'], // across the new year
  ])('next week from %s (week starts on %i) is %s – %s', (today, first, from, to) => {
    expect(nextWeekRange(parseIsoDate(today), first)).toEqual({ from, to })
  })
})
