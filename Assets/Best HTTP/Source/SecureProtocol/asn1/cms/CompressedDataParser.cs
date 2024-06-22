#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.X509;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Asn1.Cms
{
	/**
	* RFC 3274 - CMS Compressed Data.
	* <pre>
	* CompressedData ::= SEQUENCE {
	*  version CMSVersion,
	*  compressionAlgorithm CompressionAlgorithmIdentifier,
	*  encapContentInfo EncapsulatedContentInfo
	* }
	* </pre>
	*/
	public class CompressedDataParser
	{
		DerInteger _version;
		AlgorithmIdentifier _compressionAlgorithm;
		ContentInfoParser _encapContentInfo;

		public CompressedDataParser(
			Asn1SequenceParser seq)
		{
			_version = (DerInteger)seq.ReadObject();
			_compressionAlgorithm = AlgorithmIdentifier.GetInstance(seq.ReadObject().ToAsn1Object());
			_encapContentInfo = new ContentInfoParser((Asn1SequenceParser)seq.ReadObject());
		}

		public DerInteger Version
		{
			get { return _version; }
		}

		public AlgorithmIdentifier CompressionAlgorithmIdentifier
		{
			get { return _compressionAlgorithm; }
		}

		public ContentInfoParser GetEncapContentInfo()
		{
			return _encapContentInfo;
		}
	}
}
#pragma warning restore
#endif