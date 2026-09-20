Shader "GpuSim/InstancedQuad"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"

            StructuredBuffer<float4x4> _MatBuf;
            StructuredBuffer<float4>   _ColorBuf;

            static const float2 quad[4] = {
                float2(-0.5, -0.5), float2(0.5, -0.5),
                float2(0.5, 0.5),  float2(-0.5, 0.5)
            };

            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; };

            v2f vert(uint vid : SV_VertexID, uint inst : SV_InstanceID)
            {
                v2f o;
                float4x4 m = _MatBuf[inst];
                // перенос в последней строке (m._41/_42) → вектор-строка: v*M
                float3 p = mul(float4(quad[vid], 0, 1), m).xyz;
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1));
                o.col = _ColorBuf[inst];
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
}