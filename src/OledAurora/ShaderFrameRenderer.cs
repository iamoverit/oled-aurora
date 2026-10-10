using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace OledAurora;

// Test-only offscreen HAL rendering of the exact embedded bytecode. RenderTargetBitmap
// uses software rendering and silently ignores PS 3.0; it cannot validate this shader.
// This never presents a window, captures a desktop, or changes user settings.
internal sealed class ShaderFrameRenderer : IDisposable
{
    private readonly Forms.Form _window = new();
    private IntPtr _d3d, _device, _shader;

    internal ShaderFrameRenderer()
    {
        try
        {
            _d3d = Direct3DCreate9(32);
            if (_d3d == IntPtr.Zero) throw new InvalidOperationException("Direct3D9 unavailable.");
            var parameters = new PresentParameters { Width = 1, Height = 1, Format = 21, BufferCount = 1,
                SwapEffect = 1, Window = _window.Handle, Windowed = 1 };
            // HAL, hardware vertex processing, preserve FPU, never change foreground focus.
            Verify(Method<CreateDevice>(_d3d, 16)(_d3d, 0, 1, _window.Handle, 0x842, ref parameters, out _device));
            using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/OledAurora;component/Shaders/NorthernLights.ps"))!.Stream;
            var bytecode = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytecode);
            Verify(Method<CreateShader>(_device, 106)(_device, bytecode, out _shader));
            Verify(Method<PointerCall>(_device, 107)(_device, _shader));
            Verify(Method<ParameterCall>(_device, 89)(_device, 0x104)); // XYZRHW | TEX1
            foreach (var state in new[] { (7u, 0u), (27u, 0u), (22u, 1u), (137u, 0u) })
                Verify(Method<StateCall>(_device, 57)(_device, state.Item1, state.Item2));
        }
        catch { Dispose(); throw; }
    }

    internal BitmapSource Render(int width, int height, double seconds)
    {
        IntPtr target = IntPtr.Zero, readback = IntPtr.Zero;
        try
        {
            Verify(Method<CreateTarget>(_device, 28)(_device, (uint)width, (uint)height, 21, 0, 0, 0, out target, IntPtr.Zero));
            Verify(Method<CreatePlainSurface>(_device, 36)(_device, (uint)width, (uint)height, 21, 2, out readback, IntPtr.Zero));
            Verify(Method<SetTarget>(_device, 37)(_device, 0, target));
            float[] constants = [(float)seconds, 0, 0, 0, (float)width / height, 0, 0, 0];
            Verify(Method<SetConstants>(_device, 109)(_device, 0, constants, 2));
            // D3D9 pixel centers are integer coordinates. Cover every pixel exactly once.
            float[] vertices = [
                -.5f, -.5f, 0, 1, 0, 0,
                width - .5f, -.5f, 0, 1, 1, 0,
                -.5f, height - .5f, 0, 1, 0, 1,
                width - .5f, height - .5f, 0, 1, 1, 1];
            Verify(Method<SimpleCall>(_device, 41)(_device));
            int result = Method<Draw>(_device, 83)(_device, 5, 2, vertices, 24); // triangle strip
            Verify(Method<SimpleCall>(_device, 42)(_device));
            Verify(result);
            Verify(Method<CopyTarget>(_device, 32)(_device, target, readback));
            Verify(Method<LockSurface>(readback, 13)(readback, out var locked, IntPtr.Zero, 0x10));
            var pixels = new byte[width * height * 4];
            try
            {
                for (int y = 0; y < height; y++)
                    Marshal.Copy(IntPtr.Add(locked.Bits, y * locked.Pitch), pixels, y * width * 4, width * 4);
            }
            finally { Verify(Method<SimpleCall>(readback, 14)(readback)); }
            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Pbgra32, null, pixels, width * 4);
        }
        finally
        {
            if (readback != IntPtr.Zero) Marshal.Release(readback);
            if (target != IntPtr.Zero) Marshal.Release(target);
        }
    }

    public void Dispose()
    {
        if (_shader != IntPtr.Zero) { Marshal.Release(_shader); _shader = IntPtr.Zero; }
        if (_device != IntPtr.Zero) { Marshal.Release(_device); _device = IntPtr.Zero; }
        if (_d3d != IntPtr.Zero) { Marshal.Release(_d3d); _d3d = IntPtr.Zero; }
        _window.Dispose();
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));
    private static void Verify(int result) => Marshal.ThrowExceptionForHR(result);

    [DllImport("d3d9.dll", ExactSpelling = true)]
    private static extern IntPtr Direct3DCreate9(uint sdkVersion);
    [StructLayout(LayoutKind.Sequential)]
    private struct PresentParameters
    {
        public uint Width, Height, Format, BufferCount, Multisample, MultisampleQuality, SwapEffect;
        public IntPtr Window;
        public int Windowed, AutoDepth;
        public uint DepthFormat, Flags, RefreshRate, Interval;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct LockedRect { public int Pitch; public IntPtr Bits; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateDevice(IntPtr self, uint adapter, uint type, IntPtr window, uint flags, ref PresentParameters parameters, out IntPtr device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateShader(IntPtr self, byte[] code, out IntPtr shader);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTarget(IntPtr self, uint width, uint height, uint format, uint multisample, uint quality, int lockable, out IntPtr surface, IntPtr shared);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreatePlainSurface(IntPtr self, uint width, uint height, uint format, uint pool, out IntPtr surface, IntPtr shared);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetTarget(IntPtr self, uint index, IntPtr surface);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SimpleCall(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int PointerCall(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ParameterCall(IntPtr self, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int StateCall(IntPtr self, uint state, uint value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int SetConstants(IntPtr self, uint start, float[] values, uint count);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Draw(IntPtr self, uint primitive, uint count, float[] vertices, uint stride);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CopyTarget(IntPtr self, IntPtr source, IntPtr destination);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int LockSurface(IntPtr self, out LockedRect locked, IntPtr rectangle, uint flags);
}
