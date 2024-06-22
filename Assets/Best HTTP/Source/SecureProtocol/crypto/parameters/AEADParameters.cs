#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Parameters
{
	public class AeadParameters
		: ICipherParameters
	{
		readonly byte[] associatedText;
		readonly byte[] nonce;
		readonly KeyParameter key;
		readonly int macSize;

		/**
		 * Base constructor.
		 *
		 * @param key key to be used by underlying cipher
		 * @param macSize macSize in bits
		 * @param nonce nonce to be used
		 */
		public AeadParameters(KeyParameter key, int macSize, byte[] nonce)
			: this(key, macSize, nonce, null)
		{
		}

		/**
		 * Base constructor.
		 *
		 * @param key key to be used by underlying cipher
		 * @param macSize macSize in bits
		 * @param nonce nonce to be used
		 * @param associatedText associated text, if any
		 */
		public AeadParameters(
			KeyParameter key,
			int macSize,
			byte[] nonce,
			byte[] associatedText)
		{
			this.key = key;
			this.nonce = nonce;
			this.macSize = macSize;
			this.associatedText = associatedText;
		}

		public virtual KeyParameter Key
		{
			get { return key; }
		}

		public virtual int MacSize
		{
			get { return macSize; }
		}

		public virtual byte[] GetAssociatedText()
		{
			return associatedText;
		}

		public virtual byte[] GetNonce()
		{
			return nonce;
		}
	}
}
#pragma warning restore
#endif