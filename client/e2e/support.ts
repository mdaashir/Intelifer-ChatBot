import { Page, expect } from '@playwright/test';

export const PASSWORD = 'Password123';

/** From appsettings.Development.json - the account the API seeds on first run. */
export const ADMIN_EMAIL = 'admin@intellifer.local';
export const ADMIN_PASSWORD = 'Admin123!';

/**
 * Accounts are created against a real database that is not reset between runs, so
 * every test needs its own.
 */
export function uniqueEmail(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1_000_000)}@example.com`;
}

export async function register(page: Page, email: string): Promise<void> {
  await page.goto('/register');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Create account' }).click();

  await expect(page).toHaveURL(/\/chat$/);
}

export async function signIn(page: Page, email: string, password: string): Promise<void> {
  await page.goto('/login');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password').fill(password);
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page).toHaveURL(/\/chat$/);
}

export async function signOut(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Sign out' }).click();

  await expect(page).toHaveURL(/\/login$/);
}

export async function sendMessage(page: Page, content: string): Promise<void> {
  await page.getByLabel('Message').fill(content);
  await page.getByRole('button', { name: 'Send' }).click();
}
