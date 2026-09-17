import { expect, test } from '@playwright/test';
import { register, sendMessage, signOut, uniqueEmail } from './support';

test('a new account gets a reply from the bot', async ({ page }) => {
  await register(page, uniqueEmail('chat'));

  await sendMessage(page, 'hello');

  // '.bubble-text' rather than the bubble itself, whose text also contains the time.
  await expect(page.locator('.bubble--user .bubble-text')).toHaveText(['hello']);
  await expect(page.locator('.bubble--bot .bubble-text')).toContainText(['Hello']);
});

test('the conversation survives a page refresh', async ({ page }) => {
  await register(page, uniqueEmail('refresh'));

  await sendMessage(page, 'hello');
  await expect(page.locator('.bubble')).toHaveCount(2);

  await page.reload();

  // Same browser, so the same session id in localStorage, so the same thread.
  await expect(page.locator('.bubble')).toHaveCount(2);
  await expect(page.locator('.bubble--user .bubble-text')).toHaveText(['hello']);
});

test('a second account in the same browser sees none of the first account conversation', async ({
  page,
}) => {
  await register(page, uniqueEmail('owner'));

  await sendMessage(page, 'this is private');
  await expect(page.locator('.bubble')).toHaveCount(2);

  await signOut(page);

  // The session id is still in localStorage, so this is the same session id with a
  // different account. The server scopes history to both, so nothing comes back.
  await register(page, uniqueEmail('intruder'));

  await expect(page.getByText('No messages yet')).toBeVisible();
  await expect(page.locator('.bubble')).toHaveCount(0);
});

test('starting a new conversation clears the thread', async ({ page }) => {
  await register(page, uniqueEmail('newconversation'));

  await sendMessage(page, 'hello');
  await expect(page.locator('.bubble')).toHaveCount(2);

  await page.getByRole('button', { name: 'New conversation' }).click();

  await expect(page.locator('.bubble')).toHaveCount(0);

  // And the new, empty session id is the one that persists.
  await page.reload();

  await expect(page.locator('.bubble')).toHaveCount(0);
});

test('an empty message cannot be sent', async ({ page }) => {
  await register(page, uniqueEmail('empty'));

  await page.getByLabel('Message').fill('   ');

  await expect(page.getByRole('button', { name: 'Send' })).toBeDisabled();
  await expect(page.locator('.bubble')).toHaveCount(0);
});

test('the message counter follows what has been typed', async ({ page }) => {
  await register(page, uniqueEmail('counter'));

  await page.getByLabel('Message').fill('hello');

  await expect(page.getByText('5/2000')).toBeVisible();
});
