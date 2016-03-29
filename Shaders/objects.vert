#version 330
precision highp float;

layout (location = 0) in vec3 in_position;
layout (location = 1) in vec2 in_tex;
layout (location = 2) in vec3 in_normal;
layout (location = 3) in vec4 in_weights;
layout (location = 4) in mat4 in_model;
layout (location = 8) in vec4 in_quat0;
layout (location = 9) in vec4 in_quat1;
layout (location = 10) in vec4 in_quat2;
layout (location = 11) in vec4 in_quat3;
layout (location = 12) in vec4 in_bpos0;
layout (location = 13) in vec4 in_bpos1;
layout (location = 14) in vec4 in_bpos2;
layout (location = 15) in vec4 in_bpos3;

layout (std140) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	vec4 lightPos;
	vec4 Color;
};

out vec2 texCoord;			
out vec3 n;			
out vec4 vEyeSpacePos;

vec3 qtransform( vec4 q, vec3 v ){ 
	return v + 2.0*cross(cross(v, q.xyz ) + q.w*v, q.xyz);
}

vec3 applyBone(vec3 inV, float w, vec4 q, vec4 bpos){
	if (w == 0.0)
		return inV;
	return qtransform(q * w, inV - bpos.xyz) + bpos.xyz;
}

void main(void)
{
	vec3 inN = in_normal;
	vec3 inP = in_position;

	inN = applyBone(inN, in_weights.x, in_quat0, vec4(0.0));
	inN = applyBone(inN, in_weights.y, in_quat1, vec4(0.0));
	inN = applyBone(inN, in_weights.z, in_quat2, vec4(0.0));
	inN = applyBone(inN, in_weights.w, in_quat3, vec4(0.0));
	inP = applyBone(inP, in_weights.x, in_quat0, in_bpos0);
	inP = applyBone(inP, in_weights.y, in_quat1, in_bpos1);
	inP = applyBone(inP, in_weights.z, in_quat2, in_bpos2);
	inP = applyBone(inP, in_weights.w, in_quat3, in_bpos3);

	texCoord = in_tex;
	n = vec3(Normal * vec4(inN,0));

	vEyeSpacePos = ModelView * in_model * vec4(inP, 1);
	
	gl_Position = Projection * ModelView * in_model * vec4(inP, 1);
}