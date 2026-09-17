import { expect, test } from '@playwright/test';
import { ADMIN_EMAIL, ADMIN_PASSWORD, PASSWORD, signIn, uniqueEmail } from './support';

test.beforeEach(async ({ page }) => {
  await signIn(page, ADMIN_EMAIL, ADMIN_PASSWORD);
});

test('an administrator sees the admin link and the account list', async ({ page }) => {
  await expect(page.getByRole('link', { name: 'Users' })).toBeVisible();

  await page.getByRole('link', { name: 'Users' }).click();

  await expect(page).toHaveURL(/\/admin\/users$/);
  await expect(page.getByRole('cell', { name: ADMIN_EMAIL })).toBeVisible();
});

test('an administrator can create an account', async ({ page }) => {
  await page.goto('/admin/users');

  const created = uniqueEmail('created');

  await page.getByRole('button', { name: 'Add user' }).click();
  await page.getByLabel('Email').fill(created);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Create' }).click();

  await expect(page.getByRole('cell', { name: created })).toBeVisible();
});

test('an administrator can promote an account to administrator', async ({ page }) => {
  await page.goto('/admin/users');

  const created = uniqueEmail('promote');

  await page.getByRole('button', { name: 'Add user' }).click();
  await page.getByLabel('Email').fill(created);
  await page.getByLabel('Password').fill(PASSWORD);
  await page.getByRole('button', { name: 'Create' }).click();
  await expect(page.getByRole('cell', { name: created })).toBeVisible();

  const row = page.getByRole('row', { name: new RegExp(created) });

  await row.getByRole('button', { name: 'Edit' }).click();
  await page.getByLabel('Role').click();
  await page.getByRole('option', { name: 'Admin' }).click();
  await page.getByRole('button', { name: 'Save' }).click();

  await expect(page.getByRole('row', { name: new RegExp(created) })).toContainText('Admin');
});

test('an administrator cannot delete their own account', async ({ page }) => {
  await page.goto('/admin/users');

  const ownRow = page.getByRole('row', { name: new RegExp(ADMIN_EMAIL) });

  await expect(ownRow.getByRole('button', { name: 'Delete' })).toBeDisabled();
});
