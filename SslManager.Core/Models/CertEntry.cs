using System;

namespace SslManager.Core.Models;

public record CertEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Domain { get; init; } = string.Empty;
    public string[] SubjectAlternativeNames { get; init; } = Array.Empty<string>();
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; init; }
    public string PfxPath { get; init; } = string.Empty;
    public string CerPath { get; init; } = string.Empty;
    public bool IsTrusted { get; init; }
    public string KeyAlgorithm { get; init; } = "RSA"; // RSA | ECDSA

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsExpiringSoon => !IsExpired && DateTime.UtcNow.AddDays(30) > ExpiresAt;
    public int DaysRemaining => Math.Max(0, (ExpiresAt - DateTime.UtcNow).Days);
}
