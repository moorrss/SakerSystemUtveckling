namespace JensenOnline.Api.Core.Interfaces;

//Audit logging (T5). Genom interfacet kan loggningen bytas ut, t.ex. mot ett SIEM-system,
//utan att controllers behöver ändras
public interface IAuditService
{
    Task LogAsync(string action, bool success, string? details = null,
        string? userId = null, string? email = null);
}