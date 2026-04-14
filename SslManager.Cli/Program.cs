using System;
using System.IO;
using System.Linq;
using Cocona;
using SslManager.Core;
using SslManager.Core.Models;
using Spectre.Console;

var builder = CoconaApp.CreateBuilder();
var app = builder.Build();

const string CaPassword = "sslmgr-root-ca";
const string CertPassword = "sslmgr-cert";

app.AddCommand("init", () =>
{
    var store = new CertificateStore();
    var gen = new CertificateGenerator();

    AnsiConsole.Write(new FigletText("SslManager").Color(Color.Aqua));
    AnsiConsole.WriteLine();

    store.EnsureDirectories();
    AnsiConsole.MarkupLine($"[green]✓[/] Data directory: [dim]{store.DataDirectory}[/]");

    if (store.RootCaExists())
    {
        AnsiConsole.MarkupLine("[yellow]⚠[/]  Root CA already exists — skipping creation.");
        AnsiConsole.MarkupLine($"    [dim]{store.RootCaPfxPath}[/]");
        return;
    }

    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("aqua"))
        .Start("Generating Root CA (RSA-4096, 10 years)...", ctx =>
        {
            var (_, pfxBytes, cerBytes) = gen.CreateRootCa(CaPassword);
            File.WriteAllBytes(store.RootCaPfxPath, pfxBytes);
            File.WriteAllBytes(store.RootCaCerPath, cerBytes);
        });

    AnsiConsole.MarkupLine("[green]✓[/] Root CA created:");
    AnsiConsole.MarkupLine($"    PFX → [dim]{store.RootCaPfxPath}[/]");
    AnsiConsole.MarkupLine($"    CER → [dim]{store.RootCaCerPath}[/]");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[grey]Tip: run [aqua]sslmgr trust --ca[/] to trust the Root CA system-wide.[/]");
}).WithDescription("Initialize SslManager: create data directories and root CA");

app.AddCommand("create", (
    [Argument(Description = "Primary domain (e.g. myapp.local)")] string domain,
    [Option('s', Description = "Additional Subject Alternative Names")] string[]? san,
    [Option('d', Description = "Validity period in days")] int days = 365,
    [Option('a', Description = "Key algorithm: RSA or ECDSA")] string algo = "RSA") =>
{
    var store = new CertificateStore();
    var gen = new CertificateGenerator();

    if (!store.RootCaExists())
    {
        AnsiConsole.MarkupLine("[red]✗[/] Root CA not found. Run [aqua]sslmgr init[/] first.");
        return;
    }

    if (store.GetByDomain(domain) is not null)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/]  Certificate for [bold]{domain}[/] already exists. Use [aqua]sslmgr renew[/] to replace it.");
        return;
    }

    if (!algo.Equals("RSA", StringComparison.OrdinalIgnoreCase) &&
        !algo.Equals("ECDSA", StringComparison.OrdinalIgnoreCase))
    {
        AnsiConsole.MarkupLine("[red]✗[/] Invalid algorithm. Use RSA or ECDSA.");
        return;
    }

    CertEntry? entry = null;

    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("aqua"))
        .Start($"Generating [bold]{algo}[/] certificate for [bold]{domain}[/]...", _ =>
        {
            var rootCa = gen.LoadRootCa(store.RootCaPfxPath, CaPassword);
            var pfxPath = store.GetPfxPath(domain);
            var cerPath = store.GetCerPath(domain);

            var (cert, pfxBytes, cerBytes) = gen.CreateLeafCert(
                rootCa, CaPassword, domain, san ?? Array.Empty<string>(), days, algo, CertPassword);

            File.WriteAllBytes(pfxPath, pfxBytes);
            File.WriteAllBytes(cerPath, cerBytes);

            entry = new CertEntry
            {
                Id = Guid.NewGuid(),
                Domain = domain,
                SubjectAlternativeNames = san ?? Array.Empty<string>(),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(days),
                PfxPath = pfxPath,
                CerPath = cerPath,
                IsTrusted = false,
                KeyAlgorithm = algo.ToUpperInvariant()
            };

            store.Add(entry);
            cert.Dispose();
            rootCa.Dispose();
        });

    AnsiConsole.MarkupLine($"[green]✓[/] Certificate created for [bold]{domain}[/]");
    AnsiConsole.MarkupLine($"    ID        : [aqua]{entry!.Id}[/]");
    AnsiConsole.MarkupLine($"    Algorithm : {entry.KeyAlgorithm}");
    AnsiConsole.MarkupLine($"    Expires   : {entry.ExpiresAt:yyyy-MM-dd} ({days} days)");
    AnsiConsole.MarkupLine($"    PFX       : [dim]{entry.PfxPath}[/]");
    AnsiConsole.MarkupLine($"    CER       : [dim]{entry.CerPath}[/]");
    AnsiConsole.MarkupLine("");
    AnsiConsole.MarkupLine("[grey]Tip: run [aqua]sslmgr trust " + domain + "[/] to add it to the OS trust store.[/]");
}).WithDescription("Create a new leaf TLS certificate signed by the local Root CA");

