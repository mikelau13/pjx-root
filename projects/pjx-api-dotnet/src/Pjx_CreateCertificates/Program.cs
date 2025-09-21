using CertificateManager;
using CertificateManager.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Pjx_CreateCertificates
{
    public enum AsymmetricAlgorithms : int
    {
        ECDSA = 0,
        RSA = 1
    }

    /// <summary>
    /// Creating selfs-signed certificate, refer to https://www.nuget.org/packages/CertificateManager/
    /// <br />Test cases: <see cref="Pjx_CreateCertificates.Tests.ProgramTests"/>
    /// </summary>
    public class Program
    {
        static CreateCertificates _cc;

        public static (string, string, AsymmetricAlgorithms) ReadArgs(string[] args)
        {
            if (args.Length == 1 && (args[0] == "-h" || args[0] == "-help"))
            {
                Console.WriteLine("arg 1 = dnsName");
                Console.WriteLine("arg 2 = password");
                Console.WriteLine("arg 3 (optional) = RSA|ECDSA");

                return (null, null, AsymmetricAlgorithms.ECDSA);
            }
            else if (args.Length < 2) { throw new InvalidOperationException("Invalid arguments");  }
            else
            {
                string dnsName = args[0];
                string password = args[1];
                AsymmetricAlgorithms algorithm = AsymmetricAlgorithms.ECDSA;

                if (args.Length == 3) 
                { 
                    switch (args[2].ToLower())
                    {
                        case "rsa": 
                            algorithm = AsymmetricAlgorithms.RSA;
                            break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(password) && !string.IsNullOrWhiteSpace(dnsName))
                {
                    return (password, dnsName, algorithm);
                } 
                else
                {
                    throw new InvalidOperationException("Invalid arguments");
                }
            }
        }

        static void Main(string[] args)
        {
            (string password, string dnsName, AsymmetricAlgorithms algorithm) = ReadArgs(args);

            var sp = new ServiceCollection()
               .AddCertificateManager()
               .BuildServiceProvider();

            _cc = sp.GetService<CreateCertificates>();

            var iec = sp.GetService<ImportExportCertificate>();

            if (algorithm == AsymmetricAlgorithms.RSA)
            {
                var rsaCert = CreateRsaCertificate(dnsName, 10);
                var rsaCertPfxBytes = iec.ExportSelfSignedCertificatePfx(password, rsaCert);
                File.WriteAllBytes(dnsName + ".cert_rsa512.pfx", rsaCertPfxBytes);
            }
            else
            {
                var ecdsaCert = CreateECDsaCertificate(dnsName, 10);
                var ecdsaCertPfxBytes = iec.ExportSelfSignedCertificatePfx(password, ecdsaCert);
                File.WriteAllBytes(dnsName + ".cert_ecdsa384.pfx", ecdsaCertPfxBytes);
            }

            Console.WriteLine("Self-signed certificates have been created and exported.");
        }

        /// <summary>
        /// RSA is a more popular choice.
        /// </summary>
        /// <param name="dnsName"></param>
        /// <param name="validityPeriodInYears"></param>
        /// <returns></returns>
        private static X509Certificate2 CreateRsaCertificate(string dnsName, int validityPeriodInYears)
        {
            var basicConstraints = new BasicConstraints
            {
                CertificateAuthority = false,
                HasPathLengthConstraint = false,
                PathLengthConstraint = 0,
                Critical = false
            };

            var subjectAlternativeName = new SubjectAlternativeName
            {
                DnsName = new List<string>
                {
                    dnsName,
                }
            };

            var x509KeyUsageFlags = X509KeyUsageFlags.DigitalSignature;

            // only if certification authentication is used
            var enhancedKeyUsages = new OidCollection
            {
                OidLookup.ClientAuthentication,
                OidLookup.ServerAuthentication 
                // OidLookup.CodeSigning,
                // OidLookup.SecureEmail,
                // OidLookup.TimeStamping  
            };

            var certificate = _cc.NewRsaSelfSignedCertificate(
                new DistinguishedName { CommonName = dnsName },
                basicConstraints,
                new ValidityPeriod
                {
                    ValidFrom = DateTimeOffset.UtcNow,
                    ValidTo = DateTimeOffset.UtcNow.AddYears(validityPeriodInYears)
                },
                subjectAlternativeName,
                enhancedKeyUsages,
                x509KeyUsageFlags,
                new RsaConfiguration
                { 
                    KeySize = 2048,
                    HashAlgorithmName = HashAlgorithmName.SHA512
                });

            return certificate;
        }

        /// <summary>
        /// ECDSA is a better choice, see https://sectigostore.com/blog/ecdsa-vs-rsa-everything-you-need-to-know/
        /// </summary>
        /// <param name="dnsName"></param>
        /// <param name="validityPeriodInYears"></param>
        /// <returns></returns>
        private static X509Certificate2 CreateECDsaCertificate(string dnsName, int validityPeriodInYears)
        {
            var basicConstraints = new BasicConstraints
            {
                CertificateAuthority = false,
                HasPathLengthConstraint = false,
                PathLengthConstraint = 0,
                Critical = false
            };

            var subjectAlternativeName = new SubjectAlternativeName
            {
                DnsName = new List<string>
                {
                    dnsName,
                }
            };

            var x509KeyUsageFlags = X509KeyUsageFlags.DigitalSignature;

            // only if certification authentication is used
            var enhancedKeyUsages = new OidCollection {
                OidLookup.ClientAuthentication,
                OidLookup.ServerAuthentication 
                // OidLookup.CodeSigning,
                // OidLookup.SecureEmail,
                // OidLookup.TimeStamping 
            };

            var certificate = _cc.NewECDsaSelfSignedCertificate(
                new DistinguishedName { CommonName = dnsName },
                basicConstraints,
                new ValidityPeriod
                {
                    ValidFrom = DateTimeOffset.UtcNow,
                    ValidTo = DateTimeOffset.UtcNow.AddYears(validityPeriodInYears)
                },
                subjectAlternativeName,
                enhancedKeyUsages,
                x509KeyUsageFlags,
                new ECDsaConfiguration
                {
                    KeySize = 384,
                    HashAlgorithmName = HashAlgorithmName.SHA384
                });

            return certificate;
        }
    }
}