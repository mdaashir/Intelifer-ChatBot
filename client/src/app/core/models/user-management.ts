export interface CreateUserRequest {
  email: string;
  password: string;
  role: string;
}

export interface UpdateUserRequest {
  email: string;
  role: string;
}

export interface ResetPasswordRequest {
  newPassword: string;
}
