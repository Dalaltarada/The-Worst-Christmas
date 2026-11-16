Shader "WorstChristmas/SantaSkin"
{
    Properties
    {
        _MainTex ("Skin", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _Color ("Skin Tint", Color) = (0.68,0.54,0.53,1)
        _SocketColor ("Socket Tint", Color) = (0.08,0.036,0.048,1)
        _LeftEye ("Left Eye", Vector) = (0,0,0,0)
        _RightEye ("Right Eye", Vector) = (0,0,0,0)
        _SocketRadius ("Socket Radius", Float) = 0.05
        _Grime ("Worn Mottling", Range(0,1)) = 0
        _Mouth ("Frown Center and Half Width", Vector) = (0,0,0,0)
        [Toggle] _UseRestPosition ("Use Mesh Rest Position", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        sampler2D _MainTex, _BumpMap;
        fixed4 _Color, _SocketColor;
        float4 _LeftEye, _RightEye;
        float _SocketRadius;
        float _Grime;
        float4 _Mouth;
        float _UseRestPosition;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 modelPosition;
        };

        void vert(inout appdata_full vertex, out Input data)
        {
            UNITY_INITIALIZE_OUTPUT(Input, data);
            data.modelPosition = lerp(vertex.vertex.xyz, vertex.texcoord2.xyz, _UseRestPosition);
        }

        float Hash(float2 cell)
        {
            return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
        }

        float Noise(float2 position)
        {
            float2 cell = floor(position);
            float2 blend = frac(position);
            blend = blend * blend * (3 - 2 * blend);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                        lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
        }

        void surf(Input data, inout SurfaceOutputStandard output)
        {
            float3 left = (data.modelPosition - _LeftEye.xyz) * float3(1.3, 0.94, 0.53);
            float3 right = (data.modelPosition - _RightEye.xyz) * float3(1.3, 0.94, 0.53);
            float distanceToEye = min(length(left), length(right));
            float shadow = 1 - smoothstep(_SocketRadius * 0.44, _SocketRadius, distanceToEye);
            float3 skin = tex2D(_MainTex, data.uv_MainTex).rgb * _Color.rgb;
            float2 uv = data.uv_MainTex;
            float marks = Noise(uv * float2(29, 18)) * 0.7 + Noise(uv * 65) * 0.3;
            float fibers = Noise(uv * float2(240, 72));
            skin *= 1 - _Grime * (smoothstep(0.46, 0.87, marks) * 0.82 + fibers * 0.18);
            output.Albedo = lerp(skin, _SocketColor.rgb, shadow);
            float width = max(0.0001, _Mouth.w);
            float3 mouth = (data.modelPosition - _Mouth.xyz) / width;
            float edge = saturate(1 - mouth.x * mouth.x);
            float curve = abs(mouth.y + 0.42 * mouth.x * mouth.x);
            float lip = (1 - smoothstep(0.115, 0.20, curve / max(0.01, edge))) * step(abs(mouth.x), 1);
            float slit = (1 - smoothstep(0.08, 0.115, curve / max(0.01, edge))) * lip;
            lip *= smoothstep(-0.8, -0.36, mouth.z) * step(0.0001, _Mouth.w);
            output.Albedo = lerp(output.Albedo, lerp(float3(0.12,0.035,0.028), float3(0.006,0.002,0.003), slit), lip);
            output.Occlusion = 1 - max(shadow * 0.65, lip * 0.8);
            output.Normal = UnpackNormal(tex2D(_BumpMap, data.uv_BumpMap));
            output.Smoothness = 0.13;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
