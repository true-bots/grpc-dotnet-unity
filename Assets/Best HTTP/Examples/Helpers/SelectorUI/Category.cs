﻿using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.SelectorUI
{
	public sealed class Category : MonoBehaviour
	{
#pragma warning disable 0649

		[SerializeField] Text _text;

#pragma warning restore

		public void SetLabel(string category)
		{
			_text.text = category;
		}
	}
}