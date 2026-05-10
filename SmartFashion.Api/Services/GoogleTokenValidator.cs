using Google.Apis.Auth;

namespace SmartFashion.Api.Services;

public class GoogleTokenValidator
{
    private readonly IConfiguration _config;

    public GoogleTokenValidator(IConfiguration config)
    {
        _config = config;
    }

    public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
    {
        var webClientId = _config["GoogleAuth:WebClientId"];
        if (string.IsNullOrWhiteSpace(webClientId))
            throw new InvalidOperationException("GoogleAuth:WebClientId is missing.");

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { webClientId }
        };

        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}