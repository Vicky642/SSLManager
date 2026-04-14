using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SslManager.Core.Models;

namespace SslManager.Core;

public class CertificateStore
{
    private readonly string _dataDir;
    private readonly string _certsDir;
    private readonly string _registryPath;
    private readonly string _rootCaPath;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public CertificateStore()
    {
        _dataDir     = GetDataDirectory();
        _certsDir    = Path.Combine(_dataDir, "certs");
        _registryPath = Path.Combine(_dataDir, "certs.json");
        _rootCaPath  = Path.Combine(_dataDir, "rootca");
    }

    public string DataDirectory  => _dataDir;
    public string CertsDirectory => _certsDir;
    public string RootCaDirectory => _rootCaPath;

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(_certsDir);
        Directory.CreateDirectory(_rootCaPath);
    }

    // ── Registry CRUD ────────────────────────────────────────────────────────

    public List<CertEntry> GetAll()
    {
        if (!File.Exists(_registryPath))
            return new List<CertEntry>();
        var json = File.ReadAllText(_registryPath);
        return JsonSerializer.Deserialize<List<CertEntry>>(json, JsonOpts) ?? new List<CertEntry>();
    }

    public CertEntry? GetById(Guid id)
        => GetAll().FirstOrDefault(c => c.Id == id);

    public CertEntry? GetByDomain(string domain)
        => GetAll().FirstOrDefault(c =>
            c.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));

    public CertEntry? FindByIdOrDomain(string idOrDomain)
    {
        if (Guid.TryParse(idOrDomain, out var id))
            return GetById(id);
        return GetByDomain(idOrDomain);
    }

    public void Add(CertEntry entry)
    {
        var list = GetAll();
        list.Add(entry);
        Save(list);
    }

    public bool Remove(Guid id)
    {
        var list = GetAll();
        var existing = list.FirstOrDefault(c => c.Id == id);
        if (existing is null) return false;
        list.Remove(existing);
        Save(list);
        return true;
    }

    public bool Update(CertEntry entry)
    {
        var list = GetAll();
        var idx  = list.FindIndex(c => c.Id == entry.Id);
        if (idx < 0) return false;
        list[idx] = entry;
        Save(list);
        return true;
    }

    // ── Root CA helpers ──────────────────────────────────────────────────────

    public string RootCaPfxPath => Path.Combine(_rootCaPath, "rootca.pfx");
    public string RootCaCerPath => Path.Combine(_rootCaPath, "rootca.cer");

    public bool RootCaExists()
        => File.Exists(RootCaPfxPath) && File.Exists(RootCaCerPath);

    // ── Cert file path helpers ───────────────────────────────────────────────

    public string GetPfxPath(string domain) =>
        Path.Combine(_certsDir, $"{Sanitize(domain)}.pfx");

    public string GetCerPath(string domain) =>
        Path.Combine(_certsDir, $"{Sanitize(domain)}.cer");

    // ── Private ──────────────────────────────────────────────────────────────

    private void Save(List<CertEntry> list)
    {
        var json = JsonSerializer.Serialize(list, JsonOpts);
        File.WriteAllText(_registryPath, json);
    }

    private static string Sanitize(string domain)
        => string.Concat(domain.Split(Path.GetInvalidFileNameChars())).Replace("*", "wildcard");

    private static string GetDataDirectory()
    {
        // Windows: %APPDATA%\SslManager
        // macOS:   ~/Library/Application Support/SslManager
        // Linux:   ~/.config/SslManager
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "SslManager");
    }
}