app.AddCommand("list", (
    [Option(Description = "Show only expired certificates")] bool expired = false,
    [Option(Description = "Show only trusted certificates")] bool trusted = false) =>
{
    var store = new CertificateStore();
    var certs = store.GetAll();

    if (expired) certs = certs.Where(c => c.IsExpired).ToList();
    if (trusted) certs = certs.Where(c => c.IsTrusted).ToList();

    if (certs.Count == 0)
    {
        AnsiConsole.MarkupLine("[grey]No certificates found. Run [aqua]sslmgr create <domain>[/] to create one.[/]");
        return;
    }

    var table = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Grey)
        .AddColumn(new TableColumn("[bold]ID[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Domain[/]").LeftAligned())
        .AddColumn(new TableColumn("[bold]Algorithm[/]").Centered())
        .AddColumn(new TableColumn("[bold]Expires[/]").Centered())
        .AddColumn(new TableColumn("[bold]Days Left[/]").Centered())
        .AddColumn(new TableColumn("[bold]Trusted[/]").Centered())
        .AddColumn(new TableColumn("[bold]Status[/]").Centered());

    foreach (var cert in certs.OrderBy(c => c.ExpiresAt))
    {
        var (statusText, statusColor) = cert switch
        {
            { IsExpired: true } => ("EXPIRED", "red"),
            { IsExpiringSoon: true } => ("EXPIRING", "yellow"),
            _ => ("OK", "green")
        };

        var trustedMark = cert.IsTrusted ? "[green]✓[/]" : "[grey]✗[/]";
        var idShort = cert.Id.ToString()[..8] + "…";
        var daysLeft = cert.IsExpired ? "[red]0[/]"
            : cert.IsExpiringSoon ? $"[yellow]{cert.DaysRemaining}[/]"
            : $"[green]{cert.DaysRemaining}[/]";

        table.AddRow(
            $"[dim]{idShort}[/]",
            $"[bold]{cert.Domain}[/]",
            cert.KeyAlgorithm,
            cert.ExpiresAt.ToString("yyyy-MM-dd"),
            daysLeft,
            trustedMark,
            $"[{statusColor}]{statusText}[/]"
        );
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"\n[grey]{certs.Count} certificate(s) total.[/]");
}).WithDescription("List all managed certificates");

app.AddCommand("trust", (
    [Argument(Description = "Certificate ID or domain to trust. Omit to trust the Root CA only.")] string? idOrDomain = null,
    [Option(Description = "Install machine-wide (may require elevation)")] bool machine = false,
    [Option(Description = "Trust the Root CA certificate only")] bool ca = false) =>
{
    var store = new CertificateStore();
    var trustMgr = new TrustManager();

    if (!store.RootCaExists())
    {
        AnsiConsole.MarkupLine("[red]✗[/] Root CA not found. Run [aqua]sslmgr init[/] first.");
        return;
    }

    void TrustFile(string label, string cerPath, TrustManager mgr, bool isMachine)
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("aqua"))
            .Start($"Trusting {label}...", _ =>
            {
                var result = mgr.Trust(cerPath, isMachine);
                if (result.Succeeded)
                    AnsiConsole.MarkupLine($"[green]✓[/] {label}: {result.Message}");
                else
                    AnsiConsole.MarkupLine($"[red]✗[/] {label}: {result.Message}");
            });
    }

    if (ca || idOrDomain is null)
    {
        TrustFile("Root CA", store.RootCaCerPath, trustMgr, machine);
        return;
    }

    var entry = store.FindByIdOrDomain(idOrDomain);
    if (entry is null)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Certificate not found: [bold]{idOrDomain}[/]");
        return;
    }

    TrustFile("Root CA", store.RootCaCerPath, trustMgr, machine);
    TrustFile(entry.Domain, entry.CerPath, trustMgr, machine);

    store.Update(entry with { IsTrusted = true });
    AnsiConsole.MarkupLine($"[green]✓[/] Registry updated — [bold]{entry.Domain}[/] marked as trusted.");
}).WithDescription("Trust a certificate (or the Root CA) in the OS trust store");

