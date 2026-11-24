Shader "The Worst Christmas/Black Wisp"
{
    Properties { _Opacity ("Smoke opacity", Range(0, 1)) = .65 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float _Opacity;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };
            v2f vert(appdata v)
            {
                v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv;
                UNITY_TRANSFER_FOG(o,o.vertex); return o;
            }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(c), Hash(c + float2(1,0)), f.x), lerp(Hash(c + float2(0,1)), Hash(c + 1), f.x), f.y);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 q = i.uv * 2 - 1;
                q.x += sin(q.y * 5 + _Time.y * .8) * .11;
                float radial = saturate(1 - dot(q, q));
                radial = radial * radial;
                float noise = Noise(i.uv * 6 + float2(_Time.y * .12, -_Time.y * .23));
                fixed4 colour = fixed4(0, 0, 0, radial * lerp(.25, 1, noise) * i.color.a * _Opacity);
                UNITY_APPLY_FOG(i.fogCoord, colour);
                return colour;
            }
            ENDCG
        }
    }
}
