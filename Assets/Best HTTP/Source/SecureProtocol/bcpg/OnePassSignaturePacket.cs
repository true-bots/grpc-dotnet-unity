#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.IO;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Bcpg
{
	/// <remarks>Generic signature object</remarks>
	public class OnePassSignaturePacket
		: ContainedPacket
	{
		int version;
		int sigType;
		HashAlgorithmTag hashAlgorithm;
		PublicKeyAlgorithmTag keyAlgorithm;
		long keyId;
		int nested;

		internal OnePassSignaturePacket(
			BcpgInputStream bcpgIn)
		{
			version = bcpgIn.ReadByte();
			sigType = bcpgIn.ReadByte();
			hashAlgorithm = (HashAlgorithmTag)bcpgIn.ReadByte();
			keyAlgorithm = (PublicKeyAlgorithmTag)bcpgIn.ReadByte();

			keyId |= (long)bcpgIn.ReadByte() << 56;
			keyId |= (long)bcpgIn.ReadByte() << 48;
			keyId |= (long)bcpgIn.ReadByte() << 40;
			keyId |= (long)bcpgIn.ReadByte() << 32;
			keyId |= (long)bcpgIn.ReadByte() << 24;
			keyId |= (long)bcpgIn.ReadByte() << 16;
			keyId |= (long)bcpgIn.ReadByte() << 8;
			keyId |= (uint)bcpgIn.ReadByte();

			nested = bcpgIn.ReadByte();
		}

		public OnePassSignaturePacket(
			int sigType,
			HashAlgorithmTag hashAlgorithm,
			PublicKeyAlgorithmTag keyAlgorithm,
			long keyId,
			bool isNested)
		{
			version = 3;
			this.sigType = sigType;
			this.hashAlgorithm = hashAlgorithm;
			this.keyAlgorithm = keyAlgorithm;
			this.keyId = keyId;
			nested = isNested ? 0 : 1;
		}

		public int SignatureType
		{
			get { return sigType; }
		}

		/// <summary>The encryption algorithm tag.</summary>
		public PublicKeyAlgorithmTag KeyAlgorithm
		{
			get { return keyAlgorithm; }
		}

		/// <summary>The hash algorithm tag.</summary>
		public HashAlgorithmTag HashAlgorithm
		{
			get { return hashAlgorithm; }
		}

		public long KeyId
		{
			get { return keyId; }
		}

		public override void Encode(BcpgOutputStream bcpgOut)
		{
			MemoryStream bOut = new MemoryStream();
			using (BcpgOutputStream pOut = new BcpgOutputStream(bOut))
			{
				pOut.Write((byte)version, (byte)sigType, (byte)hashAlgorithm, (byte)keyAlgorithm);
				pOut.WriteLong(keyId);
				pOut.WriteByte((byte)nested);
			}

			bcpgOut.WritePacket(PacketTag.OnePassSignature, bOut.ToArray(), true);
		}
	}
}
#pragma warning restore
#endif