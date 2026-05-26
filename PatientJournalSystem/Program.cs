using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PatientJournalSystem.Data;
using PatientJournalSystem.Services;

var builder = WebApplication.CreateBuilder(args);

var keycloakAuthority = (builder.Configuration["Keycloak:Authority"]
    ?? "http://localhost:8080/realms/patient-journal").TrimEnd('/');
var keycloakClientId = builder.Configuration["Keycloak:ClientId"]
    ?? "patient-journal-system";

// Database - SQLite for simplicity
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddSingleton<EncryptionService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Authentication: Keycloak / OpenID Connect JWT bearer tokens
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.RequireHttpsMetadata = false;
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidateAudience = false,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var principal = context.Principal;

                if (principal?.Identity is not ClaimsIdentity identity)
                    return Task.CompletedTask;

                var authorizedParty = principal.FindFirst("azp")?.Value;

                if (!string.Equals(authorizedParty, keycloakClientId, StringComparison.Ordinal))
                {
                    context.Fail($"Tokenet kommer ikke fra den forventede Keycloak-client '{keycloakClientId}'.");
                    return Task.CompletedTask;
                }

                AddClaimIfMissing(identity, ClaimTypes.Email, principal.FindFirst("email")?.Value);
                AddClaimIfMissing(identity, ClaimTypes.Name, principal.FindFirst("name")?.Value
                    ?? principal.FindFirst("preferred_username")?.Value);

                foreach (var role in ReadKeycloakRoles(principal, keycloakClientId))
                    AddRoleClaimIfMissing(identity, role);

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();

// Swagger with Keycloak / OpenID Connect support
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Patient Journal System API",
        Version = "v1",
        Description = """
            Secure patient journal system demonstrating:
            - Keycloak / OpenID Connect authentication
            - Role-Based Access Control (Doctor, Patient, Secretary, Admin)
            - AES-256 encryption of journal data at rest
            - GDPR Article 9 consent management
            - NIS2 audit logging

            Use Swagger Authorize to log in through Keycloak, or open /api/auth/login in the browser and paste the returned access_token as: Bearer {token}
            """
    });

    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Description = "Login via Keycloak / OpenID Connect",
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{keycloakAuthority}/protocol/openid-connect/auth"),
                TokenUrl = new Uri($"{keycloakAuthority}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    ["openid"] = "OpenID Connect",
                    ["profile"] = "Profile",
                    ["email"] = "Email"
                }
            }
        }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste a Keycloak access token as: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "openid", "profile", "email" }
        }
    });

    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// Auto-create and seed the database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var encryption = scope.ServiceProvider.GetRequiredService<EncryptionService>();

    db.Database.EnsureCreated();
    DatabaseSeeder.Seed(db, encryption);
}

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Patient Journal API v1");
    c.RoutePrefix = string.Empty;
    c.DocumentTitle = "Patient Journal System";
    c.OAuthClientId(keycloakClientId);
    c.OAuthClientSecret(builder.Configuration["Keycloak:ClientSecret"] ?? string.Empty);
    c.OAuthUsePkce();
    c.OAuthScopeSeparator(" ");
});

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static IEnumerable<string> ReadKeycloakRoles(ClaimsPrincipal principal, string clientId)
{
    foreach (var role in ReadRolesFromJsonClaim(principal, "realm_access", "roles"))
        yield return role;

    var resourceAccess = principal.FindFirst("resource_access")?.Value;

    if (string.IsNullOrWhiteSpace(resourceAccess))
        yield break;

    using var document = JsonDocument.Parse(resourceAccess);

    if (document.RootElement.TryGetProperty(clientId, out var client) &&
        client.TryGetProperty("roles", out var roles) &&
        roles.ValueKind == JsonValueKind.Array)
    {
        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
                yield return role.GetString()!;
        }
    }
}

static IEnumerable<string> ReadRolesFromJsonClaim(
    ClaimsPrincipal principal,
    string claimType,
    string rolesProperty)
{
    var json = principal.FindFirst(claimType)?.Value;

    if (string.IsNullOrWhiteSpace(json))
        yield break;

    using var document = JsonDocument.Parse(json);

    if (!document.RootElement.TryGetProperty(rolesProperty, out var roles) ||
        roles.ValueKind != JsonValueKind.Array)
    {
        yield break;
    }

    foreach (var role in roles.EnumerateArray())
    {
        if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
            yield return role.GetString()!;
    }
}

static void AddClaimIfMissing(ClaimsIdentity identity, string claimType, string? value)
{
    if (!string.IsNullOrWhiteSpace(value) && !identity.HasClaim(claimType, value))
        identity.AddClaim(new Claim(claimType, value));
}

static void AddRoleClaimIfMissing(ClaimsIdentity identity, string role)
{
    if (!string.IsNullOrWhiteSpace(role) && !identity.HasClaim(ClaimTypes.Role, role))
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
}