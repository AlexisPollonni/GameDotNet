//we will be using glsl version 4.5 syntax
#version 450
#extension GL_EXT_debug_printf: enable

layout (location = 0) in vec3 vPosition;
layout (location = 1) in vec3 vNormal;
layout (location = 2) in vec4 vColor;

// output to frag shader
layout (location = 0) out vec4 outColor;

layout (push_constant) uniform constants
{
    vec4 data;
    mat4 render_matrix;
} PushConstants;

void main()
{
    mat4 r = PushConstants.render_matrix;

    gl_Position = PushConstants.render_matrix * vec4(vPosition, 1.0f);
    outColor = vColor;

    //    debugPrintfEXT("RenderMat = [ %f, %f, %f, %f ]; [ %f, %f, %f, %f ]; [%f, %f, %f, %f ]; [ %f, %f, %f, %f ]",
    //                    r[0][0], r[0][1], r[0][2], r[0][3],
    //                    r[1][0], r[1][1], r[1][2], r[1][3],
    //                    r[2][0], r[2][1], r[2][2], r[2][3],
    //                    r[3][0], r[3][1], r[3][2], r[3][3]
    //    );
    //debugPrintfEXT("Position = %v3f, Normal = %v3f, Color = %v4f", PushConstants.render_matrix, vPosition, vNormal, vColor);
}
