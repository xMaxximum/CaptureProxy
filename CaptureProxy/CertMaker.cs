using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace CaptureProxy
{
    public class CertMaker
    {
        public static string CommonName { get; set; } = "CaptureProxy";

        private static X509Certificate2? _caCert;
        public static X509Certificate2 CaCert
        {
            get
            {
                if (_caCert == null)
                {
                    _caCert = CreateCaCertificate();
                }
                return _caCert;
            }
        }

        private static X509Certificate2 CreateCaCertificate()
        {
            using RSA rsa = RSA.Create(4096);
            CertificateRequest csr = new CertificateRequest(
                $"CN={CommonName}, O=DO_NOT_TRUST, OU=Created by CaptureProxy",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            );

            csr.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            csr.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(csr.PublicKey, false));

            X509Certificate2 cert = csr.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(1)
            );

            return cert;
        }

        public static byte[] GenerateSerialNumber()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                var serialNumber = new byte[16];
                rng.GetBytes(serialNumber);
                return serialNumber;
            }
        }

        public static X509Certificate2 CreateCertificate(X509Certificate2 caCert, string subjectName)
        {
            // Tạo thông tin chủ sở hữu chứng chỉ
            var distinguishedName = new X500DistinguishedName($"CN={subjectName}, O=DO_NOT_TRUST, OU=Created by CaptureProxy");

            // Tạo khóa RSA mới
            using var rsa = RSA.Create(2048);

            // Tạo yêu cầu ký chứng chỉ (CSR)
            var certificateRequest = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Thêm các extension cần thiết
            certificateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
            certificateRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            certificateRequest.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(certificateRequest.PublicKey, false));

            // Thêm extension cho SAN (Subject Alternative Name) cho domain
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(subjectName);
            certificateRequest.CertificateExtensions.Add(sanBuilder.Build());

            // Ký CSR bằng CA Cert
            var caPrivateKey = caCert.GetRSAPrivateKey();
            if (caPrivateKey == null) throw new Exception("Can not create private key with input CA cert.");
            var signedCertificate = certificateRequest.Create(caCert.SubjectName, X509SignatureGenerator.CreateForRSA(caPrivateKey, RSASignaturePadding.Pkcs1), DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1), GenerateSerialNumber());

            // Xuất chứng chỉ đã ký với khóa riêng
            var certificateWithPrivateKey = signedCertificate.CopyWithPrivateKey(rsa);
            var cert = new X509Certificate2(certificateWithPrivateKey.Export(X509ContentType.Pfx));
            return cert;
        }

        public static bool InstallCert(X509Certificate2 certificate)
        {
            bool successful = false;

            // Mở Trusted Root Certification Authorities Store
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            // Tìm chứng chỉ dựa trên thumbprint
            bool certificateExists = store.Certificates
                .Find(X509FindType.FindByThumbprint, certificate.Thumbprint, false)
                .Count > 0;

            if (!certificateExists)
            {
                // Thêm chứng chỉ vào store
                try
                {
                    store.Add(certificate);
                    successful = true;
                }
                catch (Exception ex)
                {
                    //Events.Log(ex);
                }
            }

            // Đóng store
            store.Close();

            return successful;
        }

        public static bool RemoveCert(X509Certificate2 certificate)
        {
            bool successful = false;

            // Mở Trusted Root Certification Authorities Store
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            // Xoá chứng chỉ từ store
            try
            {
                store.Remove(certificate);
                successful = true;
            }
            catch (Exception ex)
            {
                //Events.Log(ex);
            }

            // Đóng store
            store.Close();

            return successful;
        }

        public static bool RemoveCertByCommonName(string commonName)
        {
            bool successful = true;

            // Mở Trusted Root Certification Authorities Store
            var store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadWrite);

            // Xoá chứng chỉ từ store
            var certs = store.Certificates.Where(x => x.Subject.Contains($"CN={commonName}"));
            foreach (var cert in certs)
            {
                try
                {
                    store.Remove(cert);
                }
                catch (Exception ex)
                {
                    successful = false;
                    //Events.Log(ex);
                }
            }

            // Đóng store
            store.Close();

            return successful;
        }
    }
}
