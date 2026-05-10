using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartFashion.Api.Data;
using SmartFashion.Api.Dtos;
using SmartFashion.Api.Models;
using SmartFashion.Api.Services;
using System.Net.Mail;

namespace SmartFashion.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly EmailService _email;
    private readonly GoogleTokenValidator _googleValidator;
    private readonly AppleTokenValidator _appleValidator;

    public AuthController(
        AppDbContext db,
        JwtTokenService jwt,
        EmailService email,
        GoogleTokenValidator googleValidator,
        AppleTokenValidator appleValidator)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _googleValidator = googleValidator;
        _appleValidator = appleValidator;
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new MailAddress(email); return true; }
        catch { return false; }
    }

    private static string GenerateOtp() => Random.Shared.Next(0, 1000000).ToString("D6");

    private static string OtpEmailBody(string code)
        => $"Your SmartFashion verification code is: {code}\nIt expires in 10 minutes.";

    private AuthResponse BuildAuthResponse(User user)
    {
        var token = _jwt.CreateToken(user);
        return new AuthResponse(user.Id, user.FullName, user.Email, token);
    }

    private async Task<AuthResponse> FindOrCreateSocialUserAsync(
        string provider,
        string providerUserId,
        string? email,
        string? fullName,
        bool providerVerifiedEmail)
    {
        var existingLink = await _db.UserAuthProviders
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ProviderUserId == providerUserId);

        if (existingLink != null)
            return BuildAuthResponse(existingLink.User);

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Provider did not return an email for first-time sign-in.");

        email = email.Trim().ToLower();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null)
        {
            user = new User
            {
                FullName = string.IsNullOrWhiteSpace(fullName) ? email.Split('@')[0] : fullName.Trim(),
                Email = email,
                PasswordHash = null,
                EmailVerified = providerVerifiedEmail
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else if (providerVerifiedEmail && !user.EmailVerified)
        {
            user.EmailVerified = true;
        }

        var link = new UserAuthProvider
        {
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = providerUserId
        };

        _db.UserAuthProviders.Add(link);
        await _db.SaveChangesAsync();

        return BuildAuthResponse(user);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var email = req.Email.Trim().ToLower();

        if (string.IsNullOrWhiteSpace(req.FullName)) return BadRequest("Full name required.");
        if (!IsValidEmail(email)) return BadRequest("Invalid email.");
        if (req.Password.Length < 6) return BadRequest("Password must be at least 6 characters.");

        var exists = await _db.Users.AnyAsync(u => u.Email == email);
        if (exists) return Conflict("Email already exists.");

        var code = GenerateOtp();

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(req.Password),
            EmailVerified = false,
            EmailVerifyCode = code,
            EmailVerifyExpiresUtc = DateTime.UtcNow.AddMinutes(10),
            EmailVerifyLastSentUtc = DateTime.UtcNow,
            EmailVerifySendCount = 1
        };

        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(user.Email, "SmartFashion verification code", OtpEmailBody(code));
        }
        catch
        {
            await tx.RollbackAsync();
            return StatusCode(500, "Could not send verification email. Please try again.");
        }

        await tx.CommitAsync();
        return Ok(new { message = "Registered. Verification code sent.", email = user.Email });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest req)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null) return NotFound("User not found.");
        if (user.EmailVerified) return Ok("Already verified.");

        if (user.EmailVerifyExpiresUtc is null || user.EmailVerifyExpiresUtc < DateTime.UtcNow)
            return BadRequest("Code expired.");

        if (user.EmailVerifyCode != req.Code)
            return BadRequest("Invalid code.");

        user.EmailVerified = true;
        user.EmailVerifyCode = null;
        user.EmailVerifyExpiresUtc = null;

        await _db.SaveChangesAsync();
        return Ok("Email verified.");
    }

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendCode(ResendCodeRequest req)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null) return NotFound("User not found.");
        if (user.EmailVerified) return Ok(new { message = "Already verified." });

        if (user.EmailVerifyLastSentUtc is not null &&
            (DateTime.UtcNow - user.EmailVerifyLastSentUtc.Value).TotalSeconds < 60)
        {
            return BadRequest("Please wait 60 seconds before resending.");
        }

        var code = GenerateOtp();
        user.EmailVerifyCode = code;
        user.EmailVerifyExpiresUtc = DateTime.UtcNow.AddMinutes(10);
        user.EmailVerifyLastSentUtc = DateTime.UtcNow;
        user.EmailVerifySendCount += 1;

        await _db.SaveChangesAsync();

        try
        {
            await _email.SendAsync(user.Email, "SmartFashion verification code", OtpEmailBody(code));
        }
        catch
        {
            return StatusCode(500, "Could not send verification email. Please try again.");
        }

        return Ok(new { message = "Code resent. Check your email." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user is null) return Unauthorized("Invalid email or password.");
        if (string.IsNullOrWhiteSpace(user.PasswordHash)) return Unauthorized("Use Google or Apple sign-in for this account.");
        if (!PasswordHasher.Verify(req.Password, user.PasswordHash)) return Unauthorized("Invalid email or password.");
        if (!user.EmailVerified) return Unauthorized("Please verify your email first.");

        return Ok(BuildAuthResponse(user));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleAuthRequest req)
    {
        var payload = await _googleValidator.ValidateAsync(req.IdToken);

        var result = await FindOrCreateSocialUserAsync(
            provider: "google",
            providerUserId: payload.Subject,
            email: payload.Email,
            fullName: payload.Name,
            providerVerifiedEmail: payload.EmailVerified
        );

        return Ok(result);
    }

    [HttpPost("apple")]
    public async Task<ActionResult<AuthResponse>> Apple(AppleAuthRequest req)
    {
        var identity = await _appleValidator.ValidateAsync(req.IdentityToken);

        var fullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName;
        var email = identity.Email ?? req.Email;

        var result = await FindOrCreateSocialUserAsync(
            provider: "apple",
            providerUserId: identity.UserId,
            email: email,
            fullName: fullName,
            providerVerifiedEmail: true
        );

        return Ok(result);
    }
    private static string ResetEmailBody(string code)
{
    return $"Your SmartFashion password reset code is: {code}\nIt expires in 10 minutes.";
}
    [HttpPost("forgot-password")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest req)
{
    var email = req.Email.Trim().ToLower();

    // Always return generic message for safety
    var genericResponse = Ok(new { message = "If that email exists, a reset code has been sent." });

    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (user is null)
        return genericResponse;

    // optional cooldown: 60 seconds
    if (user.PasswordResetLastSentUtc is not null &&
        (DateTime.UtcNow - user.PasswordResetLastSentUtc.Value).TotalSeconds < 60)
    {
        return BadRequest("Please wait 60 seconds before requesting another code.");
    }

    var code = GenerateOtp();

    user.PasswordResetCode = code;
    user.PasswordResetExpiresUtc = DateTime.UtcNow.AddMinutes(10);
    user.PasswordResetLastSentUtc = DateTime.UtcNow;
    user.PasswordResetSendCount += 1;

    await _db.SaveChangesAsync();

    try
    {
        await _email.SendAsync(
            user.Email,
            "SmartFashion password reset code",
            ResetEmailBody(code)
        );
    }
    catch
    {
        return StatusCode(500, "Could not send reset email. Please try again.");
    }

    return genericResponse;
}

[HttpPost("reset-password")]
public async Task<IActionResult> ResetPassword(ResetPasswordRequest req)
{
    var email = req.Email.Trim().ToLower();
    var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

    if (user is null)
        return BadRequest("Invalid email or code.");

    if (string.IsNullOrWhiteSpace(user.PasswordResetCode) ||
        user.PasswordResetExpiresUtc is null ||
        user.PasswordResetExpiresUtc < DateTime.UtcNow)
    {
        return BadRequest("Code expired or invalid.");
    }

    if (user.PasswordResetCode != req.Code)
        return BadRequest("Invalid code.");

    if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
        return BadRequest("New password must be at least 6 characters.");

    user.PasswordHash = PasswordHasher.Hash(req.NewPassword);
    user.PasswordResetCode = null;
    user.PasswordResetExpiresUtc = null;
    user.PasswordResetLastSentUtc = null;
    user.PasswordResetSendCount = 0;

    await _db.SaveChangesAsync();

    return Ok(new { message = "Password changed successfully." });
}
}