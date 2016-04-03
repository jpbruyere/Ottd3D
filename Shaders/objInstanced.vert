#version 330
precision highp float;

layout (location = 0) in vec3 in_position;
layout (location = 1) in vec2 in_tex;
layout (location = 2) in vec3 in_normal;
layout (location = 4) in mat4 in_model;

layout (std140) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	mat4 shadowTexMat;
	vec4 lightPos;
	vec4 Color;
	vec4 Shared; //x=ScreenGama, y=ShadingPass
};

out vec2 texCoord;			
out vec3 n;			
out vec4 vEyeSpacePos;

void main(void)
{
	texCoord = in_tex;
	n = normalize(vec3(Normal * in_model * vec4(in_normal,0)));

	vEyeSpacePos = ModelView * in_model * vec4(in_position, 1);
	
	gl_Position = Projection * ModelView * in_model * vec4(in_position, 1);
}