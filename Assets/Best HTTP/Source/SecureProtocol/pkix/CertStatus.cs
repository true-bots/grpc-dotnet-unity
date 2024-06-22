#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Pkix
{
	public class CertStatus
	{
		public const int Unrevoked = 11;

		public const int Undetermined = 12;

		int status = Unrevoked;

		DateTime? revocationDate = null;

		/// <summary>
		/// Returns the revocationDate.
		/// </summary>
		public DateTime? RevocationDate
		{
			get { return revocationDate; }
			set { revocationDate = value; }
		}

		/// <summary>
		/// Returns the certStatus.
		/// </summary>
		public int Status
		{
			get { return status; }
			set { status = value; }
		}
	}
}
#pragma warning restore
#endif