using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace OledAurora;

internal sealed class NorthernLightsEffect : ShaderEffect
{
    // PS 3.0 deliberately uses the GPU. WPF cannot execute it in its software renderer.
    internal static bool IsSupported => RenderOptions.ProcessRenderMode != System.Windows.Interop.RenderMode.SoftwareOnly
        && (RenderCapability.Tier >> 16) > 0 && RenderCapability.IsPixelShaderVersionSupported(3, 0);

    private static readonly PixelShader Shader = LoadShader();
    public static readonly DependencyProperty TimeProperty = DependencyProperty.Register(nameof(Time), typeof(double),
        typeof(NorthernLightsEffect), new UIPropertyMetadata(0.0, PixelShaderConstantCallback(0)));
    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(nameof(AspectRatio), typeof(double),
        typeof(NorthernLightsEffect), new UIPropertyMetadata(8.0, PixelShaderConstantCallback(1)));

    public NorthernLightsEffect()
    {
        PixelShader = Shader;
        UpdateShaderValue(TimeProperty);
        UpdateShaderValue(AspectRatioProperty);
    }

    public double Time { get => (double)GetValue(TimeProperty); set => SetValue(TimeProperty, value); }
    public double AspectRatio { get => (double)GetValue(AspectRatioProperty); set => SetValue(AspectRatioProperty, value); }

    private static PixelShader LoadShader()
    {
        var shader = new PixelShader { UriSource = new Uri("pack://application:,,,/OledAurora;component/Shaders/NorthernLights.ps") };
        shader.Freeze();
        return shader;
    }
}
