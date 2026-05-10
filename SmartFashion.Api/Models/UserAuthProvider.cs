namespace SmartFashion.Api.Models;

public class UserAuthProvider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    public string Provider { get; set; } = "";        // google | apple
    public string ProviderUserId { get; set; } = "";  // Google sub / Apple sub

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}