Shader "MechStorm/UI/SegmentedSlider"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _Value ("Value", Float) = 2.5
        _MinValue ("Min Value", Float) = 0
        _MaxValue ("Max Value", Float) = 5
        _WholeNumbers ("Whole Numbers", Range(0, 1)) = 0
        _FillWholeCells ("Fill Whole Cells", Range(0, 1)) = 0
        [Enum(LeftToRight, 0, RightToLeft, 1, BottomToTop, 2, TopToBottom, 3)] _FillDirection ("Fill Direction", Float) = 0

        _ActiveColor ("Active Color", Color) = (1, 0.82, 0.12, 1)
        _InactiveColor ("Inactive Color", Color) = (0.18, 0.2, 0.24, 1)
        [NoScaleOffset] _SegmentColorTex ("Per Segment Color LUT", 2D) = "white" {}
        [HideInInspector] _UseSegmentColors ("Use Per Segment Colors", Float) = 0

        [Enum(None, 0, Contiguous, 1)] _HighlightMode ("Highlight Mode", Float) = 0
        _HighlightCount ("Highlight Count", Float) = 0
        [Enum(LeftToRight, 0, RightToLeft, 1, BottomToTop, 2, TopToBottom, 3)] _HighlightDirection ("Highlight Direction", Float) = 1
        [Enum(PhysicalStart, 0, PhysicalEnd, 1, FilledEdge, 2)] _HighlightAnchor ("Highlight Anchor", Float) = 2
        _BreathingHighlightColor ("Breathing Highlight Color", Color) = (1, 1, 1, 1)
        _BreathingHighlightSpeed ("Breathing Highlight Speed", Float) = 6
        _BreathingHighlightStrength ("Breathing Highlight Strength", Range(0, 1)) = 0.8
        [HideInInspector] _BreathingHighlightEnabled ("Breathing Highlight Enabled", Range(0, 1)) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "SegmentedSlider"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            fixed4 _Color;
            float4 _ClipRect;

            float _Value;
            float _MinValue;
            float _MaxValue;
            float _WholeNumbers;
            float _FillWholeCells;
            float _FillDirection;
            fixed4 _ActiveColor;
            fixed4 _InactiveColor;

            sampler2D _SegmentColorTex;
            float _UseSegmentColors;
            float _HighlightMode;
            float _HighlightCount;
            float _HighlightDirection;
            float _HighlightAnchor;
            fixed4 _BreathingHighlightColor;
            float _BreathingHighlightSpeed;
            float _BreathingHighlightStrength;
            float _BreathingHighlightEnabled;

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 cellData : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.vertex = UnityObjectToClipPos(input.vertex);
                output.worldPosition = input.vertex;
                output.cellData = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            float GetSegmentCount()
            {
                return max(floor(_MaxValue + 0.5), 1.0);
            }

            float GetFilledCellCount(float segmentCount)
            {
                float valueRange = max(_MaxValue - _MinValue, 0.0001);
                float fillRatio = saturate((_Value - _MinValue) / valueRange);
                if (_WholeNumbers > 0.5)
                {
                    fillRatio = floor(fillRatio * segmentCount + 0.0001) / segmentCount;
                }

                float filledCells = fillRatio * segmentCount;
                return _FillWholeCells > 0.5
                    ? floor(filledCells + 0.0001)
                    : filledCells;
            }

            float GetCellFill(float segmentIndex, float segmentPosition, float filledCells)
            {
                float cellProgress = filledCells - segmentIndex;
                float antiAlias = min(max(fwidth(segmentPosition) * 1.5, 0.001), 0.05);
                return smoothstep(0.0, antiAlias, cellProgress - segmentPosition);
            }

            float GetPhysicalSegmentIndex(float segmentIndex, float segmentCount, bool reversed)
            {
                return reversed
                    ? segmentCount - 1.0 - segmentIndex
                    : segmentIndex;
            }

            float GetHighlightAnchorIndex(float segmentCount, float filledCells, bool fillReversed)
            {
                if (_HighlightAnchor < 0.5)
                {
                    return 0.0;
                }

                if (_HighlightAnchor < 1.5)
                {
                    return segmentCount - 1.0;
                }

                return fillReversed
                    ? segmentCount - ceil(filledCells)
                    : max(ceil(filledCells) - 1.0, 0.0);
            }

            float GetHighlightMask(float segmentIndex, float segmentCount, float filledCells,
                bool vertical)
            {
                if (_HighlightMode < 0.5)
                {
                    return 0.0;
                }

                float highlightCount = min(max(floor(_HighlightCount + 0.5), 0.0), segmentCount);
                if (highlightCount < 0.5)
                {
                    return 0.0;
                }

                bool fillReversed = fmod(_FillDirection, 2.0) > 0.5;
                float physicalIndex = GetPhysicalSegmentIndex(segmentIndex, segmentCount, fillReversed);
                float anchorIndex = GetHighlightAnchorIndex(segmentCount, filledCells, fillReversed);
                bool moveBackward = vertical
                    ? _HighlightDirection > 2.5
                    : _HighlightDirection > 0.5 && _HighlightDirection < 1.5;
                float distance = moveBackward
                    ? anchorIndex - physicalIndex
                    : physicalIndex - anchorIndex;
                float contiguousMask = step(0.0, distance) * step(distance, highlightCount - 1.0);
                float fullyFilledMask = step(segmentIndex + 0.5, filledCells);
                return contiguousMask * fullyFilledMask;
            }

            fixed3 ApplyBreathingHighlightEffects(fixed3 baseColor, float highlightMask)
            {
                float pulse = 0.5 + 0.5 * sin(_Time.y * max(_BreathingHighlightSpeed, 0.0001));
                float amount = _BreathingHighlightEnabled * highlightMask * pulse;
                return lerp(baseColor, _BreathingHighlightColor.rgb,
                    amount * saturate(_BreathingHighlightStrength));
            }

            fixed4 frag(v2f input) : SV_Target
            {
                bool vertical = _FillDirection > 1.5;
                bool reversed = fmod(_FillDirection, 2.0) > 0.5;
                float segmentIndex = vertical ? input.cellData.x : input.cellData.y;
                float segmentPosition = vertical ? input.cellData.y : input.cellData.x;
                segmentIndex = floor(segmentIndex + 0.5);

                if (reversed)
                {
                    segmentPosition = 1.0 - segmentPosition;
                }

                float segmentCount = GetSegmentCount();
                float filledCells = GetFilledCellCount(segmentCount);
                float fillMask = GetCellFill(segmentIndex, segmentPosition, filledCells);
                fixed4 segmentActiveColor = _ActiveColor;
                if (_UseSegmentColors > 0.5)
                {
                    float2 lutUv = float2((segmentIndex + 0.5) / segmentCount, 0.5);
                    segmentActiveColor = tex2D(_SegmentColorTex, lutUv);
                }

                fixed4 outputColor = lerp(_InactiveColor, segmentActiveColor, fillMask);

                float highlight = GetHighlightMask(segmentIndex, segmentCount, filledCells, vertical);
                outputColor.rgb = ApplyBreathingHighlightEffects(outputColor.rgb, highlight);
                outputColor *= input.color;

                #ifdef UNITY_UI_CLIP_RECT
                outputColor.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outputColor.a - 0.001);
                #endif

                return outputColor;
            }
            ENDCG
        }
    }
}
