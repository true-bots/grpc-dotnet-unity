using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BestHTTP;

namespace BestHTTP.Examples.HTTP
{
	public sealed class AssetBundleSample : Helpers.SampleBase
	{
#pragma warning disable 0649

		[Tooltip("The url of the resource to download")] [SerializeField]
		string _path = "/AssetBundles/WebGL/demobundle.assetbundle";

		[SerializeField] string _assetnameInBundle = "9443182_orig";

		[SerializeField] Text _statusText;

		[SerializeField] RawImage _rawImage;

		[SerializeField] Button _downloadButton;

#pragma warning restore

		#region Private Fields

		/// <summary>
		/// Reference to the request to be able to call Abort on it.
		/// </summary>
		HTTPRequest request;

		/// <summary>
		/// The downloaded and cached AssetBundle
		/// </summary>
		AssetBundle cachedBundle;

		#endregion

		#region Unity Events

		protected override void Start()
		{
			base.Start();

			_statusText.text = "Waiting for user interaction";
		}

		void OnDestroy()
		{
			if (request != null)
			{
				request.Abort();
			}

			request = null;

			UnloadBundle();
		}

		/// <summary>
		/// GUI button callback
		/// </summary>
		public void OnStartDownloadButton()
		{
			_downloadButton.enabled = false;
			UnloadBundle();

			StartCoroutine(DownloadAssetBundle());
		}

		#endregion

		#region Private Helper Functions

		IEnumerator DownloadAssetBundle()
		{
			// Create and send our request
			request = new HTTPRequest(new Uri(sampleSelector.BaseURL + _path)).Send();

			_statusText.text = "Download started";

			// Wait while it's finishes and add some fancy dots to display something while the user waits for it.
			// A simple "yield return StartCoroutine(request);" would do the job too.
			while (request.State < HTTPRequestStates.Finished)
			{
				yield return new WaitForSeconds(0.1f);

				_statusText.text += ".";
			}

			// Check the outcome of our request.
			switch (request.State)
			{
				// The request finished without any problem.
				case HTTPRequestStates.Finished:

					if (request.Response.IsSuccess)
					{
#if !BESTHTTP_DISABLE_CACHING
						if (request.Response.IsFromCache)
						{
							_statusText.text = "Loaded from local cache!";
						}
						else
						{
							_statusText.text = "Downloaded!";
						}
#else
                        this._statusText.text = "Downloaded!";
#endif

						// Start creating the downloaded asset bundle
						AssetBundleCreateRequest async =
#if UNITY_5_3_OR_NEWER
							AssetBundle.LoadFromMemoryAsync(request.Response.Data);
#else
                            AssetBundle.CreateFromMemory(request.Response.Data);
#endif

						// wait for it
						yield return async;

						PlatformSupport.Memory.BufferPool.Release(request.Response.Data);

						// And process the bundle
						yield return StartCoroutine(ProcessAssetBundle(async.assetBundle));
					}
					else
					{
						_statusText.text = string.Format("Request finished Successfully, but the server sent an error. Status Code: {0}-{1} Message: {2}",
							request.Response.StatusCode,
							request.Response.Message,
							request.Response.DataAsText);
						Debug.LogWarning(_statusText.text);
					}

					break;

				// The request finished with an unexpected error. The request's Exception property may contain more info about the error.
				case HTTPRequestStates.Error:
					_statusText.text = "Request Finished with Error! " +
					                   (request.Exception != null ? request.Exception.Message + "\n" + request.Exception.StackTrace : "No Exception");
					Debug.LogError(_statusText.text);
					break;

				// The request aborted, initiated by the user.
				case HTTPRequestStates.Aborted:
					_statusText.text = "Request Aborted!";
					Debug.LogWarning(_statusText.text);
					break;

				// Connecting to the server is timed out.
				case HTTPRequestStates.ConnectionTimedOut:
					_statusText.text = "Connection Timed Out!";
					Debug.LogError(_statusText.text);
					break;

				// The request didn't finished in the given time.
				case HTTPRequestStates.TimedOut:
					_statusText.text = "Processing the request Timed Out!";
					Debug.LogError(_statusText.text);
					break;
			}

			request = null;
			_downloadButton.enabled = true;
		}

		/// <summary>
		/// In this function we can do whatever we want with the freshly downloaded bundle.
		/// In this example we will cache it for later use, and we will load a texture from it.
		/// </summary>
		IEnumerator ProcessAssetBundle(AssetBundle bundle)
		{
			if (bundle == null)
			{
				yield break;
			}

			// Save the bundle for future use
			cachedBundle = bundle;

			// Start loading the asset from the bundle
			AssetBundleRequest asyncAsset =
#if UNITY_5_1 || UNITY_5_2 || UNITY_5_3_OR_NEWER
				cachedBundle.LoadAssetAsync(_assetnameInBundle, typeof(Texture2D));
#else
            cachedBundle.LoadAsync(this._assetnameInBundle, typeof(Texture2D));
#endif

			// wait til load
			yield return asyncAsset;

			// get the texture
			_rawImage.texture = asyncAsset.asset as Texture2D;
		}

		void UnloadBundle()
		{
			_rawImage.texture = null;

			if (cachedBundle != null)
			{
				cachedBundle.Unload(true);
				cachedBundle = null;
			}
		}

		#endregion
	}
}