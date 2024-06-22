#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Nist;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Ntt;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Cms;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Operators;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Operators
{
	public class CmsContentEncryptorBuilder
	{
		static readonly IDictionary<DerObjectIdentifier, int> KeySizes =
			new Dictionary<DerObjectIdentifier, int>();

		static CmsContentEncryptorBuilder()
		{
			KeySizes[NistObjectIdentifiers.IdAes128Cbc] = 128;
			KeySizes[NistObjectIdentifiers.IdAes192Cbc] = 192;
			KeySizes[NistObjectIdentifiers.IdAes256Cbc] = 256;

			KeySizes[NttObjectIdentifiers.IdCamellia128Cbc] = 128;
			KeySizes[NttObjectIdentifiers.IdCamellia192Cbc] = 192;
			KeySizes[NttObjectIdentifiers.IdCamellia256Cbc] = 256;
		}

		static int GetKeySize(DerObjectIdentifier oid)
		{
			return KeySizes.TryGetValue(oid, out int keySize) ? keySize : -1;
		}

		readonly DerObjectIdentifier encryptionOID;
		readonly int keySize;

		readonly EnvelopedDataHelper helper = new EnvelopedDataHelper();
		//private SecureRandom random;

		public CmsContentEncryptorBuilder(DerObjectIdentifier encryptionOID)
			: this(encryptionOID, GetKeySize(encryptionOID))
		{
		}

		public CmsContentEncryptorBuilder(DerObjectIdentifier encryptionOID, int keySize)
		{
			this.encryptionOID = encryptionOID;
			this.keySize = keySize;
		}

		public ICipherBuilderWithKey Build()
		{
			//return new Asn1CipherBuilderWithKey(encryptionOID, keySize, random);
			return new Asn1CipherBuilderWithKey(encryptionOID, keySize, null);
		}
	}
}
#pragma warning restore
#endif