import { expect, test } from '@playwright/test';
import { register, signIn, uniqueEmail } from './support';

test('an anonymous visitor is sent to the login page', async ({ page }) => {
  await page.goto('/chat');

  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('button', { name: 'Sign in' })).toBeVisible();
});

test('a visitor with no account can register and lands in the chat', async ({ page }) => {
  await register(page, uniqueEmail('register'));

  await expect(page.getByLabel('Message')).toBeVisible();
});

test('a signed-in visitor is sent away from the login page', async ({ page }) => {
  await register(page, uniqueEmail('guard'));

  await page.goto('/login');

  await expect(page).toHaveURL(/\/chat$/);
});

test('bad credentials are reported and do not sign anyone in', async ({ page }) => {
  await page.goto('/login');
  await page.getByLabel('Email').fill('nobody@example.com');
  await page.getByLabel('Password').fill('definitely-wrong');
  await page.getByRole('button', { name: 'Sign in' }).click();

  await expect(page.getByText('Invalid email or password.')).toBeVisible();
  await expect(page).toHaveURL(/\/login$/);
});

test('a regular account gets no admin link and cannot open the admin area', async ({ page }) => {
  await register(page, uniqueEmail('plain'));

  await expect(page.getByRole('link', { name: 'Users' })).toHaveCount(0);

  await page.goto('/admin/users');

  await expect(page).toHaveURL(/\/chat$/);
});

test('signing out returns to the login page and protects the chat again', async ({ page }) => {
  await register(page, uniqueEmail('signout'));

  await page.getByRole('button', { name: 'Sign out' }).click();

  await expect(page).toHaveURL(/\/login$/);

  await page.goto('/chat');

  await expect(page).toHaveURL(/\/login$/);
});

test('an account can change its own password and sign in with the new one', async ({ page }) => {
  const email = uniqueEmail('password');

  await register(page, email);

  await page.getByRole('link', { name: 'Profile' }).click();
  await page.getByLabel('Current password').fill('Password123');
  await page.getByLabel('New password').fill('Password456');
  await page.getByRole('button', { name: 'Change password' }).click();

  await expect(page.getByText('Your password has been changed.')).toBeVisible();

  await page.getByRole('button', { name: 'Sign out' }).click();

  await signIn(page, email, 'Password456');
});
