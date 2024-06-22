#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Math.EC.Multiplier
{
	/**
	 * Class holding precomputation data for fixed-point multiplications.
	 */
	public class FixedPointPreCompInfo
		: PreCompInfo
	{
		protected ECPoint m_offset = null;

		/**
		 * Lookup table for the precomputed <code>ECPoint</code>s used for a fixed point multiplication.
		 */
		protected ECLookupTable m_lookupTable = null;

		/**
		 * The width used for the precomputation. If a larger width precomputation
		 * is already available this may be larger than was requested, so calling
		 * code should refer to the actual width.
		 */
		protected int m_width = -1;

		public virtual ECLookupTable LookupTable
		{
			get { return m_lookupTable; }
			set { m_lookupTable = value; }
		}

		public virtual ECPoint Offset
		{
			get { return m_offset; }
			set { m_offset = value; }
		}

		public virtual int Width
		{
			get { return m_width; }
			set { m_width = value; }
		}
	}
}
#pragma warning restore
#endif