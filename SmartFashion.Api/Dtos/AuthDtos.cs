namespace SmartFashion.Api.Dtos;

public record RegisterRequest(string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);

public record VerifyEmailRequest(string Email, string Code);
public record ResendCodeRequest(string Email);

public record GoogleAuthRequest(string IdToken);
public record AppleAuthRequest(string IdentityToken, string User, string? Email, string? FullName);

public record AuthResponse(Guid UserId, string FullName, string Email, string Token);

public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Code, string NewPassword);