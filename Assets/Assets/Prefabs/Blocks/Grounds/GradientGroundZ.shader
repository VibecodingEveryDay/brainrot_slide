Shader "GPFS/GradientGroundZ"
{
    Properties
    {
        // Градиентная текстура (полоса по X), рисуется через Gradient Property for Shader
        [GradientGUI] _Gradient_GradientTexture ("Gradient", 2D) = "white" {}

        // Базовая текстура земли
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Albedo Tint", Color) = (1,1,1,1)

        // Диапазон по мировой оси Z, в котором разворачивается градиент
        _MinZ ("Min Z (world)", Float) = 0
        _MaxZ ("Max Z (world)", Float) = 10

        // Сколько раз градиент повторяется вдоль диапазона [MinZ..MaxZ]
        _RepeatCount ("Gradient Repeats", Float) = 1

        // Доля конца градиента, на которой
        // последний цвет плавно переходит в первый
        _WrapBlend ("Wrap Blend Fraction", Range(0,0.5)) = 0.15

        // Яркость итогового цвета
        _Brightness ("Brightness", Range(0,2)) = 1

        // Гладкость (для последующего использования в освещении / эффектах)
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _MinZ;
                float  _MaxZ;
                float  _RepeatCount;
                float  _Brightness;
                float  _WrapBlend;
                float  _Smoothness;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_Gradient_GradientTexture);
            SAMPLER(sampler_Gradient_GradientTexture);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Albedo с учётом tiling/offset
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                // Нормализуем мировую координату Z в [0,1]
                float range = max(_MaxZ - _MinZ, 0.0001);
                float baseT = saturate((IN.positionWS.z - _MinZ) / range);

                // Повторяем градиент RepeatCount раз по диапазону
                float t = frac(baseT * max(_RepeatCount, 1.0));

                // Плавный переход последнего цвета в первый при повторении
                float wrapBlend = saturate(_WrapBlend);
                half4 grad;

                if (wrapBlend > 0.0001)
                {
                    float mainEnd = 1.0 - wrapBlend;

                    // Цвет в начале и в конце градиента
                    half4 colStart = SAMPLE_TEXTURE2D(
                        _Gradient_GradientTexture,
                        sampler_Gradient_GradientTexture,
                        float2(0.0, 0.5)
                    );
                    half4 colEnd = SAMPLE_TEXTURE2D(
                        _Gradient_GradientTexture,
                        sampler_Gradient_GradientTexture,
                        float2(1.0, 0.5)
                    );

                    // Основная часть градиента [0 .. mainEnd]
                    if (t < mainEnd)
                    {
                        float mappedT = t / mainEnd; // 0..1
                        grad = SAMPLE_TEXTURE2D(
                            _Gradient_GradientTexture,
                            sampler_Gradient_GradientTexture,
                            float2(mappedT, 0.5)
                        );
                    }
                    else
                    {
                        // Хвостовая зона [mainEnd .. 1],
                        // где фиксированный "конец" (фиолетовый) плавно
                        // переходит в "начало" (красный)
                        float tailT = (t - mainEnd) / wrapBlend; // 0..1
                        grad = lerp(colEnd, colStart, tailT);
                    }
                }
                else
                {
                    // Обычное повторение без сглаживания
                    grad = SAMPLE_TEXTURE2D(
                        _Gradient_GradientTexture,
                        sampler_Gradient_GradientTexture,
                        float2(t, 0.5)
                    );
                }

                half3 rgb = albedo.rgb * grad.rgb * _Brightness;
                half  a   = albedo.a * grad.a;

                return half4(rgb, a);
            }

            ENDHLSL
        }
    }
}

