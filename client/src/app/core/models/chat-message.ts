export const MAX_MESSAGE_LENGTH = 2000;

export type MessageSender = 'User' | 'Bot';

export interface ChatMessage {
  id: number;
  sessionId: string;
  content: string;
  sender: MessageSender;
  createdAtUtc: string;
}
