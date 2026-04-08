/** Sentinel value meaning "no expiry / active indefinitely". */
export const ACTIVE_EXPIRE = '9999-01-01';

/** Returns true only when a real expiry date has been set (not null and not the sentinel). */
export function isExpired(expireAt: string | null | undefined): boolean {
  return !!expireAt && expireAt !== ACTIVE_EXPIRE;
}
