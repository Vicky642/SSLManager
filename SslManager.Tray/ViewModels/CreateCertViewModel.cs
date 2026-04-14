using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SslManager.Core;
using SslManager.Core.Models;

namespace SslManager.Tray.ViewModels;

public partial class CreateCertViewModel : ObservableObject
{
    private readonly CertificateStore _store;

    [ObservableProperty] private string _domain = string.Empty;
    [ObservableProperty] private string _sans   = string.Empty;
    [ObservableProperty] private int    _days   = 365;
    [ObservableProperty] private string _algorithm = "RSA";
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _isBusy;

    public CreateCertViewModel(CertificateStore store) => _store = store;

    [RelayCommand]
    private async System.Threading.Tasks.Task Create()
    {
        if (string.IsNullOrWhiteSpace(Domain))
        {
            StatusMessage = "Domain is required.";
            return;
        }

        if (_store.GetByDomain(Domain) is not null)
        {
            StatusMessage = $"Certificate for '{Domain}' already exists. Use Renew instead.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Generating certificate...";

        try
        {
            var sanList = Sans
                .Split(new[] { ',', ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            await System.Threading.Tasks.Task.Run(() =>
            {
                var gen    = new CertificateGenerator();
                var rootCa = gen.LoadRootCa(_store.RootCaPfxPath, "sslmgr-root-ca");
                var pfxPath = _store.GetPfxPath(Domain);
                var cerPath = _store.GetCerPath(Domain);

                var (cert, pfxBytes, cerBytes) = gen.CreateLeafCert(
                    rootCa, "sslmgr-root-ca", Domain, sanList, Days, Algorithm, "sslmgr-cert");

                File.WriteAllBytes(pfxPath, pfxBytes);
                File.WriteAllBytes(cerPath, cerBytes);

                _store.Add(new CertEntry
                {
                    Id                      = Guid.NewGuid(),
                    Domain                  = Domain,
                    SubjectAlternativeNames = sanList,
                    CreatedAt               = DateTime.UtcNow,
                    ExpiresAt               = DateTime.UtcNow.AddDays(Days),
                    PfxPath                 = pfxPath,
                    CerPath                 = cerPath,
                    IsTrusted               = false,
                    KeyAlgorithm            = Algorithm
                });

                cert.Dispose();
                rootCa.Dispose();
            });

            StatusMessage = $"✓ Certificate created for {Domain}";
            Domain = Sans = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
