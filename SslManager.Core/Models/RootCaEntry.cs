using System;

namespace SslManager.Core.Models;

public record RootCaEntry
{
    public string PfxPath { get; init; } = string.Empty;
    public string CerPath { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; }
    public string Thumbprint { get; init; } = string.Empty;
}
