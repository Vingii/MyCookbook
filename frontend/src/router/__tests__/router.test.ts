import { describe, it, expect } from 'vitest'
import router from '../index'

describe('router', () => {
  it('has a Shared page', () => {
    expect(router.resolve('/shared').matched).toHaveLength(1)
  })
})
