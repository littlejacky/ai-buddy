import { describe, expect, it } from 'vitest'
import { timeoutError, toUserError } from '../lib/errors'

describe('toUserError mapping', () => {
  it('maps microphone denied error properly', () => {
    const err = new DOMException('Permission denied', 'NotAllowedError')
    const userFacing = toUserError(err)

    expect(userFacing.code).toBe('mic_denied')
    expect(userFacing.title).toContain('麦克风')
  })

  it('maps timeout error properly', () => {
    const err = timeoutError('WebRTC 握手超时')
    const userFacing = toUserError(err)

    expect(userFacing.code).toBe('timeout')
    expect(userFacing.title).toContain('超时')
  })
})
