export const RoleNames = {
  Admin: 'Admin',
  User: 'User',
} as const;

export type RoleName = (typeof RoleNames)[keyof typeof RoleNames];

export interface User {
  id: string;
  email: string;
  roles: string[];
  createdAtUtc: string;
}
