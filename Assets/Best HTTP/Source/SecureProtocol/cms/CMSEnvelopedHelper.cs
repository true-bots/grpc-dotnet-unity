#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using System.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.IO;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Security;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cms
{
	class CmsEnvelopedHelper
	{
		internal static readonly CmsEnvelopedHelper Instance = new CmsEnvelopedHelper();

		static readonly IDictionary<string, int> KeySizes = new Dictionary<string, int>();
		static readonly IDictionary<string, string> BaseCipherNames = new Dictionary<string, string>();

		static CmsEnvelopedHelper()
		{
			KeySizes.Add(CmsEnvelopedGenerator.DesEde3Cbc, 192);
			KeySizes.Add(CmsEnvelopedGenerator.Aes128Cbc, 128);
			KeySizes.Add(CmsEnvelopedGenerator.Aes192Cbc, 192);
			KeySizes.Add(CmsEnvelopedGenerator.Aes256Cbc, 256);

			BaseCipherNames.Add(CmsEnvelopedGenerator.DesEde3Cbc, "DESEDE");
			BaseCipherNames.Add(CmsEnvelopedGenerator.Aes128Cbc, "AES");
			BaseCipherNames.Add(CmsEnvelopedGenerator.Aes192Cbc, "AES");
			BaseCipherNames.Add(CmsEnvelopedGenerator.Aes256Cbc, "AES");
		}

		string GetAsymmetricEncryptionAlgName(
			string encryptionAlgOid)
		{
			if (Asn1.Pkcs.PkcsObjectIdentifiers.RsaEncryption.Id.Equals(encryptionAlgOid))
			{
				return "RSA/ECB/PKCS1Padding";
			}

			return encryptionAlgOid;
		}

		internal IBufferedCipher CreateAsymmetricCipher(
			string encryptionOid)
		{
			string asymName = GetAsymmetricEncryptionAlgName(encryptionOid);
			if (!asymName.Equals(encryptionOid))
			{
				try
				{
					return CipherUtilities.GetCipher(asymName);
				}
				catch (SecurityUtilityException)
				{
					// Ignore
				}
			}

			return CipherUtilities.GetCipher(encryptionOid);
		}

		internal IWrapper CreateWrapper(
			string encryptionOid)
		{
			try
			{
				return WrapperUtilities.GetWrapper(encryptionOid);
			}
			catch (SecurityUtilityException)
			{
				return WrapperUtilities.GetWrapper(GetAsymmetricEncryptionAlgName(encryptionOid));
			}
		}

		internal string GetRfc3211WrapperName(string oid)
		{
			if (oid == null)
			{
				throw new ArgumentNullException(nameof(oid));
			}

			if (!BaseCipherNames.TryGetValue(oid, out string alg))
			{
				throw new ArgumentException("no name for " + oid, nameof(oid));
			}

			return alg + "RFC3211Wrap";
		}

		internal int GetKeySize(string oid)
		{
			if (oid == null)
			{
				throw new ArgumentNullException(nameof(oid));
			}

			if (!KeySizes.TryGetValue(oid, out int keySize))
			{
				throw new ArgumentException("no keysize for " + oid, "oid");
			}

			return keySize;
		}

		internal static RecipientInformationStore BuildRecipientInformationStore(
			Asn1Set recipientInfos, CmsSecureReadable secureReadable)
		{
			List<RecipientInformation> infos = new List<RecipientInformation>();
			for (int i = 0; i != recipientInfos.Count; i++)
			{
				RecipientInfo info = RecipientInfo.GetInstance(recipientInfos[i]);

				ReadRecipientInfo(infos, info, secureReadable);
			}

			return new RecipientInformationStore(infos);
		}

		static void ReadRecipientInfo(IList<RecipientInformation> infos, RecipientInfo info,
			CmsSecureReadable secureReadable)
		{
			Asn1Encodable recipInfo = info.Info;
			if (recipInfo is KeyTransRecipientInfo keyTransRecipientInfo)
			{
				infos.Add(new KeyTransRecipientInformation(keyTransRecipientInfo, secureReadable));
			}
			else if (recipInfo is KekRecipientInfo kekRecipientInfo)
			{
				infos.Add(new KekRecipientInformation(kekRecipientInfo, secureReadable));
			}
			else if (recipInfo is KeyAgreeRecipientInfo keyAgreeRecipientInfo)
			{
				KeyAgreeRecipientInformation.ReadRecipientInfo(infos, keyAgreeRecipientInfo, secureReadable);
			}
			else if (recipInfo is PasswordRecipientInfo passwordRecipientInfo)
			{
				infos.Add(new PasswordRecipientInformation(passwordRecipientInfo, secureReadable));
			}
		}

		internal class CmsAuthenticatedSecureReadable : CmsSecureReadable
		{
			AlgorithmIdentifier algorithm;
			IMac mac;
			CmsReadable readable;

			internal CmsAuthenticatedSecureReadable(AlgorithmIdentifier algorithm, CmsReadable readable)
			{
				this.algorithm = algorithm;
				this.readable = readable;
			}

			public AlgorithmIdentifier Algorithm
			{
				get { return algorithm; }
			}

			public object CryptoObject
			{
				get { return mac; }
			}

			public CmsReadable GetReadable(KeyParameter sKey)
			{
				string macAlg = algorithm.Algorithm.Id;
//				Asn1Object sParams = this.algorithm.Parameters.ToAsn1Object();

				try
				{
					mac = MacUtilities.GetMac(macAlg);

					// FIXME Support for MAC algorithm parameters similar to cipher parameters
//						ASN1Object sParams = (ASN1Object)macAlg.getParameters();
//
//						if (sParams != null && !(sParams instanceof ASN1Null))
//						{
//							AlgorithmParameters params = CMSEnvelopedHelper.INSTANCE.createAlgorithmParameters(macAlg.getObjectId().getId(), provider);
//
//							params.init(sParams.getEncoded(), "ASN.1");
//
//							mac.init(sKey, params.getParameterSpec(IvParameterSpec.class));
//						}
//						else
					{
						mac.Init(sKey);
					}

//						Asn1Object asn1Params = asn1Enc == null ? null : asn1Enc.ToAsn1Object();
//
//						ICipherParameters cipherParameters = sKey;
//
//						if (asn1Params != null && !(asn1Params is Asn1Null))
//						{
//							cipherParameters = ParameterUtilities.GetCipherParameters(
//							macAlg.Algorithm, cipherParameters, asn1Params);
//						}
//						else
//						{
//							string alg = macAlg.Algorithm.Id;
//							if (alg.Equals(CmsEnvelopedDataGenerator.DesEde3Cbc)
//								|| alg.Equals(CmsEnvelopedDataGenerator.IdeaCbc)
//								|| alg.Equals(CmsEnvelopedDataGenerator.Cast5Cbc))
//							{
//								cipherParameters = new ParametersWithIV(cipherParameters, new byte[8]);
//							}
//						}
//
//						mac.Init(cipherParameters);
				}
				catch (SecurityUtilityException e)
				{
					throw new CmsException("couldn't create cipher.", e);
				}
				catch (InvalidKeyException e)
				{
					throw new CmsException("key invalid in message.", e);
				}
				catch (IOException e)
				{
					throw new CmsException("error decoding algorithm parameters.", e);
				}

				try
				{
					return new CmsProcessableInputStream(
						new TeeInputStream(
							readable.GetInputStream(),
							new MacSink(mac)));
				}
				catch (IOException e)
				{
					throw new CmsException("error reading content.", e);
				}
			}
		}

		internal class CmsEnvelopedSecureReadable : CmsSecureReadable
		{
			AlgorithmIdentifier algorithm;
			IBufferedCipher cipher;
			CmsReadable readable;

			internal CmsEnvelopedSecureReadable(AlgorithmIdentifier algorithm, CmsReadable readable)
			{
				this.algorithm = algorithm;
				this.readable = readable;
			}

			public AlgorithmIdentifier Algorithm
			{
				get { return algorithm; }
			}

			public object CryptoObject
			{
				get { return cipher; }
			}

			public CmsReadable GetReadable(KeyParameter sKey)
			{
				try
				{
					cipher = CipherUtilities.GetCipher(algorithm.Algorithm);

					Asn1Encodable asn1Enc = algorithm.Parameters;
					Asn1Object asn1Params = asn1Enc == null ? null : asn1Enc.ToAsn1Object();

					ICipherParameters cipherParameters = sKey;

					if (asn1Params != null && !(asn1Params is Asn1Null))
					{
						cipherParameters = ParameterUtilities.GetCipherParameters(
							algorithm.Algorithm, cipherParameters, asn1Params);
					}
					else
					{
						string alg = algorithm.Algorithm.Id;
						if (alg.Equals(CmsEnvelopedGenerator.DesEde3Cbc)
						    || alg.Equals(CmsEnvelopedGenerator.IdeaCbc)
						    || alg.Equals(CmsEnvelopedGenerator.Cast5Cbc))
						{
							cipherParameters = new ParametersWithIV(cipherParameters, new byte[8]);
						}
					}

					cipher.Init(false, cipherParameters);
				}
				catch (SecurityUtilityException e)
				{
					throw new CmsException("couldn't create cipher.", e);
				}
				catch (InvalidKeyException e)
				{
					throw new CmsException("key invalid in message.", e);
				}
				catch (IOException e)
				{
					throw new CmsException("error decoding algorithm parameters.", e);
				}

				try
				{
					return new CmsProcessableInputStream(
						new CipherStream(readable.GetInputStream(), cipher, null));
				}
				catch (IOException e)
				{
					throw new CmsException("error reading content.", e);
				}
			}
		}
	}
}
#pragma warning restore
#endif