app.AddCommand("renew", (
    [Argument(Description = "Certificate ID or domain to renew")] string idOrDomain,
    [Option('d', Description = "New validity period in days")] int days = 365) =>
{
    var store = new CertificateStore();
    var gen = new CertificateGenerator();

    if (!store.RootCaExists())
    {
        AnsiConsole.MarkupLine("[red]✗[/] Root CA not found. Run [aqua]sslmgr init[/] first.");
        return;
    }

    var entry = store.FindByIdOrDomain(idOrDomain);
    if (entry is null)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Certificate not found: [bold]{idOrDomain}[/]");
        return;
    }

    CertEntry? renewed = null;

    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("aqua"))
        .Start($"Renewing [bold]{entry.Domain}[/]...", _ =>
        {
            if (File.Exists(entry.PfxPath)) File.Delete(entry.PfxPath);
            if (File.Exists(entry.CerPath)) File.Delete(entry.CerPath);

            var rootCa = gen.LoadRootCa(store.RootCaPfxPath, CaPassword);
            var (cert, pfxBytes, cerBytes) = gen.CreateLeafCert(
                rootCa, CaPassword,
                entry.Domain,
                entry.SubjectAlternativeNames,
                days,
                entry.KeyAlgorithm,
                CertPassword);

            File.WriteAllBytes(entry.PfxPath, pfxBytes);
            File.WriteAllBytes(entry.CerPath, cerBytes);

            renewed = entry with
            {
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(days),
                IsTrusted = false
            };

            store.Update(renewed);
            cert.Dispose();
            rootCa.Dispose();
        });

    AnsiConsole.MarkupLine($"[green]✓[/] Certificate renewed for [bold]{entry.Domain}[/]");
    AnsiConsole.MarkupLine($"    New expiry: {renewed!.ExpiresAt:yyyy-MM-dd} ({days} days)");
    AnsiConsole.MarkupLine("[grey]Note: re-run [aqua]sslmgr trust " + entry.Domain + "[/] if needed.[/]");
}).WithDescription("Renew an existing certificate (same domain & SANs, new validity)");

app.AddCommand("share", (
    [Argument(Description = "Certificate ID or domain to export")] string idOrDomain,
    [Option('f', Description = "Export format: pfx | cer | both")] string format = "both",
    [Option('o', Description = "Output directory")] string @out = ".") =>
{
    var store = new CertificateStore();
    var entry = store.FindByIdOrDomain(idOrDomain);

    if (entry is null)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] Certificate not found: [bold]{idOrDomain}[/]");
        return;
    }

    var outDir = Path.GetFullPath(@out);
    Directory.CreateDirectory(outDir);

    var copied = new System.Collections.Generic.List<string>();

    if (format is "pfx" or "both")
    {
        if (!File.Exists(entry.PfxPath)) { AnsiConsole.MarkupLine("[red]✗[/] PFX file missing."); }
        else
        {
            var dest = Path.Combine(outDir, Path.GetFileName(entry.PfxPath));
            File.Copy(entry.PfxPath, dest, overwrite: true);
            copied.Add($"[green]✓[/] PFX → [dim]{dest}[/]");
        }
    }

    if (format is "cer" or "both")
    {
        if (!File.Exists(entry.CerPath)) { AnsiConsole.MarkupLine("[red]✗[/] CER file missing."); }
        else
        {
            var dest = Path.Combine(outDir, Path.GetFileName(entry.CerPath));
            File.Copy(entry.CerPath, dest, overwrite: true);
            copied.Add($"[green]✓[/] CER → [dim]{dest}[/]");
        }
    }

    if (copied.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]✗[/] No files were exported. Check the format option.");
        return;
    }

    AnsiConsole.MarkupLine($"[green]✓[/] Exported [bold]{entry.Domain}[/] to [dim]{outDir}[/]:");
    foreach (var line in copied)
        AnsiConsole.MarkupLine("    " + line);
}).WithDescription("Export certificate files (PFX and/or CER) to a target directory");

app.Run();
