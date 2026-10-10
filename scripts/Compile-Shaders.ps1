param([string]$OutputPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $projectRoot 'src/OledAurora/Shaders/NorthernLights.hlsl'
if (-not $OutputPath) { throw 'Specify -OutputPath for the compiled shader.' }

# D3DCompiler ships with Windows; no legacy DirectX SDK or fxc installation is needed.
if (-not ('AuroraShaderCompiler' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class AuroraShaderCompiler
{
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, UIntPtr length, string name, IntPtr defines, IntPtr include,
        string entry, string target, uint flags, uint flags2, out IntPtr code, out IntPtr errors);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetPointer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate UIntPtr GetSize(IntPtr self);
    private static byte[] Bytes(IntPtr blob)
    {
        var table = Marshal.ReadIntPtr(blob);
        var pointer = (GetPointer)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table, 3 * IntPtr.Size), typeof(GetPointer));
        var size = (GetSize)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(table, 4 * IntPtr.Size), typeof(GetSize));
        var bytes = new byte[checked((int)size(blob).ToUInt64())];
        Marshal.Copy(pointer(blob), bytes, 0, bytes.Length);
        return bytes;
    }
    public static byte[] Compile(string source, string name)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        IntPtr code, errors;
        // Strictness + optimization level 3. WPF accepts legacy ps_3_0 bytecode.
        int result = D3DCompile(bytes, (UIntPtr)bytes.Length, name, IntPtr.Zero, IntPtr.Zero,
            "main", "ps_3_0", (1u << 11) | (1u << 15), 0, out code, out errors);
        try
        {
            if (result < 0) throw new InvalidOperationException(errors == IntPtr.Zero
                ? "Shader compilation failed: " + result : Encoding.UTF8.GetString(Bytes(errors)));
            return Bytes(code);
        }
        finally
        {
            if (code != IntPtr.Zero) Marshal.Release(code);
            if (errors != IntPtr.Zero) Marshal.Release(errors);
        }
    }
}
'@
}
$bytecode = [AuroraShaderCompiler]::Compile([System.IO.File]::ReadAllText($sourcePath), 'NorthernLights.hlsl')
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($resolvedOutput)) | Out-Null
[System.IO.File]::WriteAllBytes($resolvedOutput, $bytecode)
Write-Output "Compiled NorthernLights.hlsl (ps_3_0, $($bytecode.Length) bytes)"
