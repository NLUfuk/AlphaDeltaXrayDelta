import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { toApiError } from './api'

// Minimal AxiosError carrying a response. Only the fields toApiError reads are populated.
function axiosErrorWith(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders() }
  return new AxiosError('request failed', 'ERR_BAD_REQUEST', config, {}, {
    status,
    statusText: '',
    data,
    headers: {},
    config,
  })
}

/** No response at all — offline, DNS failure, connection refused. */
function axiosErrorWithoutResponse(): AxiosError {
  return new AxiosError('Network Error', 'ERR_NETWORK', { headers: new AxiosHeaders() }, {})
}

describe('toApiError', () => {
  it('reads the server envelope when there is one', () => {
    const err = axiosErrorWith(401, { code: 'invite.invalid', message: 'This invitation is invalid.' })
    expect(toApiError(err)).toEqual({
      code: 'invite.invalid',
      message: 'This invitation is invalid.',
      details: undefined,
    })
  })

  it('keeps the per-field details a validation failure carries', () => {
    const details = [{ field: 'newPassword', error: 'Parola en az bir rakam içermeli.' }]
    const err = axiosErrorWith(400, { code: 'validation.failed', message: 'Validation failed.', details })
    expect(toApiError(err).details).toEqual(details)
  })

  /**
   * THE regression test. The response interceptor rejects with `toApiError(error)`, so by the time a
   * screen calls `errorText(err)` the value is already an ApiError — never an AxiosError. Converting a
   * second time used to fail `isAxiosError` and fall through to "server unreachable", discarding a
   * perfectly good `{code:"invite.invalid"}` the server had actually sent. Conversion must be idempotent.
   */
  it('is idempotent — converting an already-converted error changes nothing', () => {
    const err = axiosErrorWith(401, { code: 'invite.invalid', message: 'This invitation is invalid.' })
    const once = toApiError(err)
    expect(toApiError(once)).toEqual(once)
    expect(toApiError(toApiError(once)).code).toBe('invite.invalid')
  })

  it('never reports a responding server as unreachable', () => {
    // Every shape the interceptor can hand back, twice-converted, must keep its real code.
    for (const status of [400, 401, 403, 404, 409, 429, 500]) {
      const twice = toApiError(toApiError(axiosErrorWith(status, { code: 'some.code', message: 'x' })))
      expect(twice.code).not.toBe('network.error')
    }
  })

  describe('responses with no usable envelope', () => {
    // The rate limiter rejects with a bare 429, the JWT middleware with a bare 401, a proxy with an
    // HTML 502. All code-less — but the server did answer, so the status line is what we read.
    it('maps a bare 429 to the rate-limit message', () => {
      expect(toApiError(axiosErrorWith(429, '')).code).toBe('rate.limited')
    })

    it('maps a bare 401 to the session message', () => {
      expect(toApiError(axiosErrorWith(401, '')).code).toBe('auth.required')
    })

    it('maps an HTML 502 from a proxy to a server error', () => {
      expect(toApiError(axiosErrorWith(502, '<html>Bad Gateway</html>')).code).toBe('server.error')
    })

    it('falls back to a generic code for an unmapped 4xx', () => {
      expect(toApiError(axiosErrorWith(418, '')).code).toBe('unknown.error')
    })
  })

  describe('network.error is reserved for a genuinely absent response', () => {
    it('is used when the request never reached a server', () => {
      expect(toApiError(axiosErrorWithoutResponse()).code).toBe('network.error')
    })

    it('is used for values that are not errors we understand', () => {
      for (const junk of [new Error('boom'), undefined, null, 'a string', 42, {}]) {
        expect(toApiError(junk).code).toBe('network.error')
      }
    })
  })
})
