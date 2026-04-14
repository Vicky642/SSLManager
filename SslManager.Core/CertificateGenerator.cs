using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SslManager.Core.Models;

namespace SslManager.Core;

public class CertificateGenerator
{
    private const string RootCaSubject = "CN=SslManager Root CA, O=SslManager, C=US";

    /// <summary>Creates a new self-signed Root CA certificate valid for 10 years.</summary>
    public (X509Certificate2 cert, byte[] pfxBytes, byte[] cerBytes) CreateRootCa(string password)
    {
        using var rsa = RSA.Create(4096);
        var req = new CertificateRequest(RootCaSubject, rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(
            certificateAuthority: true, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter  = notBefore.AddYears(10);

        using var cert = req.CreateSelfSigned(notBefore, notAfter);

        var pfxBytes = cert.Export(X509ContentType.Pfx, password);
        var cerBytes = cert.Export(X509ContentType.Cert);

        // Return with private key
        var finalCert = new X509Certificate2(pfxBytes, password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return (finalCert, pfxBytes, cerBytes);
    }

    /// <summary>Creates a leaf TLS certificate signed by the provided Root CA.</summary>
    public (X509Certificate2 cert, byte[] pfxBytes, byte[] cerBytes) CreateLeafCert(
        X509Certificate2 rootCa,
        string rootCaPassword,
        string domain,
        string[] sans,
        int validDays,
        string algorithm,
        string pfxPassword)
    {
        // Re-load CA with private key
        var caPfxBytes = rootCa.Export(X509ContentType.Pfx, rootCaPassword);
        using var caWithKey = new X509Certificate2(caPfxBytes, rootCaPassword,
            X509KeyStorageFlags.Exportable);

        CertificateRequest req;
        AsymmetricAlgorithm keyAlg;

        if (algorithm.Equals("ECDSA", StringComparison.OrdinalIgnoreCase))
        {
            var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            keyAlg = ecdsa;
            req = new CertificateRequest($"CN={domain}", ecdsa, HashAlgorithmName.SHA256);
        }
        else
        {
            var rsa = RSA.Create(2048);
            keyAlg = rsa;
            req = new CertificateRequest($"CN={domain}", rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false)); // TLS Server Auth

        // Build SAN
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        foreach (var san in sans)
        {
            if (!string.Equals(san, domain, StringComparison.OrdinalIgnoreCase))
                sanBuilder.AddDnsName(san);
        }
        // Always include localhost
        if (!domain.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            sanBuilder.AddDnsName("localhost");
        req.CertificateExtensions.Add(sanBuilder.Build());
        req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

        var notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
        var notAfter  = notBefore.AddDays(validDays);

        using var cert = req.Create(caWithKey, notBefore, notAfter, Guid.NewGuid().ToByteArray());

        // Combine with private key
        X509Certificate2 certWithKey;
        if (algorithm.Equals("ECDSA", StringComparison.OrdinalIgnoreCase))
        {
            certWithKey = cert.CopyWithPrivateKey((ECDsa)keyAlg);
        }
        else
        {
            certWithKey = cert.CopyWithPrivateKey((RSA)keyAlg);
        }

        var pfxBytes = certWithKey.Export(X509ContentType.Pfx, pfxPassword);
        var cerBytes = cert.Export(X509ContentType.Cert);

        certWithKey.Dispose();
        keyAlg.Dispose();

        var finalCert = new X509Certificate2(pfxBytes, pfxPassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        return (finalCert, pfxBytes, cerBytes);
    }

    /// <summary>Load an existing Root CA from a PFX file.</summary>
    public X509Certificate2 LoadRootCa(string pfxPath, string password)
    {
        return new X509Certificate2(pfxPath, password,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
    }
}
