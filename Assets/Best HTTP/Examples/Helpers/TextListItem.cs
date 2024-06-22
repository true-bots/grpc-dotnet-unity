using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers
{
	public class TextListItem : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField] Text _text;
#pragma warning restore

		public void SetText(string text)
		{
			_text.text = text;
		}

		public void AddLeftPadding(int padding)
		{
			GetComponent<LayoutGroup>().padding.left += padding;
		}
	}
}