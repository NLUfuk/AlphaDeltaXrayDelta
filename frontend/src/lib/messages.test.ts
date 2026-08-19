import { readFileSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { AxiosError, AxiosHeaders } from 'axios'
import { describe, expect, it } from 'vitest'
import { toApiError } from './api'
import { errorText, greeting, passwordProblem, ticketEvent } from './messages'

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

describe('errorText', () => {
  it('translates a known code into Turkish rather than showing the server sentence', () => {
    const err = axiosErrorWith(401, { code: 'invite.invalid', message: 'This invitation is invalid.' })
    expect(errorText(err)).toBe('Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.')
  })

  /**
   * The user-visible half of the production bug. Screens receive what the response interceptor already
   * converted, so this is the shape `errorText` actually sees in the running app — not a raw AxiosError.
   * It used to render "Sunucuya ulaşılamadı" while the server was answering correctly.
   */
  it('handles the already-converted error the interceptor really hands to screens', () => {
    const fromInterceptor = toApiError(
      axiosErrorWith(401, { code: 'invite.invalid', message: 'This invitation is invalid.' }),
    )
    expect(errorText(fromInterceptor)).toBe('Bu bağlantı geçersiz veya süresi dolmuş. Yeni bir bağlantı isteyin.')
    expect(errorText(fromInterceptor)).not.toContain('Sunucuya ulaşılamadı')
  })

  it('shows which rule broke instead of a generic "check your input"', () => {
    const err = axiosErrorWith(400, {
      code: 'validation.failed',
      message: 'Validation failed.',
      details: [
        { field: 'newPassword', error: 'Parola en az bir büyük harf içermeli.' },
        { field: 'newPassword', error: 'Parola en az bir rakam içermeli.' },
      ],
    })
    expect(errorText(err)).toBe('Parola en az bir büyük harf içermeli. Parola en az bir rakam içermeli.')
  })

  it('falls back to the generic sentence when a validation failure carries no details', () => {
    const err = axiosErrorWith(400, { code: 'validation.failed', message: 'Validation failed.', details: null })
    expect(errorText(err)).toBe('Girdiğiniz bilgileri kontrol edin.')
  })

  it('only says "unreachable" when the request truly never got a response', () => {
    const offline = new AxiosError('Network Error', 'ERR_NETWORK', { headers: new AxiosHeaders() }, {})
    expect(errorText(offline)).toContain('Sunucuya ulaşılamadı')
  })
})

describe('passwordProblem', () => {
  it.each([
    ['kısa', 'Ab1!', 'Parola en az 8 karakter olmalı.'],
    ['büyük harf yok', 'gecerli1!', 'Parola en az bir büyük harf içermeli.'],
    ['küçük harf yok', 'GECERLI1!', 'Parola en az bir küçük harf içermeli.'],
    ['rakam yok', 'Gecerliii!', 'Parola en az bir rakam içermeli.'],
    ['özel karakter yok', 'Gecerli12', 'Parola en az bir özel karakter içermeli (örn. ! @ # $ % & * ? _ -).'],
  ])('reddeder: %s', (_label, password, expected) => {
    expect(passwordProblem(password)).toBe(expected)
  })

  it('accepts a password that meets every rule', () => {
    expect(passwordProblem('Gecerli1!')).toBeNull()
  })

  it('does not count a Turkish letter as a special character', () => {
    // `[^A-Za-z0-9]` would accept this; the hint says a symbol is required, so the check must agree.
    for (const letter of ['ş', 'ğ', 'ı', 'ö', 'ç', 'ü', 'İ', 'Ş']) {
      expect(passwordProblem(`Gecerli1${letter}`)).toContain('özel karakter')
    }
  })

  it('does not count a space as a special character', () => {
    expect(passwordProblem('Gecerli1 ')).toContain('özel karakter')
  })
})

/**
 * The password rules live in two places on purpose (PROGRESS teknik borç #17): the backend is the
 * authority, the frontend copy exists only so the user is told the rules before submitting. A copy is
 * safe only while the two agree — so read the backend's own definition and diff the behaviour, rather
 * than trusting a comment that says "keep these in sync".
 */
describe('special-character set matches the backend definition', () => {
  const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..')
  const validators = readFileSync(
    join(repoRoot, 'src', 'CrmKanban.Application', 'Auth', 'AuthValidators.cs'),
    'utf8',
  )

  const declaration = validators.match(/const string SpecialCharacters = @"(.*)";/)

  it('finds the backend declaration (the parse itself must not rot)', () => {
    expect(declaration).not.toBeNull()
  })

  it('agrees with the backend on every printable ASCII character', () => {
    // C# verbatim string: "" is an escaped quote. Otherwise the class is valid JS regex syntax as-is.
    const backend = new RegExp(declaration![1].replaceAll('""', '"'))

    const disagreements: string[] = []
    for (let code = 32; code < 127; code++) {
      const char = String.fromCharCode(code)
      // 'Gecerli1' already satisfies length/upper/lower/digit, so the verdict turns only on `char`.
      const frontendAccepts = passwordProblem(`Gecerli1${char}`) === null
      if (frontendAccepts !== backend.test(char)) disagreements.push(`${JSON.stringify(char)} (${code})`)
    }
    expect(disagreements).toEqual([])
  })
})

describe('greeting', () => {
  // The home screen's first line. Boundaries, not midpoints: 04:59 is still night, 05:00 is not.
  it.each([
    [0, 'İyi geceler'],
    [4, 'İyi geceler'],
    [5, 'Günaydın'],
    [11, 'Günaydın'],
    [12, 'İyi günler'],
    [17, 'İyi günler'],
    [18, 'İyi akşamlar'],
    [23, 'İyi akşamlar'],
  ])('says the right thing at %i:00', (hour, expected) => {
    expect(greeting(new Date(2026, 7, 19, hour, 0, 0))).toBe(expected)
  })
})

describe('ticketEvent', () => {
  it('names the event type', () => {
    expect(ticketEvent(4).text).toBe('Yeni yorum')
    expect(ticketEvent(5).text).toBe('İç not eklendi')
  })

  it('shows the status name a company chose for its own column', () => {
    expect(ticketEvent(1, 'Teklif Hazırlanıyor').text).toBe('Durum: Teklif Hazırlanıyor')
    // A status change with no name still says something rather than rendering "Durum: undefined".
    expect(ticketEvent(1, null).text).toBe('Durum değişti')
  })

  it('falls back for an event type it does not know (the enum is sparse and can grow)', () => {
    expect(ticketEvent(99).text).toBe('Güncelleme')
  })
})
