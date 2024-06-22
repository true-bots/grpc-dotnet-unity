using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BestHTTP.Examples.Helpers.SelectorUI
{
	public class SampleSelectorUI : MonoBehaviour
	{
#pragma warning disable 0649, 0169

		[SerializeField] Category _categoryListItemPrefab;

		[SerializeField] ExampleListItem _exampleListItemPrefab;

		[SerializeField] ExampleInfo _exampleInfoPrefab;

		[SerializeField] RectTransform _listRoot;

		[SerializeField] RectTransform _dyncamicContentRoot;

		SampleRoot sampleSelector;
		ExampleListItem selectedSample;
		GameObject dynamicContent;

#pragma warning restore

		void Start()
		{
			sampleSelector = FindObjectOfType<SampleRoot>();
			DisplayExamples();
		}

		void DisplayExamples()
		{
			// Sort examples by category
			sampleSelector.samples.Sort((a, b) =>
			{
				if (a == null || b == null)
				{
					return 0;
				}

				int result = a.Category.CompareTo(b.Category);
				if (result == 0)
				{
					result = a.DisplayName.CompareTo(b.DisplayName);
				}

				return result;
			});

			string currentCategory = null;

			for (int i = 0; i < sampleSelector.samples.Count; ++i)
			{
				SampleBase examplePrefab = sampleSelector.samples[i];

				if (examplePrefab == null)
				{
					continue;
				}

				if (examplePrefab.BannedPlatforms.Contains(Application.platform))
				{
					continue;
				}

				if (currentCategory != examplePrefab.Category)
				{
					Category category = Instantiate<Category>(_categoryListItemPrefab, _listRoot, false);
					category.SetLabel(examplePrefab.Category);

					currentCategory = examplePrefab.Category;
				}

				ExampleListItem listItem = Instantiate<ExampleListItem>(_exampleListItemPrefab, _listRoot, false);
				listItem.Setup(this, examplePrefab);

				if (sampleSelector.selectedExamplePrefab == null)
				{
					SelectSample(listItem);
				}
			}
		}

		public void SelectSample(ExampleListItem item)
		{
			sampleSelector.selectedExamplePrefab = item.ExamplePrefab;
			if (dynamicContent != null)
			{
				Destroy(dynamicContent);
			}

			ExampleInfo example = Instantiate<ExampleInfo>(_exampleInfoPrefab, _dyncamicContentRoot, false);
			example.Setup(this, item.ExamplePrefab);
			dynamicContent = example.gameObject;
		}

		public void ExecuteExample(SampleBase example)
		{
			if (dynamicContent != null)
			{
				Destroy(dynamicContent);
			}

			dynamicContent = Instantiate(example, _dyncamicContentRoot, false).gameObject;
		}
	}
}