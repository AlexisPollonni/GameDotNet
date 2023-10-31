//we will be using glsl version 4.5 syntax
#version 450
#extension GL_EXT_debug_printf: enable

layout (location = 0) in vec3 vPosition;
layout (location = 1) in vec3 vNormal;
layout (location = 2) in vec4 vColor;

// output to frag shader
layout (location = 0) out vec4 outColor;

layout (binding = 0, row_major) uniform CameraData
{
    vec4 data;
    mat4 render_matrix;
} Data;

void main()
{
    gl_Position = vec4(vPosition, 1.0f) * Data.render_matrix;
    outColor = vColor;

    //    debugPrintfEXT("RenderMat = [ %f, %f, %f, %f ]; [ %f, %f, %f, %f ]; [%f, %f, %f, %f ]; [ %f, %f, %f, %f ]",
    //                    r[0][0], r[0][1], r[0][2], r[0][3],
    //                    r[1][0], r[1][1], r[1][2], r[1][3],
    //                    r[2][0], r[2][1], r[2][2], r[2][3],
    //                    r[3][0], r[3][1], r[3][2], r[3][3]
    //    );
    //debugPrintfEXT("gl_position = %v4f, Position = %v3f, Normal = %v3f, Color = %v4f", gl_Position, vPosition, vNormal, vColor);
}
