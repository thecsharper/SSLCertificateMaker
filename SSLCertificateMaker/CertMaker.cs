using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace SSLCertificateMaker
{
	public static class CertMaker
	{
		internal static SecureRandom secureRandom = new SecureRandom();

		private static AsymmetricCipherKeyPair GenerateRsaKeyPair(int length)
		{
			var keygenParam = new KeyGenerationParameters(secureRandom, length);

			var keyGenerator = new RsaKeyPairGenerator();
			keyGenerator.Init(keygenParam);
			
			return keyGenerator.GenerateKeyPair();
		}

		private static AsymmetricCipherKeyPair GenerateEcKeyPair(string curveName)
		{
			var ecParam = SecNamedCurves.GetByName(curveName);
			var ecDomain = new ECDomainParameters(ecParam.Curve, ecParam.G, ecParam.N);
			var keygenParam = new ECKeyGenerationParameters(ecDomain, secureRandom);

			var keyGenerator = new ECKeyPairGenerator();
			keyGenerator.Init(keygenParam);
			
			return keyGenerator.GenerateKeyPair();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="domains"></param>
		/// <param name="subjectPublic"></param>
		/// <param name="issuerName"></param>
		/// <param name="issuerPublic"></param>
		/// <param name="issuerPrivate"></param>
		/// <returns></returns>
		private static X509Certificate GenerateCertificate(MakeCertArgs args, AsymmetricKeyParameter subjectPublic, string issuerName, AsymmetricKeyParameter issuerPublic, AsymmetricKeyParameter issuerPrivate)
		{
			bool isCA = args.KeyUsage == (KeyUsage.CrlSign | KeyUsage.KeyCertSign);

			ISignatureFactory signatureFactory;
			if (issuerPrivate is ECPrivateKeyParameters)
			{
				signatureFactory = new Asn1SignatureFactory(
					X9ObjectIdentifiers.ECDsaWithSha256.ToString(),
					issuerPrivate);
			}
			else
			{
				signatureFactory = new Asn1SignatureFactory(
					PkcsObjectIdentifiers.Sha256WithRsaEncryption.ToString(),
					issuerPrivate);
			}

			var certGenerator = new X509V3CertificateGenerator();
			certGenerator.SetIssuerDN(new X509Name("CN=" + issuerName));
			certGenerator.SetSubjectDN(new X509Name("CN=" + args.domains[0]));
			certGenerator.SetSerialNumber(BigInteger.ProbablePrime(120, new Random()));
			certGenerator.SetNotBefore(args.validFrom);
			certGenerator.SetNotAfter(args.validTo);
			certGenerator.SetPublicKey(subjectPublic);

			if (issuerPublic != null)
			{
				var akis = new AuthorityKeyIdentifierStructure(issuerPublic);
				certGenerator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, akis);
			}

			// Subject Key Identifier
			var skis = new SubjectKeyIdentifierStructure(subjectPublic);
			certGenerator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, skis);

			if (!isCA || args.domains.Length > 1)
			{
				// Add SANs (Subject Alternative Names)
				var names = args.domains.Select(domain => new GeneralName(GeneralName.DnsName, domain)).ToArray();
				var subjectAltName = new GeneralNames(names);
				certGenerator.AddExtension(X509Extensions.SubjectAlternativeName, false, subjectAltName);
			}

			// Specify allowed key usage
			if (args.KeyUsage != 0)
			{
                certGenerator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(args.KeyUsage));
            }
				
			if (args.ExtendedKeyUsage.Length != 0)
			{
                certGenerator.AddExtension(X509Extensions.ExtendedKeyUsage, false, new ExtendedKeyUsage(args.ExtendedKeyUsage));
            }

			// Specify Basic Constraints
			certGenerator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(isCA));

			return certGenerator.Generate(signatureFactory);
		}

		private static bool ValidateCert(X509Certificate cert, ICipherParameters pubKey)
		{
			cert.CheckValidity(DateTime.UtcNow);
			var tbsCert = cert.GetTbsCertificate();
			var sig = cert.GetSignature();

			var signer = SignerUtilities.GetSigner(cert.SigAlgName);
			signer.Init(false, pubKey);
			signer.BlockUpdate(tbsCert, 0, tbsCert.Length);
			return signer.VerifySignature(sig);
		}

		private static bool IsSelfSigned(X509Certificate cert)
		{
			return ValidateCert(cert, cert.GetPublicKey());
		}

		//static IEnumerable<X509Certificate> BuildCertificateChain(X509Certificate primary, IEnumerable<X509Certificate> additional)
		//{
		//	PkixCertPathBuilder builder = new PkixCertPathBuilder();

		//	// Separate root from itermediate
		//	List<X509Certificate> intermediateCerts = new List<X509Certificate>();
		//	HashSet rootCerts = new HashSet();

		//	foreach (X509Certificate x509Cert in additional)
		//	{
		//		// Separate root and subordinate certificates
		//		if (IsSelfSigned(x509Cert))
		//			rootCerts.Add(new TrustAnchor(x509Cert, null));
		//		else
		//			intermediateCerts.Add(x509Cert);
		//	}

		//	// Create chain for this certificate
		//	X509CertStoreSelector holder = new X509CertStoreSelector();
		//	holder.Certificate = primary;

		//	// WITHOUT THIS LINE BUILDER CANNOT BEGIN BUILDING THE CHAIN
		//	intermediateCerts.Add(holder.Certificate);

		//	PkixBuilderParameters builderParams = new PkixBuilderParameters(rootCerts, holder);
		//	builderParams.IsRevocationEnabled = false;

		//	X509CollectionStoreParameters intermediateStoreParameters = new X509CollectionStoreParameters(intermediateCerts);

		//	builderParams.AddStore(X509StoreFactory.Create("Certificate/Collection", intermediateStoreParameters));

		//	PkixCertPathBuilderResult result = builder.Build(builderParams);

		//	return result.CertPath.Certificates.Cast<X509Certificate>();
		//}

		/// <summary>
		/// Generates a self-signed certificate.
		/// </summary>
		/// <param name="domains">An array of domain names or "subject" common names / alternative names.  If making a certificate authority, you could just let this be a single string, not even a domain name but something like "Do Not Trust This Root CA".</param>
		/// <param name="keySizeBits">Key size in bits for the RSA keys.</param>
		/// <param name="validFrom">Start date for certificate validity.</param>
		/// <param name="validTo">End date for certificate validity.</param>
		/// <returns></returns>
		public static CertificateBundle GetCertificateSignedBySelf(MakeCertArgs args)
		{
			var keys = GenerateRsaKeyPair(args.keyStrength);
			var cert = GenerateCertificate(args, keys.Public, args.domains[0], null, keys.Private);

			return new CertificateBundle(cert, keys.Private);
		}

		/// <summary>
		/// Generates a certificate signed by the specified certificate authority.
		/// </summary>
		/// <param name="domains">An array of domain names or subject common names / alternative names.</param>
		/// <param name="keySizeBits">Key size in bits for the RSA keys.</param>
		/// <param name="validFrom">Start date for certificate validity.</param>
		/// <param name="validTo">End date for certificate validity.</param>
		/// <param name="ca">A CertificateBundle representing the CA used to sign the new certificate.</param>
		/// <returns></returns>
		public static CertificateBundle GetCertificateSignedByCA(MakeCertArgs args, CertificateBundle ca)
		{
			var keys = GenerateRsaKeyPair(args.keyStrength);
			var cert = GenerateCertificate(args, keys.Public, ca.GetSubjectName(), ca.cert.GetPublicKey(), ca.privateKey);

			var certificateBundle = new CertificateBundle(cert, keys.Private);
            certificateBundle.SetIssuerBundle(ca);
			return certificateBundle;
		}

		///// <summary>
		///// Generates a certificate signed by the specified certificate authority.
		///// </summary>
		///// <param name="domains">An array of domain names or subject common names / alternative names.</param>
		///// <param name="keySizeBits">Key size in bits for the RSA keys.</param>
		///// <param name="validFrom">Start date for certificate validity.</param>
		///// <param name="validTo">End date for certificate validity.</param>
		///// <param name="caName">The name of the certificate authority, e.g. "Do Not Trust This Root CA".</param>
		///// <param name="caPublic">The public key of the certificate authority.  May be null if you don't have it handy.</param>
		///// <param name="caPrivate">The private key of the certificate authority.</param>
		///// <returns></returns>
		//public static CertificateBundle GetCertificateSignedByCA(string[] domains, int keySizeBits, DateTime validFrom, DateTime validTo, string caName, AsymmetricKeyParameter caPublic, AsymmetricKeyParameter caPrivate)
		//{
		//	AsymmetricCipherKeyPair keys = GenerateRsaKeyPair(keySizeBits);
		//	X509Certificate cert = GenerateCertificate(domains, keys.Public, validFrom, validTo, caName, caPublic, caPrivate, null);

		//	return new CertificateBundle(cert, keys.Private);
		//}
	}

	public class CertificateBundle
	{
		public X509Certificate cert;
		public AsymmetricKeyParameter privateKey;
		public X509Certificate[] chain = new X509Certificate[0];
		
		public CertificateBundle() { }
		
		public CertificateBundle(X509Certificate cert, AsymmetricKeyParameter privateKey)
		{
			this.cert = cert;
			this.privateKey = privateKey;
		}
		
		public string GetSubjectName()
		{
			if (cert == null)
			{
                return "Unknown";
            }
				
			var subject = cert.SubjectDN.ToString();
			if (subject.StartsWith("cn=", StringComparison.OrdinalIgnoreCase))
			{
                subject = subject.Substring(3);
            }
			
			return subject;
		}
		
		/// <summary>
		/// Gets the certificate and chain concatenated into a single .pem file (DER → Base64 → ASCII).  Each certificate is the issuer of the certificate before it.
		/// </summary>
		/// <returns></returns>
		public byte[] GetPublicCertAsCerFile()
		{
			using (var textWriter = new StringWriter())
			{
				var pemWriter = new PemWriter(textWriter);
				pemWriter.WriteObject(cert);
				
				foreach (X509Certificate link in chain)
				{
                    pemWriter.WriteObject(link);
                }
				
				pemWriter.Writer.Flush();
				var strKey = textWriter.ToString();
				return Encoding.ASCII.GetBytes(strKey);
			}
		}
		
		/// <summary>
		/// Gets the private key as a .pem file (DER → Base64 → ASCII).
		/// </summary>
		/// <returns></returns>
		public byte[] GetPrivateKeyAsKeyFile()
		{
			using (var textWriter = new StringWriter())
			{
				var pemWriter = new PemWriter(textWriter);
				pemWriter.WriteObject(privateKey);
				pemWriter.Writer.Flush();
				var strKey = textWriter.ToString();
				return Encoding.ASCII.GetBytes(strKey);
			}
		}
		
		/// <summary>
		/// Exports the certificate as a pfx file, optionally including the private key.
		/// </summary>
		/// <param name="password">If non-null, a password is required to use the resulting pfx file.</param>
		/// <returns></returns>
		public byte[] GetPfx(string password)
		{
			var subject = GetSubjectName();
			var pkcs12Store = new Pkcs12Store();
			var certEntry = new X509CertificateEntry(cert);
			var chainEntry = new X509CertificateEntry[] { certEntry }.Concat(chain.Select(c => new X509CertificateEntry(c))).ToArray();
			pkcs12Store.SetCertificateEntry(subject, certEntry);
			pkcs12Store.SetKeyEntry(subject, new AsymmetricKeyEntry(privateKey), chainEntry);
			
			using (var pfxStream = new MemoryStream())
			{
				pkcs12Store.Save(pfxStream, password?.ToCharArray(), CertMaker.secureRandom);
				return pfxStream.ToArray();
			}
		}
		
		/// <summary>
		/// Loads a CertificateBundle from .cer and .key files.
		/// </summary>
		/// <param name="publicCer">The path to the public .cer file.  If null or the file does not exist, the resulting CertificateBundle will have a null [cert] field.</param>
		/// <param name="privateKey">The path to the private .key file.  If null or the file does not exist, the resulting CertificateBundle will have a null [privateKey] field.</param>
		/// <returns></returns>
		public static CertificateBundle LoadFromCerAndKeyFiles(string publicCer, string privateKey)
		{
			var pemFilePaths = new string[] { publicCer, privateKey };
			AsymmetricKeyParameter key = null;
			var certs = new List<X509Certificate>();
			foreach (string path in pemFilePaths)
			{
				if (path != null && File.Exists(path))
				{
					using (var sr = new StreamReader(path, Encoding.ASCII))
					{
						var reader = new PemReader(sr);
						object obj = reader.ReadObject();
						while (obj != null)
						{
							if (obj is AsymmetricCipherKeyPair) 
							{ 
								key = ((AsymmetricCipherKeyPair)obj).Private;
                            }
                            else if (obj is X509Certificate) { 
								certs.Add((X509Certificate)obj);
                            }

                            obj = reader.ReadObject();
						}
					}
				}
			}

			if (key == null)
			{
                throw new ApplicationException("Private key was not found in input files \"" + string.Join("\", \"", pemFilePaths) + "\"");
            }

			var primary = certs.FirstOrDefault(c => DoesCertificateMatchKey(c, key));
			if (primary == null)
			{
                throw new ApplicationException("The public key matching the private key was not found in input files \"" + string.Join("\", \"", pemFilePaths) + "\"");
            }

            var fullchain = ChainBuilder.BuildChain(primary, certs.Where(c => c != primary));

            var b = new CertificateBundle
            {
                cert = fullchain[0],
                chain = fullchain.Skip(1).ToArray(),
                privateKey = key
            };

            return b;
		}

		/// <summary>
		/// Loads a CertificateBundle from a .pfx file.
		/// </summary>
		/// <param name="filePath">The path to the .pfx file.</param>
		/// <param name="password">The password required to access the .pfx file, or null.</param>
		/// <returns></returns>
		public static CertificateBundle LoadFromPfxFile(string filePath, string password)
		{
			try
			{
				using (var fileStream = File.OpenRead(filePath))
				{
					var pkcs12Store = new Pkcs12Store(fileStream, password == null ? null : password.ToCharArray());
					foreach (string alias in pkcs12Store.Aliases)
					{
						var certificateBundle = new CertificateBundle();
						var pfxChain = pkcs12Store.GetCertificateChain(alias).Select(e => e.Certificate).ToArray();
						
						certificateBundle.cert = pfxChain.First();
						certificateBundle.privateKey = pkcs12Store.GetKey(alias)?.Key;
						
						var fullchain = ChainBuilder.BuildChain(certificateBundle.cert, pfxChain.Skip(1));
						certificateBundle.chain = fullchain.Skip(1).ToArray();

						if (certificateBundle.cert != null && certificateBundle.privateKey != null)
						{
							return certificateBundle;
						}
					}

					return null;
				}
			}
			catch (IOException)
			{
				return null;
			}
		}

		/// <summary>
		/// Returns true of the certificate has the public key that matches the private key. Supports RSA, DSA, and EC keys.
		/// </summary>
		/// <param name="cert">Certificate with a public key</param>
		/// <param name="privKey">Private key</param>
		/// <returns></returns>
		private static bool DoesCertificateMatchKey(X509Certificate cert, AsymmetricKeyParameter privKey)
		{
			var pubKey = cert.GetPublicKey();
			if (pubKey is RsaKeyParameters parameters && privKey is RsaPrivateCrtKeyParameters parameters1)
			{
				var a = parameters;
				var b = parameters1;
				
				return a.Exponent.Equals(b.PublicExponent) && a.Modulus.Equals(b.Modulus);
			}
			else if (pubKey is DsaPublicKeyParameters && privKey is DsaPrivateKeyParameters)
			{
				var a = (DsaPublicKeyParameters)pubKey;
                var b = (DsaPrivateKeyParameters)privKey;
				
				return a.Y.Equals(b.Parameters.G.ModPow(b.X, b.Parameters.P));
			}
			else if (pubKey is ECPublicKeyParameters && privKey is ECPrivateKeyParameters)
			{
				var a = (ECPublicKeyParameters)pubKey;
				var b = (ECPrivateKeyParameters)privKey;
				
				return a.Q.Equals(b.Parameters.G.Multiply(b.D));
			}

			return false;
		}

		/// <summary>
		/// Sets the <see cref="chain"/> field to the issuer's certificate and chain.
		/// </summary>
		/// <param name="issuerBundle"></param>
		public void SetIssuerBundle(CertificateBundle issuerBundle)
		{
			chain = new X509Certificate[] { issuerBundle.cert }.Concat(issuerBundle.chain).ToArray();
		}
	}
}