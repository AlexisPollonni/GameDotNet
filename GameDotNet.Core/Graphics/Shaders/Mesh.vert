//we will be using glsl version 4.5 syntax
#version 450
#extension GL_EXT_debug_printf : enable

layout (location = 0) in vec3 vPosition;
layout (location = 1) in vec3 vNormal;
layout (location = 2) in vec4 vColor;

// output to frag shader
layout (location = 0) out vec4 outColor;

void main()
{
    gl_Position = vec4(vPosition, 1.0f);
    outColor = vColor;

    //debugPrintfEXT("Position = %v3f, Normal = %v3f, Color = %v4f", vPosition, vNormal, vColor);
}
