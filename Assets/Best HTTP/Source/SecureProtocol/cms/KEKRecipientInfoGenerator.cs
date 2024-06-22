#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Kisa;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Nist;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ntt;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Pkcs;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cms
{
	class KekRecipientInfoGenerator : RecipientInfoGenerator
	{
		static readonly CmsEnvelopedHelper Helper = CmsEnvelopedHelper.Instance;

		KeyParameter keyEncryptionKey;

		// TODO Can get this from keyEncryptionKey?		
		string keyEncryptionKeyOID;
		KekIdentifier kekIdentifier;

		// Derived
		AlgorithmIdentifier keyEncryptionAlgorithm;

		internal KekRecipientInfoGenerator()
		{
		}

		internal KekIdentifier KekIdentifier
		{
			set { kekIdentifier = value; }
		}

		internal KeyParameter KeyEncryptionKey
		{
			set
			{
				keyEncryptionKey = value;
				keyEncryptionAlgorithm = DetermineKeyEncAlg(keyEncryptionKeyOID, keyEncryptionKey);
			}
		}

		internal string KeyEncryptionKeyOID
		{
			set { keyEncryptionKeyOID = value; }
		}

		public RecipientInfo Generate(KeyParameter contentEncryptionKey, SecureRandom random)
		{
			byte[] keyBytes = contentEncryptionKey.GetKey();

			IWrapper keyWrapper = Helper.CreateWrapper(keyEncryptionAlgorithm.Algorithm.Id);
			keyWrapper.Init(true, new ParametersWithRandom(keyEncryptionKey, random));
			Asn1OctetString encryptedKey = new DerOctetString(
				keyWrapper.Wrap(keyBytes, 0, keyBytes.Length));

			return new RecipientInfo(new KekRecipientInfo(kekIdentifier, keyEncryptionAlgorithm, encryptedKey));
		}

		static AlgorithmIdentifier DetermineKeyEncAlg(
			string algorithm, KeyParameter key)
		{
			if (Platform.StartsWith(algorithm, "DES"))
			{
				return new AlgorithmIdentifier(
					PkcsObjectIdentifiers.IdAlgCms3DesWrap,
					DerNull.Instance);
			}
			else if (Platform.StartsWith(algorithm, "RC2"))
			{
				return new AlgorithmIdentifier(
					PkcsObjectIdentifiers.IdAlgCmsRC2Wrap,
					new DerInteger(58));
			}
			else if (Platform.StartsWith(algorithm, "AES"))
			{
				int length = key.GetKey().Length * 8;
				DerObjectIdentifier wrapOid;

				if (length == 128)
				{
					wrapOid = NistObjectIdentifiers.IdAes128Wrap;
				}
				else if (length == 192)
				{
					wrapOid = NistObjectIdentifiers.IdAes192Wrap;
				}
				else if (length == 256)
				{
					wrapOid = NistObjectIdentifiers.IdAes256Wrap;
				}
				else
				{
					throw new ArgumentException("illegal keysize in AES");
				}

				return new AlgorithmIdentifier(wrapOid); // parameters absent
			}
			else if (Platform.StartsWith(algorithm, "SEED"))
			{
				// parameters absent
				return new AlgorithmIdentifier(KisaObjectIdentifiers.IdNpkiAppCmsSeedWrap);
			}
			else if (Platform.StartsWith(algorithm, "CAMELLIA"))
			{
				int length = key.GetKey().Length * 8;
				DerObjectIdentifier wrapOid;

				if (length == 128)
				{
					wrapOid = NttObjectIdentifiers.IdCamellia128Wrap;
				}
				else if (length == 192)
				{
					wrapOid = NttObjectIdentifiers.IdCamellia192Wrap;
				}
				else if (length == 256)
				{
					wrapOid = NttObjectIdentifiers.IdCamellia256Wrap;
				}
				else
				{
					throw new ArgumentException("illegal keysize in Camellia");
				}

				return new AlgorithmIdentifier(wrapOid); // parameters must be absent
			}
			else
			{
				throw new ArgumentException("unknown algorithm");
			}
		}
	}
}
#pragma warning restore
#endif