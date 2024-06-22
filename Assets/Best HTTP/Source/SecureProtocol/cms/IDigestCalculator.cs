#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cms
{
	interface IDigestCalculator
	{
		byte[] GetDigest();
	}
}
#pragma warning restore
#endif