using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.SelectorUI
{
	public sealed class ExampleInfo : MonoBehaviour
	{
#pragma warning disable 0649
		[SerializeField] Text _header;

		[SerializeField] Text _description;

#pragma warning restore

		SampleSelectorUI _parentUI;

		SampleBase _example;

		public void Setup(SampleSelectorUI parentUI, SampleBase example)
		{
			_parentUI = parentUI;
			_example = example;

			_header.text = _example.name;
			_description.text = _example.Description;
		}

		public void OnExecuteExample()
		{
			_parentUI.ExecuteExample(_example);
		}
	}
}