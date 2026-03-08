Shader "GPFS/Demo/Sample"
{
    Properties
    {
        [GradientGUI] _Gradient_gradienttexture ("Gradient", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "Assets/Gradient Property for Shader/Assets/Sample Gradient.hlsl"

        sampler2D _Gradient_gradienttexture;

        struct Input
        {
            float2 uv_Gradient_gradienttexture;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = SampleGradient(_Gradient_gradienttexture, float2(IN.uv_Gradient_gradienttexture.x, 0));
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
