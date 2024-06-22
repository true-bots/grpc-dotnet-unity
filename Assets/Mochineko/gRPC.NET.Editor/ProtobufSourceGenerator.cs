#nullable enable
using UnityEditor;
using UnityEngine;

namespace Mochineko.gRPC.NET.Editor
{
	public sealed class ProtobufSourceGenerator : EditorWindow
	{
		[MenuItem("Window/Mochineko/gRPC.NET/Protobuf Source Generator")]
		public static void Open()
		{
			GetWindow<ProtobufSourceGenerator>("Protobuf Source Generator");
		}

		private string protocPath = string.Empty;
		private string pluginPath = string.Empty;
		private string outputRelativePath = "Assets/Generated";
		private string protoFileRelativePath = "Assets/Proto";

		private void OnGUI()
		{
			EditorGUILayout.LabelField("Generates C# source code from protocol buffer (.proto) file.");

			EditorGUILayout.Space();

			if (GUILayout.Button("Select Output Relative Path ..."))
			{
				var fullPath =
					EditorUtility.OpenFolderPanel("Select Output Directory", "Assets", string.Empty);
				outputRelativePath = fullPath.Replace(Application.dataPath, "Assets");
			}

			outputRelativePath = EditorGUILayout.TextField("Output Relative Path", outputRelativePath);

			EditorGUILayout.Space();

			if (GUILayout.Button("Select .proto File ..."))
			{
				var fullPath = EditorUtility.OpenFilePanel("Select .proto File", "Assets/", "proto");
				protoFileRelativePath = fullPath.Replace(Application.dataPath, "Assets");
			}

			protoFileRelativePath = EditorGUILayout.TextField(".proto File Relative Path", protoFileRelativePath);

			EditorGUILayout.Space();
			EditorGUILayout.Space();

			if (string.IsNullOrEmpty(GRPCSettings.ProtocPath)
			    || string.IsNullOrEmpty(GRPCSettings.GrpcCsharpPluginPath))
			{
				EditorGUILayout.HelpBox("Please set all paths in Preferences/gRPC.NET.", MessageType.Warning);
			}
			else if (string.IsNullOrEmpty(outputRelativePath)
			         || string.IsNullOrEmpty(protoFileRelativePath))
			{
				EditorGUILayout.HelpBox("Please set all paths in this windows.", MessageType.Warning);
			}
			else if (GUILayout.Button("Generate source code from .proto file..."))
			{
				Generate();
			}
		}

		private void Generate()
		{
			using var process = new System.Diagnostics.Process();
			var startInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = GRPCSettings.ProtocPath,
				Arguments = $"--csharp_out {outputRelativePath} " +
				            $"--grpc_out {outputRelativePath} " +
				            $"--plugin=protoc-gen-grpc={GRPCSettings.GrpcCsharpPluginPath} " +
				            $"{protoFileRelativePath}",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
			};

			process.StartInfo = startInfo;

			Debug.Log($"[gRPC.NET.Editor] Start process: {process.StartInfo.FileName} {process.StartInfo.Arguments}");

			process.Start();
			process.WaitForExit();

			if (process.ExitCode is 0)
			{
				Debug.Log($"[gRPC.NET.Editor] Process success with exit code: {process.ExitCode}, {process.StandardOutput.ReadToEnd()}");
			}
			else
			{
				Debug.LogError($"[gRPC.NET.Editor] Process failed with exit code: {process.ExitCode}, {process.StandardError.ReadToEnd()}");
			}
		}
	}
}