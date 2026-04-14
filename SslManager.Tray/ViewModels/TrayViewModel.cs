using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SslManager.Core;
using SslManager.Core.Models;
using SslManager.Tray.Views;

namespace SslManager.Tray.ViewModels;

public partial class TrayViewModel : ObservableObject
{
    private readonly CertificateStore _store = new();

    [RelayCommand]
    private void ShowList()
    {
        var win = new CertListWindow { DataContext = new CertListViewModel(_store) };
        win.Show();
    }

    [RelayCommand]
    private void ShowCreate()
    {
        var win = new CreateCertWindow { DataContext = new CreateCertViewModel(_store) };
        win.Show();
    }

    [RelayCommand]
    private void RenewExpiring()
    {
        var store = new CertificateStore();
        var gen   = new CertificateGenerator();

        if (!store.RootCaExists()) return;

        var expiring = store.GetAll().Where(c => c.IsExpiringSoon && !c.IsExpired).ToList();
        foreach (var entry in expiring)
        {
            try
            {
                var rootCa = gen.LoadRootCa(store.RootCaPfxPath, "sslmgr-root-ca");
                var (cert, pfxBytes, cerBytes) = gen.CreateLeafCert(
                    rootCa, "sslmgr-root-ca",
                    entry.Domain, entry.SubjectAlternativeNames,
                    365, entry.KeyAlgorithm, "sslmgr-cert");

                System.IO.File.WriteAllBytes(entry.PfxPath, pfxBytes);
                System.IO.File.WriteAllBytes(entry.CerPath, cerBytes);

                store.Update(entry with
                {
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(365),
                    IsTrusted = false
                });

                cert.Dispose();
                rootCa.Dispose();
            }
            catch { /* silently skip failed renewals */ }
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var store = new CertificateStore();
        store.EnsureDirectories();
        var path = store.CertsDirectory;

        if (OperatingSystem.IsWindows())
            Process.Start("explorer.exe", path);
        else if (OperatingSystem.IsMacOS())
            Process.Start("open", path);
        else
            Process.Start("xdg-open", path);
    }

    [RelayCommand]
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime lifetime)
        {
            lifetime.Shutdown();
        }
    }
}
