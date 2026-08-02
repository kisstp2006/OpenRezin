#version 330
#ifdef GL_ARB_shading_language_420pack
#extension GL_ARB_shading_language_420pack : require
#endif

const vec2 _34[3] = vec2[](vec2(0.0, -0.5), vec2(0.5), vec2(-0.5, 0.5));
const vec3 _35[3] = vec3[](vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0));

out vec3 out_var_COLOR0;

void main()
{
    gl_Position = vec4(_34[uint(gl_VertexID)], 0.0, 1.0);
    out_var_COLOR0 = _35[uint(gl_VertexID)];
    gl_Position.y = -gl_Position.y;
}

