using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SslManager.Core;
using SslManager.Core.Models;

namespace SslManager.Tray.ViewModels;

public partial class CertListViewModel : ObservableObject
{
    private readonly CertificateStore _store;

    [ObservableProperty]
    private ObservableCollection<CertEntryDisplay> _certificates = new();

    public CertListViewModel(CertificateStore store)
    {
        _store = store;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        var items = _store.GetAll()
            .OrderBy(c => c.ExpiresAt)
            .Select(c => new CertEntryDisplay(c));
        Certificates = new ObservableCollection<CertEntryDisplay>(items);
    }

    [RelayCommand]
    private void Trust(CertEntryDisplay? item)
    {
        if (item is null) return;
        var trustMgr = new TrustManager();
        trustMgr.Trust(_store.RootCaCerPath);
        trustMgr.Trust(item.Entry.CerPath);
        _store.Update(item.Entry with { IsTrusted = true });
        Refresh();
    }

    [RelayCommand]
    private void Delete(CertEntryDisplay? item)
    {
        if (item is null) return;
        if (System.IO.File.Exists(item.Entry.PfxPath)) System.IO.File.Delete(item.Entry.PfxPath);
        if (System.IO.File.Exists(item.Entry.CerPath)) System.IO.File.Delete(item.Entry.CerPath);
        _store.Remove(item.Entry.Id);
        Refresh();
    }
}

public class CertEntryDisplay
{
    public CertEntry Entry { get; }
    public string Domain       => Entry.Domain;
    public string Algorithm    => Entry.KeyAlgorithm;
    public string ExpiresAt    => Entry.ExpiresAt.ToString("yyyy-MM-dd");
    public string DaysLeft     => Entry.IsExpired ? "Expired" : $"{Entry.DaysRemaining}d";
    public string StatusText   => Entry.IsExpired ? "EXPIRED" : Entry.IsExpiringSoon ? "EXPIRING" : "OK";
    public string TrustedText  => Entry.IsTrusted ? "✓" : "✗";
    public string IdShort      => Entry.Id.ToString()[..8];

    public CertEntryDisplay(CertEntry entry) => Entry = entry;
}
