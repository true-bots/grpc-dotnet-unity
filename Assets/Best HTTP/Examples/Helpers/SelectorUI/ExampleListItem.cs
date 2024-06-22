using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.SelectorUI
{
	public sealed class ExampleListItem : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField] Text _text;
#pragma warning restore

		public SampleSelectorUI ParentUI { get; private set; }

		public SampleBase ExamplePrefab { get; private set; }

		public void Setup(SampleSelectorUI parentUI, SampleBase prefab)
		{
			ParentUI = parentUI;
			ExamplePrefab = prefab;

			_text.text = prefab.DisplayName;
		}

		public void OnButton()
		{
			ParentUI.SelectSample(this);
		}
	}
}