Shader "The Worst Christmas/Face Monster"
{
    Properties
    {
        _MainTex ("Exact face reference", 2D) = "black" {}
        _FaceRect ("Face placement (centre X, centre Y, width, height)", Vector) = (0, .92, .58, .65)
        _FaceBrightness ("Face brightness", Range(0, 4)) = 1
        _FaceRotation ("Face rotation", Range(-180, 180)) = 180
        _Vanish ("Ghost vanish", Range(0, 1)) = 0
        _GhostLift ("Ghost rise (metres)", Float) = .18
        _GhostBounds ("Ghost bounds (world centre XYZ, height)", Vector) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        CGINCLUDE
        #include "UnityCG.cginc"
        float _Vanish, _GhostLift;
        float4 _GhostBounds;

        float GhostHash(float3 p)
        {
            p = frac(p * .1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }
        float GhostNoise(float3 p)
        {
            float3 cell = floor(p);
            float3 f = frac(p); f = f * f * (3 - 2 * f);
            float lo = lerp(lerp(GhostHash(cell), GhostHash(cell + float3(1,0,0)), f.x),
                            lerp(GhostHash(cell + float3(0,1,0)), GhostHash(cell + float3(1,1,0)), f.x), f.y);
            float hi = lerp(lerp(GhostHash(cell + float3(0,0,1)), GhostHash(cell + float3(1,0,1)), f.x),
                            lerp(GhostHash(cell + float3(0,1,1)), GhostHash(cell + float3(1,1,1)), f.x), f.y);
            return lerp(lo, hi, f.z);
        }
        float3 GhostPosition(float3 world)
        {
            float amount = saturate(_Vanish);
            float height = max(.05, _GhostBounds.w);
            float phase = (world.y - _GhostBounds.y) / height;
            world.x += sin(phase * 8 + _Time.y * 1.6) * height * .055 * amount;
            world.z += cos(phase * 6 + _Time.y * 1.3) * height * .035 * amount;
            world.y += _GhostLift * amount * amount;
            return world;
        }
        void GhostClip(float3 originalWorld, float2 pixel)
        {
            if (_Vanish <= .0001) return;
            float height = max(.05, _GhostBounds.w);
            float vertical = saturate((originalWorld.y - _GhostBounds.y) / height + .5);
            float3 noisePosition = (originalWorld - _GhostBounds.xyz) / height;
            noisePosition.y -= _Time.y * .19;
            float smoke = GhostNoise(noisePosition * 13) * .72 + GhostNoise(noisePosition * 29) * .28;
            float field = vertical * .55 + smoke * .45;
            float threshold = lerp(-.24, 1.24, saturate(_Vanish));
            float coverage = smoothstep(threshold - .1, threshold + .1, field);
            // Screen-door coverage softens each dissolving wisp without the
            // depth-sorting faults of a transparent, many-limbed skinned mesh.
            float dither = frac(52.9829189 * frac(dot(floor(pixel), float2(.06711056, .00583715))));
            clip(coverage - dither - .001);
        }
        struct appdata
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 head : TEXCOORD1;
        };
        ENDCG
        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            sampler2D _MainTex;
            float4 _FaceRect;
            float _FaceBrightness, _FaceRotation;
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 head : TEXCOORD0;
                float3 originalWorld : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };
            v2f vert(appdata v)
            {
                v2f o;
                o.originalWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.vertex = UnityWorldToClipPos(GhostPosition(o.originalWorld));
                o.head = v.head;
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                GhostClip(i.originalWorld, i.vertex.xy);
                float2 projected = (i.head.xy - _FaceRect.xy) / _FaceRect.zw;
                float angle = radians(_FaceRotation);
                float2 uv = float2(cos(angle) * projected.x - sin(angle) * projected.y,
                                  sin(angle) * projected.x + cos(angle) * projected.y) + .5;
                float mask = smoothstep(.35, .7, i.head.z) * smoothstep(-.03, .12, i.head.w);
                float2 border = min(uv, 1 - uv);
                mask *= smoothstep(0, .025, min(border.x, border.y));
                // The photograph sinks into black before the body unravels.
                mask *= 1 - smoothstep(0, .32, _Vanish);
                fixed4 colour = fixed4(tex2D(_MainTex, saturate(uv)).rgb * mask * _FaceBrightness, 1);
                UNITY_APPLY_FOG(i.fogCoord, colour);
                return colour;
            }
            ENDCG
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex shadowVert
            #pragma fragment shadowFrag
            #pragma multi_compile_shadowcaster
            struct shadowOutput
            {
                V2F_SHADOW_CASTER;
                float3 originalWorld : TEXCOORD1;
            };
            shadowOutput shadowVert(appdata v)
            {
                shadowOutput o;
                o.originalWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
                v.vertex = mul(unity_WorldToObject, float4(GhostPosition(o.originalWorld), 1));
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
            float4 shadowFrag(shadowOutput i) : SV_Target
            {
                GhostClip(i.originalWorld, i.pos.xy);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "Unlit/Texture"
}
