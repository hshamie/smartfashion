namespace SmartFashion.Api.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? PasswordHash { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool EmailVerified { get; set; } = false;
    public string? EmailVerifyCode { get; set; }
    public DateTime? EmailVerifyExpiresUtc { get; set; }
    public DateTime? EmailVerifyLastSentUtc { get; set; }
    public int EmailVerifySendCount { get; set; } = 0;

    public string? PasswordResetCode { get; set; }
    public DateTime? PasswordResetExpiresUtc { get; set; }
    public DateTime? PasswordResetLastSentUtc { get; set; }
    public int PasswordResetSendCount { get; set; } = 0;

    public ICollection<UserAuthProvider> AuthProviders { get; set; } = new List<UserAuthProvider>();
}