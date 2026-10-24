using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using AulaTutor.Application.Identity;
using AulaTutor.Domain.Identity;
using AulaTutor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AulaTutor.Infrastructure.Ai;

public sealed class PersonalAiOptions
{
    public string PublicOrigin { get; set; } = "https://localhost:4200";
    public string[] AllowedOrigins { get; set; } = ["https://localhost:4200"];
    public string? KeyPath { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificateKeyPath { get; set; }
    public bool Valid() => Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var u) && u.Scheme == "https" &&
        string.IsNullOrEmpty(u.UserInfo) && u.AbsolutePath == "/" && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) &&
        AllowedOrigins.Contains(PublicOrigin, StringComparer.Ordinal) && AllowedOrigins.Length is > 0 and <= 8;
}

// Independent ring and application purpose. No ephemeral/plaintext fallback.
public sealed class PersonalAiVault(IConfiguration config, IOptions<PersonalAiOptions> options)
{
    private readonly Lazy<IDataProtectionProvider?> protector = new(() => Create(config, options.Value));
    public bool Available => protector.Value is not null;
    private static IDataProtectionProvider? Create(IConfiguration config, PersonalAiOptions settings)
    {
        try
        {
            var cookiePath = config["DataProtection:KeyPath"];
            var keyPath = settings.KeyPath ?? (OperatingSystem.IsWindows() && cookiePath is not null ? Path.Combine(cookiePath, "personal-ai") : null);
            if (keyPath is null || !Path.IsPathFullyQualified(keyPath) || cookiePath is not null && Path.GetFullPath(keyPath) == Path.GetFullPath(cookiePath)) return null;
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(keyPath);
                return Verify(DataProtectionProvider.Create(new DirectoryInfo(keyPath), b => { b.SetApplicationName("AulaTutorIA.PersonalAI.v1"); if (OperatingSystem.IsWindows()) b.ProtectKeysWithDpapi(); }));
            }
            if (settings.CertificatePath is null || settings.CertificateKeyPath is null || !Directory.Exists(keyPath) ||
                !PrivateUnix(keyPath, true) || !PrivateUnix(settings.CertificateKeyPath, false)) return null;
            var certificate = X509Certificate2.CreateFromPemFile(settings.CertificatePath, settings.CertificateKeyPath);
            if (!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime() <= DateTime.UtcNow) return null;
            return Verify(DataProtectionProvider.Create(new DirectoryInfo(keyPath), b => b.SetApplicationName("AulaTutorIA.PersonalAI.v1").ProtectKeysWithCertificate(certificate)));
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException) { return null; }
    }
    private static IDataProtectionProvider Verify(IDataProtectionProvider provider)
    {
        var probe=provider.CreateProtector("custody-self-test");var value=Random();
        if(probe.Unprotect(probe.Protect(value))!=value)throw new CryptographicException();
        return provider;
    }
    private static bool PrivateUnix(string path, bool directory) => !OperatingSystem.IsWindows() &&
        (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) == 0 &&
        (!directory || (File.GetUnixFileMode(path) & UnixFileMode.UserExecute) != 0);
    public string Protect(string purpose, string value) => (protector.Value ?? throw Unavailable()).CreateProtector(purpose).Protect(value);
    public string ProtectTemporary(string purpose, string value, DateTimeOffset expires) => (protector.Value ?? throw Unavailable()).CreateProtector(purpose).ToTimeLimitedDataProtector().Protect(value, expires);
    public string UnprotectTemporary(string purpose, string value)
    {
        try { return (protector.Value ?? throw Unavailable()).CreateProtector(purpose).ToTimeLimitedDataProtector().Unprotect(value); }
        catch (CryptographicException) { throw new AppException(409, "La autorización venció. Vuelve a conectar OpenRouter."); }
    }
    public string Unprotect(string purpose, string value)
    {
        try { return (protector.Value ?? throw Unavailable()).CreateProtector(purpose).Unprotect(value); }
        catch (CryptographicException) { throw new AppException(409, "La credencial necesita volver a conectarse."); }
    }
    public static string CredentialPurpose(Guid owner, Guid id, long revision) => $"credential/v1/{owner:N}/{id:N}/{revision}";
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Random() => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    private static AppException Unavailable() => new(503, "La conexión personal requiere configurar la custodia de credenciales.");
}

public static class PersonalAiSession
{
    public const string Property = "AulaTutor.PersonalAI.Session";
    public const string BrowserCookie = "__Host-AulaTutor.AiAuthorization";
    public static async Task<string> IdAsync(HttpContext http)
    {
        var ticket = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!ticket.Succeeded || ticket.Properties is null || !ticket.Properties.Items.TryGetValue(Property, out var id) || string.IsNullOrEmpty(id))
            throw new AppException(409, "Vuelve a iniciar sesión para conectar tu proveedor.");
        return id;
    }
    public static async Task<Actor> RequireAsync(ICurrentActor current, AulaTutorDbContext db, CancellationToken ct)
    {
        var actor = await current.GetAsync();
        if (!actor.IsStudent && !actor.IsTeacher) throw new AppException(403, "Se requiere una cuenta docente o de estudiante.");
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == actor.Id && u.IsActive && u.OrganizationId == actor.OrganizationId &&
            db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId &&
                (actor.IsStudent && r.Name == RoleNames.Student || actor.IsTeacher && u.TeacherApproval == TeacherApproval.Approved && r.Name == RoleNames.Teacher))), ct))
            throw new AppException(403, "Tu cuenta ya no tiene acceso a esta operación.");
        return actor;
    }
    public static Task LockAsync(AulaTutorDbContext db, CancellationToken ct) => db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(8128001)", ct);
}
