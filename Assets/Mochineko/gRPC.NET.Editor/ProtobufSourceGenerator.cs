#nullable enable
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Mochineko.gRPC.NET.Editor
{
	public sealed class ProtobufSourceGenerator : EditorWindow
	{
		[MenuItem("Window/Mochineko/gRPC.NET/Protobuf Source Generator")]
		public static void Open()
		{
			GetWindow<ProtobufSourceGenerator>("Protobuf Source Generator");
		}

		string protocPath = string.Empty;
		string pluginPath = string.Empty;
		string outputRelativePath = "Assets/Generated";
		string protoFileRelativePath = "Assets/Proto";

		void OnGUI()
		{
			EditorGUILayout.LabelField("Generates C# source code from protocol buffer (.proto) file.");

			EditorGUILayout.Space();

			if (GUILayout.Button("Select Output Relative Path ..."))
			{
				string? fullPath =
					EditorUtility.OpenFolderPanel("Select Output Directory", "Assets", string.Empty);
				outputRelativePath = fullPath.Replace(Application.dataPath, "Assets");
			}

			outputRelativePath = EditorGUILayout.TextField("Output Relative Path", outputRelativePath);

			EditorGUILayout.Space();

			if (GUILayout.Button("Select .proto File ..."))
			{
				string? fullPath = EditorUtility.OpenFilePanel("Select .proto File", "Assets/", "proto");
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

		void Generate()
		{
			using Process? process = new System.Diagnostics.Process();
			ProcessStartInfo? startInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = GRPCSettings.ProtocPath,
				Arguments = $"--csharp_out {outputRelativePath} " +
				            $"--grpc_out {outputRelativePath} " +
				            $"--plugin=protoc-gen-grpc={GRPCSettings.GrpcCsharpPluginPath} " +
				            $"{protoFileRelativePath}",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true
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