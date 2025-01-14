

uniform CameraData Camera;
uniform float4x4 ModelMatrix;

struct CameraData
{
    float4x4 View;
    float4x4 Projection;
};

struct VSOutput 
{
    float4 Position : SV_Position,
    float4 Color : COLOR
};

[shader("vertex")]
VSOutput vertexMain(
        float3 vPosition : POSITION,
        float3 vNormal : NORMAL,
        float4 vColor : COLOR
    ) : SV_Position
{
    var position = Camera.Projection * Camera.View * ModelMatrix * float4(vPosition, 0);

    return new VSOutput(position, vColor);
}