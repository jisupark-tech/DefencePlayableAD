Shader "Custom/CustomRepeatPattern_Scrolling"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        // X축 반복 개수 (1이면 한 번, 5면 5번 반복)
        _TilingX ("Tiling X", Float) = 1.0

        // X축 시작 오프셋 (수동으로 밀고 싶을 때)
        _OffsetX ("Offset X", Float) = 0.0

        // 시간에 따라 앞으로 움직이는 속도
        _SpeedX ("Scroll Speed X", Float) = 0.5

        _Color ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv     : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float  _TilingX;
            float  _OffsetX;
            float  _SpeedX;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // 기본 UV는 그대로 넘기고,
                // 실제 Tiling/Offset/스크롤은 fragment에서 처리
                o.uv = v.uv;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 크기/모양은 유지: 기본 uv로 시작
                float2 uv = i.uv;

                // X 방향으로만 Tiling + 수동 Offset + 시간 기반 Offset(애니메이션)
                // => 패턴의 크기는 유지하고, 그냥 앞으로 흐르기만 함
                uv.x = uv.x * _TilingX
                              + _OffsetX
                              + _SpeedX * _Time.y;

                // 0~1 사이로 잘라서 반복
                float2 repeatedUV = frac(uv);

                fixed4 col = tex2D(_MainTex, repeatedUV) * _Color;
                return col;
            }
            ENDCG
        }
    }
}
