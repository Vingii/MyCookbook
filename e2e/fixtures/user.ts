/**
 * E2E tests sign in as their own user (via the dev-only X-Dev-User header) rather than the
 * developer's DEV_AUTO_LOGIN user, so globalSetup's cleanup never deletes the developer's recipes.
 */
export const E2E_USER = 'e2e-user'
export const E2E_HEADERS = { 'X-Dev-User': E2E_USER }
