import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HubConnectionState } from '@microsoft/signalr';
import { installLocalStorage } from '../../../testing/in-memory-storage';
import { ChatMessage } from '../models/chat-message';
import { ChatService } from './chat.service';

describe('ChatService', () => {
  const sessionKey = 'intellifer.sessionId';

  let http!: HttpTestingController;

  beforeEach(() => {
    installLocalStorage();

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });

    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates a session id and persists it on first use', () => {
    const service = TestBed.inject(ChatService);

    expect(service.session()).toBeTruthy();
    expect(localStorage.getItem(sessionKey)).toBe(service.session());
  });

  it('reuses a stored session id, which is what survives a page refresh', () => {
    localStorage.setItem(sessionKey, '11111111-1111-1111-1111-111111111111');

    expect(TestBed.inject(ChatService).session()).toBe('11111111-1111-1111-1111-111111111111');
  });

  it('starts disconnected', () => {
    expect(TestBed.inject(ChatService).connectionState()).toBe(HubConnectionState.Disconnected);
  });

  it('starts a new conversation under a different session id', () => {
    const service = TestBed.inject(ChatService);
    const original = service.session();

    service.startNewConversation();

    expect(service.session()).not.toBe(original);
    expect(localStorage.getItem(sessionKey)).toBe(service.session());
  });

  it('clears the messages when starting a new conversation', () => {
    const service = TestBed.inject(ChatService);
    const published: ChatMessage[][] = [];

    service.messages$.subscribe((value) => published.push(value));

    service.startNewConversation();

    expect(published[published.length - 1]).toEqual([]);
  });

  it('loads the history for the current session and publishes it', () => {
    const service = TestBed.inject(ChatService);
    const sessionId = service.session();
    const messages: ChatMessage[] = [
      {
        id: 1,
        sessionId,
        content: 'hello',
        sender: 'User',
        createdAtUtc: '2026-01-01T12:00:00Z',
      },
    ];

    const published: ChatMessage[][] = [];
    service.messages$.subscribe((value) => published.push(value));

    let result!: ChatMessage[] | undefined;
    service.loadHistory().subscribe((value) => (result = value));

    const request = http.expectOne(
      (candidate) => candidate.url === '/api/messages' && candidate.method === 'GET',
    );

    expect(request.request.params.get('sessionId')).toBe(sessionId);

    request.flush(messages);

    expect(result).toEqual(messages);
    expect(published[published.length - 1]).toEqual(messages);
  });

  it('refuses to send before the connection is open', async () => {
    const service = TestBed.inject(ChatService);

    await expect(service.sendMessage('hello')).rejects.toThrow();
  });

  it('does nothing when disconnecting without a connection', async () => {
    const service = TestBed.inject(ChatService);

    await expect(service.disconnect()).resolves.toBeUndefined();
    expect(service.connectionState()).toBe(HubConnectionState.Disconnected);
  });
});
