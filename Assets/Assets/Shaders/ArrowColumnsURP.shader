Shader "Custom/ArrowColumnsURP"
{
    Properties
    {
        [MainTexture]_MainTex("Arrow Texture (RGBA)", 2D) = "white" {}
        [MainColor]_Color("Tint", Color) = (1,1,1,1)

        _Speed("Speed (arrows/sec)", Float) = 1
        _ArrowsPerColumn("Arrows Per Column (vertical)", Float) = 3
        _ColumnsOffset("Columns Offset (0-1 of cell)", Range(0,1)) = 0.2
        _ArrowFillV("Element Offset (0-1 of tile)", Range(0,1)) = 0.3

        _Columns("Columns Count (X)", Float) = 4
        _ColumnDelay("Column Delay (sec)", Float) = 0.12
        _ArrowWidth01("Arrow Width (0-1 of column)", Range(0,1)) = 0.7

        _ImageScaleX("Image Width Scale", Float) = 1
        _ImageScaleY("Image Height Scale", Float) = 1

        _GridScaleX("Grid Scale X", Float) = 1
        _GridScaleY("Grid Scale Y", Float) = 1

        _EdgeSoftness("Edge Softness", Range(0,0.2)) = 0.02
        _Alpha("Alpha", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _Speed;
                float _ArrowsPerColumn;
                float _ColumnsOffset;
                float _ArrowFillV;
                float _Columns;
                float _ColumnDelay;
                float _ArrowWidth01;
                float _ImageScaleX;
                float _ImageScaleY;
                float _GridScaleX;
                float _GridScaleY;
                float _EdgeSoftness;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            static float SoftStep(float edge0, float edge1, float x, float softness)
            {
                // Smoothstep with controllable softness; avoids hard aliasing at edges.
                float t = saturate((x - edge0) / max(edge1 - edge0, 1e-5));
                float s = saturate(softness * 50.0); // map [0..0.2] into usable smoothing
                return smoothstep(0.0, 1.0, lerp(t, smoothstep(0.0, 1.0, t), s));
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Масштаб сетки: позволяет уменьшать/увеличивать количество рядов и колонн.
                float2 baseUV = IN.uv * float2(max(_GridScaleX, 1e-4), max(_GridScaleY, 1e-4));

                // Колонки по X считаем из UV, с зазором между колоннами (ColumnsOffset).
                float columns = max(_Columns, 1.0);
                float gapFracX = saturate(_ColumnsOffset);      // доля ячейки под зазор
                float fillFracX = max(1.0 - gapFracX, 1e-4);    // доля ячейки под саму колонну

                float xCell = baseUV.x * columns;               // [0..columns)
                float colIdx = floor(xCell);
                float cellFrac = frac(xCell);                   // [0..1) внутри ячейки "колонна+зазор"

                float inColumn = step(cellFrac, fillFracX);     // 1 внутри колонки, 0 в зазоре
                float colFrac = saturate(cellFrac / fillFracX); // нормализованный X внутри колонки [0..1]

                // Center within column and apply width mask (in column-normalized space).
                float halfW = 0.5 * _ArrowWidth01;
                float xCentered = (colFrac - 0.5);
                float xMask = smoothstep(halfW + _EdgeSoftness, halfW, abs(xCentered)) * inColumn;

                // Скроллим узор по V (вертикали UV): инвертируем направление,
                // чтобы стрелки шли от меньшего Z к большему.
                float scroll = -_Time.y * _Speed - colIdx * _ColumnDelay;
                float tiles = max(_ArrowsPerColumn, 1.0);
                float vBase = baseUV.y * tiles + scroll;
                float vTile = frac(vBase); // 0..1 внутри одного вертикального тайла

                // ElementOffset трактуем как долю тайла, которая остаётся ПУСТОЙ (зазор между стрелками).
                float gapV = saturate(_ArrowFillV);
                float fillV = max(1.0 - gapV, 1e-4);            // доля тайла под саму стрелку

                float vArrow = saturate(vTile / fillV);         // сжатие в нижнюю часть тайла
                float vMask = step(vTile, fillV);               // 1 только в зоне стрелки

                // Separate scaling of the image inside the logical arrow window.
                // Scale around the center (0.5, 0.5) so можно уменьшать/увеличивать стрелку
                // по ширине (_ImageScaleX) и высоте (_ImageScaleY), не меняя движение по Z.
                float2 uvLocal = float2(colFrac, vArrow);
                uvLocal = (uvLocal - 0.5) * float2(_ImageScaleX, _ImageScaleY) + 0.5;

                float2 uv = TRANSFORM_TEX(uvLocal, _MainTex);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Fade edges a bit to reduce popping at entry/exit.
                float vEdge = min(vArrow, 1.0 - vArrow);
                float vSoft = SoftStep(0.0, 0.02, vEdge, _EdgeSoftness);

                half a = tex.a * (half)(xMask * vSoft * vMask) * (half)_Alpha;
                half3 rgb = tex.rgb * _Color.rgb;
                return half4(rgb, a) * _Color.a;
            }
            ENDHLSL
        }
    }
}

