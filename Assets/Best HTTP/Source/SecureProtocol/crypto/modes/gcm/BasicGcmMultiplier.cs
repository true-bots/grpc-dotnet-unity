#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Crypto.Modes.Gcm
{
	public class BasicGcmMultiplier
		: IGcmMultiplier
	{
		GcmUtilities.FieldElement H;

		public void Init(byte[] H)
		{
			GcmUtilities.AsFieldElement(H, out this.H);
		}

		public void MultiplyH(byte[] x)
		{
			GcmUtilities.AsFieldElement(x, out GcmUtilities.FieldElement T);
			GcmUtilities.Multiply(ref T, ref H);
			GcmUtilities.AsBytes(ref T, x);
		}
	}
}
#pragma warning restore
#endif