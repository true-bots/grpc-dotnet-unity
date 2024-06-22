#if !BESTHTTP_DISABLE_ALTERNATE_SSL && (!UNITY_WEBGL || UNITY_EDITOR)
#pragma warning disable
using System;
using System.Collections.Generic;
using BestHTTP.SecureProtocol.Org.BouncyCastle.Utilities;

namespace BestHTTP.SecureProtocol.Org.BouncyCastle.Cms
{
	public class RecipientInformationStore
	{
		readonly IList<RecipientInformation> m_all;

		readonly IDictionary<RecipientID, IList<RecipientInformation>> m_table =
			new Dictionary<RecipientID, IList<RecipientInformation>>();

		public RecipientInformationStore(IEnumerable<RecipientInformation> recipientInfos)
		{
			foreach (RecipientInformation recipientInformation in recipientInfos)
			{
				RecipientID rid = recipientInformation.RecipientID;

				if (!m_table.TryGetValue(rid, out IList<RecipientInformation> list))
				{
					m_table[rid] = list = new List<RecipientInformation>(1);
				}

				list.Add(recipientInformation);
			}

			m_all = new List<RecipientInformation>(recipientInfos);
		}

		public RecipientInformation this[RecipientID selector]
		{
			get { return GetFirstRecipient(selector); }
		}

		/**
		* Return the first RecipientInformation object that matches the
		* passed in selector. Null if there are no matches.
		*
		* @param selector to identify a recipient
		* @return a single RecipientInformation object. Null if none matches.
		*/
		public RecipientInformation GetFirstRecipient(RecipientID selector)
		{
			if (!m_table.TryGetValue(selector, out IList<RecipientInformation> list))
			{
				return null;
			}

			return list[0];
		}

		/**
		* Return the number of recipients in the collection.
		*
		* @return number of recipients identified.
		*/
		public int Count
		{
			get { return m_all.Count; }
		}

		/**
		* Return all recipients in the collection
		*
		* @return a collection of recipients.
		*/
		public IList<RecipientInformation> GetRecipients()
		{
			return new List<RecipientInformation>(m_all);
		}

		/**
		* Return possible empty collection with recipients matching the passed in RecipientID
		*
		* @param selector a recipient id to select against.
		* @return a collection of RecipientInformation objects.
		*/
		public IList<RecipientInformation> GetRecipients(RecipientID selector)
		{
			if (!m_table.TryGetValue(selector, out IList<RecipientInformation> list))
			{
				return new List<RecipientInformation>(0);
			}

			return new List<RecipientInformation>(list);
		}
	}
}
#pragma warning restore
#endif