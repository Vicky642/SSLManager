using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace SslManager.Core;

/// <summary>
/// Installs/removes the Root CA into the OS trust store.
/// Each platform has different mechanisms; we abstract them here.
/// </summary>
public class TrustManager
{
    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Installs a certificate into the trusted root store.</summary>
    /// <param name="cerPath">Path to the .cer (DER/PEM) file.</param>
    /// <param name="machineWide">If true, installs machine-wide (may require elevation).</param>
    public TrustResult Trust(string cerPath, bool machineWide = false)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return TrustWindows(cerPath, machineWide);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return TrustMacOs(cerPath, machineWide);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return TrustLinux(cerPath, machineWide);

            return TrustResult.Failure("Unsupported OS platform.");
        }
        catch (Exception ex)
        {
            return TrustResult.Failure(ex.Message);
        }
    }

    /// <summary>Removes a certificate from the trusted root store by thumbprint.</summary>
    public TrustResult Untrust(string thumbprint, bool machineWide = false)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return UntrustWindows(thumbprint, machineWide);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return UntrustMacOs(thumbprint);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return UntrustLinux(thumbprint);

            return TrustResult.Failure("Unsupported OS platform.");
        }
        catch (Exception ex)
        {
            return TrustResult.Failure(ex.Message);
        }
    }

    /// <summary>Checks whether a thumbprint is currently trusted in the root store.</summary>
    public bool IsTrusted(string thumbprint, bool machineWide = false)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var loc = machineWide ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
            using var store = new X509Store(StoreName.Root, loc);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).Count > 0;
        }
        // On macOS/Linux, attempt a quick X509Store check (best-effort)
        try
        {
            using var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false).Count > 0;
        }
        catch { return false; }
    }

    // ── Windows ──────────────────────────────────────────────────────────────

    private static TrustResult TrustWindows(string cerPath, bool machineWide)
    {
        var loc = machineWide ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        using var store = new X509Store(StoreName.Root, loc);
        store.Open(OpenFlags.ReadWrite);
        var cert = new X509Certificate2(cerPath);
        store.Add(cert);
        return TrustResult.Success($"Certificate added to {loc}\\Root store.");
    }

    private static TrustResult UntrustWindows(string thumbprint, bool machineWide)
    {
        var loc = machineWide ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        using var store = new X509Store(StoreName.Root, loc);
        store.Open(OpenFlags.ReadWrite);
        var certs = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, false);
        if (certs.Count == 0)
            return TrustResult.Failure("Certificate not found in store.");
        store.Remove(certs[0]);
        return TrustResult.Success("Certificate removed from store.");
    }

    // ── macOS ─────────────────────────────────────────────────────────────────

    private static TrustResult TrustMacOs(string cerPath, bool machineWide)
    {
        // User keychain: ~/Library/Keychains/login.keychain-db
        // System:        /Library/Keychains/System.keychain (requires sudo)
        var keychain = machineWide
            ? "/Library/Keychains/System.keychain"
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Keychains/login.keychain-db");

        var args = machineWide
            ? $"add-trusted-cert -d -r trustRoot -k \"{keychain}\" \"{cerPath}\""
            : $"add-trusted-cert -r trustRoot -k \"{keychain}\" \"{cerPath}\"";

        return RunProcess("security", args);
    }

    private static TrustResult UntrustMacOs(string thumbprint)
    {
        // Find the cert in keychain by thumbprint and remove it
        var args = $"delete-certificate -Z {thumbprint}";
        return RunProcess("security", args);
    }

    // ── Linux ─────────────────────────────────────────────────────────────────

    private static TrustResult TrustLinux(string cerPath, bool machineWide)
    {
        // Detect distro family
        if (IsDebianBased())
        {
            // /usr/local/share/ca-certificates/ for machine, ~/.local/share/ca-certificates/ for user
            var destDir = machineWide
                ? "/usr/local/share/ca-certificates"
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local/share/ca-certificates");
            var destPath = Path.Combine(destDir, "sslmgr-root.crt");
            Directory.CreateDirectory(destDir);
            File.Copy(cerPath, destPath, overwrite: true);
            var cmd = machineWide ? "update-ca-certificates" : $"update-ca-certificates --localdir \"{destDir}\"";
            return RunProcess("sh", $"-c \"{cmd}\"");
        }
        else if (IsRhelBased())
        {
            var destDir  = "/etc/pki/ca-trust/source/anchors";
            var destPath = Path.Combine(destDir, "sslmgr-root.crt");
            File.Copy(cerPath, destPath, overwrite: true);
            return RunProcess("update-ca-trust", "extract");
        }
        else if (IsArchBased())
        {
            var destPath = "/etc/ca-certificates/trust-source/anchors/sslmgr-root.crt";
            File.Copy(cerPath, destPath, overwrite: true);
            return RunProcess("trust", "extract-compat");
        }

        return TrustResult.Failure("Unknown Linux distro. Please install the certificate manually.");
    }

    private static TrustResult UntrustLinux(string thumbprint)
    {
        // Best-effort: remove the known file and update
        string[] paths = [
            "/usr/local/share/ca-certificates/sslmgr-root.crt",
            "/etc/pki/ca-trust/source/anchors/sslmgr-root.crt",
            "/etc/ca-certificates/trust-source/anchors/sslmgr-root.crt"
        ];
        foreach (var p in paths)
        {
            if (File.Exists(p))
            {
                File.Delete(p);
            }
        }
        if (IsDebianBased())  RunProcess("update-ca-certificates", "--fresh");
        else if (IsRhelBased()) RunProcess("update-ca-trust", "extract");
        else if (IsArchBased()) RunProcess("trust", "extract-compat");
        return TrustResult.Success("Certificate removed (if it was present).");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TrustResult RunProcess(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };
        using var proc = Process.Start(psi)!;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();

        return proc.ExitCode == 0
            ? TrustResult.Success(stdout.Trim())
            : TrustResult.Failure($"[exit {proc.ExitCode}] {stderr.Trim()}");
    }

    private static bool IsDebianBased()
        => File.Exists("/etc/debian_version") || File.Exists("/usr/bin/apt");

    private static bool IsRhelBased()
        => File.Exists("/etc/redhat-release") || File.Exists("/usr/bin/dnf");

    private static bool IsArchBased()
        => File.Exists("/etc/arch-release");
}

public record TrustResult(bool Succeeded, string Message)
{
    public static TrustResult Success(string message) => new(true, message);
    public static TrustResult Failure(string message) => new(false, message);
}
