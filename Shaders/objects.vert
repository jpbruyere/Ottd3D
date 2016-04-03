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
	mat4 shadowTexMat;
	vec4 lightPos;
	vec4 Color;
	vec4 Shared; //x=ScreenGama, y=ShadingPass
};

uniform vec3 bones[4];

out vec2 texCoord;
out vec4 shadowCoord;			
out vec3 n;			
out vec4 vEyeSpacePos;

vec3 qtransform( vec4 q, vec3 v ){ 
	return v + 2.0*cross(cross(v, q.xyz ) + q.w*v, q.xyz);
}
vec4 qmult(vec4 q1, vec4 q2){
	return vec4(
		(q1.w * q2.x) + (q1.x * q2.w) + (q1.y * q2.z) - (q1.z * q2.y),
	    (q1.w * q2.y) - (q1.x * q2.z) + (q1.y * q2.w) + (q1.z * q2.x),
	    (q1.w * q2.z) + (q1.x * q2.y) - (q1.y * q2.x) + (q1.z * q2.w),
	    (q1.w * q2.w) - (q1.x * q2.x) - (q1.y * q2.y) - (q1.z * q2.z));
}
vec4 quat_conj(vec4 q)
{ 
  return vec4(-q.x, -q.y, -q.z, q.w); 
}
void applyBone(inout vec3 inV, float w, vec4 q, vec4 bpos){
	inV = qtransform(q * w, inV - bpos.xyz) + bpos.xyz;
}
vec3 transformPositionDQ( vec3 position, vec4 realDQ, vec4 dualDQ )
{
    return position +
        2 * cross( realDQ.xyz, cross(realDQ.xyz, position) + realDQ.w*position ) +
        2 * (realDQ.w * dualDQ.xyz - dualDQ.w * realDQ.xyz + 
            cross( realDQ.xyz, dualDQ.xyz));
}
 
vec3 transformNormalDQ( vec3 normal, vec4 realDQ, vec4 dualDQ )
{
    return normal + 2.0 * cross( realDQ.xyz, cross( realDQ.xyz, normal ) + 
                          realDQ.w * normal );
}

void main(void)
{
	vec3 inN = in_normal;
	vec3 inP = in_position;

	inN = transformNormalDQ(inN, in_quat0 * in_weights.x, in_bpos0);
	inN = transformNormalDQ(inN, in_quat1 * in_weights.y, in_bpos1);
	inN = transformNormalDQ(inN, in_quat2 * in_weights.z, in_bpos2);
	inN = transformNormalDQ(inN, in_quat3 * in_weights.w, in_bpos3);
	inP = transformPositionDQ(inP - bones[0], in_quat0 * in_weights.x, in_bpos0)+bones[0];
	inP = transformPositionDQ(inP - bones[1], in_quat1 * in_weights.y, in_bpos1)+bones[1];
	inP = transformPositionDQ(inP - bones[2], in_quat2 * in_weights.z, in_bpos2)+bones[2];
	inP = transformPositionDQ(inP - bones[3], in_quat3 * in_weights.w, in_bpos3)+bones[3];

	texCoord = in_tex;
	n = vec3(Normal * vec4(inN,0));

	vEyeSpacePos = ModelView * in_model * vec4(inP, 1);
	shadowCoord = shadowTexMat * in_model * vec4(inP, 1);
	
	gl_Position = Projection * ModelView * in_model * vec4(inP, 1);
}