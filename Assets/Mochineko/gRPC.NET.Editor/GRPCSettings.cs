#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Mochineko.gRPC.NET.Editor
{
    internal static class GRPCSettings
    {
        internal static string ProtocPath { get; private set; } = string.Empty;
        private const string protocPathKey = "Mochineko.gRPC.NET.Editor.ProtocPath";
        internal static string GrpcCsharpPluginPath { get; private set; } = string.Empty;
        private const string grpcCsharpPluginPathKey = "Mochineko.gRPC.NET.Editor.GrpcCsharpPluginPath";

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            ProtocPath = EditorPrefs.GetString(protocPathKey);
            GrpcCsharpPluginPath = EditorPrefs.GetString(grpcCsharpPluginPathKey);
            
            var provider = new SettingsProvider("Preferences/", SettingsScope.User)
            {
                label = "gRPC.NET",
                guiHandler = _ =>
                {
                    using (var changeScope = new EditorGUI.ChangeCheckScope())
                    {
                        if (GUILayout.Button("Select protoc.exe Path ..."))
                        {
                            ProtocPath = EditorUtility.OpenFilePanel("Select protoc", string.Empty, "exe");
                        }
                        ProtocPath = EditorGUILayout.TextField("protoc.exe Path", ProtocPath);
                        
                        EditorGUILayout.Space();
                        
                        if (GUILayout.Button("Select grpc_csharp_plugin.exe Path ..."))
                        {
                            GrpcCsharpPluginPath = EditorUtility.OpenFilePanel("Select gRPC C# Plugin", string.Empty, "exe");
                        }
                        GrpcCsharpPluginPath = EditorGUILayout.TextField("grpc_csharp_plugin.exe Path", GrpcCsharpPluginPath);

                        if (changeScope.changed)
                        {
                            EditorPrefs.SetString(protocPathKey, ProtocPath);
                            EditorPrefs.SetString(grpcCsharpPluginPathKey, GrpcCsharpPluginPath);
                        }
                    }
                },
                keywords = new HashSet<string>(new[] { "gRPC", "proto", "protoc", "grpc_csharp_plugin" })
            };

            return provider;
        }
    }
}