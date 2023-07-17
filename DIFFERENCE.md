# Difference

## How to import by Unity Package Manager

Add following dependencies to your `/Packages/manifest.json`.

```json
{
    "dependencies": {
        "com.mochineko.grpc-dotnet-unity": "https://github.com/mochi-neko/grpc-dotnet-unity.git?path=/Assets/GRPC.NET/Scripts#1.2.0",
        "com.mochineko.grpc-dotnet-unity.plugins": "https://github.com/mochi-neko/grpc-dotnet-unity.git?path=/Assets/GRPC.NET/Plugins/GRPC#1.2.0",
        "com.mochineko.grpc-dotnet-unity.editor": "https://github.com/mochi-neko/grpc-dotnet-unity.git?path=/Assets/Mochineko/gRPC.NET.Editor#1.2.0",
        ...
    }
}
```

## Change logs

Change logs from original repository is as follows.

- Add `package.json`s to support UPM.
- Add `.asmdef`s to refer from an assembly.
- Change `AutoReference = false` to avoid to conflict `.dll` reference in user project.
- Add editor window to generate C# source code from `.proto` file.
