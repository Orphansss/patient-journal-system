using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientJournalSystem.Data;
using PatientJournalSystem.DTOs;
using PatientJournalSystem.Models;
using PatientJournalSystem.Services;

namespace PatientJournalSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("Authentication")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CurrentUserService _currentUser;

    public AuthController(
        AppDbContext db,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        CurrentUserService currentUser)
    {
        _db = db;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _currentUser = currentUser;
    }

    /// <summary>Redirect to Keycloak login using OpenID Connect authorization code flow</summary>
    [HttpGet("login")]
    public IActionResult Login()
    {
        var loginUrl =
            $"{KeycloakAuthority}/protocol/openid-connect/auth" +
            $"?client_id={Uri.EscapeDataString(ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(CallbackUrl)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString("openid profile email")}";

        return Redirect(loginUrl);
    }

    /// <summary>OpenID Connect callback that exchanges a Keycloak authorization code for tokens</summary>
    [HttpGet("callback")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription)
    {
        if (!string.IsNullOrWhiteSpace(error))
            return BadRequest(new { error, errorDescription });

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Keycloak callback mangler authorization code." });

        var tokenEndpoint = $"{KeycloakAuthority}/protocol/openid-connect/token";

        using var request = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = CallbackUrl,
            ["code"] = code
        });

        var client = _httpClientFactory.CreateClient();
        var response = await client.PostAsync(tokenEndpoint, request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return StatusCode((int)response.StatusCode, new
            {
                message = "Keycloak kunne ikke udveksle authorization code til tokens.",
                keycloakResponse = body
            });
        }

        using var document = JsonDocument.Parse(body);

        var root = document.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();

        var idToken = root.TryGetProperty("id_token", out var idTokenElement)
            ? idTokenElement.GetString()
            : null;

        return Ok(new
        {
            message = "Login lykkedes. Kopier access_token og brug den i Swagger Authorize som: Bearer {access_token}",
            access_token = accessToken,
            id_token = idToken,
            token_type = root.TryGetProperty("token_type", out var tokenType) ? tokenType.GetString() : "Bearer",
            expires_in = root.TryGetProperty("expires_in", out var expiresIn) ? expiresIn.GetInt32() : 0,
            id_token_claims = DecodeJwtPayload(idToken)
        });
    }

    /// <summary>Local JWT login is disabled because Keycloak now issues tokens</summary>
    [HttpPost("login")]
    [ProducesResponseType(400)]
    public IActionResult LocalLoginDisabled([FromBody] LoginRequest _)
    {
        return BadRequest(new
        {
            message = "Lokal JWT-login er fjernet. Brug Keycloak: GET /api/auth/login eller Swagger Authorize."
        });
    }

    /// <summary>Show the currently authenticated Keycloak user and matching local database user</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Me()
    {
        try
        {
            var localUser = await _currentUser.GetRequiredUserAsync(User);

            return Ok(new
            {
                localUser.Id,
                localUser.FullName,
                localUser.Email,
                LocalRole = localUser.Role.ToString(),
                KeycloakRole = _currentUser.GetCurrentApplicationRole(User),
                Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Register a matching local database user. The Keycloak user must also exist and have a role.</summary>
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
            return BadRequest(new { message = "Email already in use." });

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest(new { message = "Invalid role. Use: Patient, Doctor, Secretary, Admin." });

        // Ingen PasswordHash - credentials oprettes i Keycloak, ikke her.
        // Denne endpoint opretter kun den lokale bruger til RBAC og relationer.
        var user = new User
        {
            FullName = request.FullName,
            Email = request.Email,
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Me), routeValues: null, value: new
        {
            message = "Lokal bruger oprettet. Husk at oprette samme bruger i Keycloak med samme email og rolle.",
            userId = user.Id
        });
    }

    private string KeycloakAuthority => (_config["Keycloak:Authority"]
        ?? "http://localhost:8080/realms/patient-journal").TrimEnd('/');

    private string ClientId => _config["Keycloak:ClientId"]
        ?? "patient-journal-system";

    private string ClientSecret => _config["Keycloak:ClientSecret"]
        ?? "patient-journal-secret";

    private string CallbackUrl => _config["Keycloak:CallbackUrl"]
        ?? "http://localhost:5000/api/auth/callback";

    private static object? DecodeJwtPayload(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var parts = token.Split('.');

        if (parts.Length < 2)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}