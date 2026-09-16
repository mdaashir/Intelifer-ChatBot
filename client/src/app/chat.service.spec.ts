import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ChatService } from './core/services/chat.service';

class InMemoryStorage {
  private readonly entries = new Map<string, string>();

  get length(): number {
    return this.entries.size;
  }

  clear(): void {
    this.entries.clear();
  }

  getItem(key: string): string | null {
    return this.entries.get(key) ?? null;
  }

  key(index: number): string | null {
    return [...this.entries.keys()][index] ?? null;
  }

  removeItem(key: string): void {
    this.entries.delete(key);
  }

  setItem(key: string, value: string): void {
    this.entries.set(key, value);
  }
}

describe('ChatService', () => {
  const storageKey = 'intellifer.sessionId';

  // The runner does not expose a usable localStorage, so the service is given a
  // real one to talk to rather than depending on the test environment's globals.
  beforeEach(() => {
    Object.defineProperty(globalThis, 'localStorage', {
      value: new InMemoryStorage(),
      configurable: true,
      writable: true,
    });

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('creates a session id and persists it on first use', () => {
    const service = TestBed.inject(ChatService);

    expect(service.session()).toBeTruthy();
    expect(localStorage.getItem(storageKey)).toBe(service.session());
  });

  it('reuses a stored session id, which is what survives a page refresh', () => {
    localStorage.setItem(storageKey, '11111111-1111-1111-1111-111111111111');

    const service = TestBed.inject(ChatService);

    expect(service.session()).toBe('11111111-1111-1111-1111-111111111111');
  });

  it('starts a new conversation under a different session id', () => {
    const service = TestBed.inject(ChatService);
    const original = service.session();

    service.startNewConversation();

    expect(service.session()).not.toBe(original);
    expect(localStorage.getItem(storageKey)).toBe(service.session());
  });
});
