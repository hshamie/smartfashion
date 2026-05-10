using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SmartFashion.Api.Services;

public class AppleIdentity
{
    public string UserId { get; set; } = "";
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
}

public class AppleTokenValidator
{
    private readonly IConfiguration _config;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public AppleTokenValidator(IConfiguration config)
    {
        _config = config;
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://appleid.apple.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever());
    }

    public async Task<AppleIdentity> ValidateAsync(string identityToken)
    {
        var configuration = await _configurationManager.GetConfigurationAsync(CancellationToken.None);

        var audiences = new List<string>();
        var bundleId = _config["AppleAuth:BundleId"];
        var serviceId = _config["AppleAuth:ServiceId"];

        if (!string.IsNullOrWhiteSpace(bundleId)) audiences.Add(bundleId);
        if (!string.IsNullOrWhiteSpace(serviceId)) audiences.Add(serviceId);

        if (audiences.Count == 0)
            throw new InvalidOperationException("AppleAuth:BundleId or AppleAuth:ServiceId is required.");

        var tokenHandler = new JwtSecurityTokenHandler();

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://appleid.apple.com",
            ValidateAudience = true,
            ValidAudiences = audiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var principal = tokenHandler.ValidateToken(identityToken, parameters, out _);

        string? Claim(string type) => principal.Claims.FirstOrDefault(c => c.Type == type)?.Value;

        return new AppleIdentity
        {
            UserId = Claim("sub") ?? "",
            Email = Claim(ClaimTypes.Email) ?? Claim("email"),
            EmailVerified =
                string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase) ||
                Claim("email_verified") == "1"
        };
    }
}