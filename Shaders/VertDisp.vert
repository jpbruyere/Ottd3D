#version 330

precision highp float;

layout (std140, index = 0) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	vec4 lightPos;
	vec4 Color;
};

uniform mat4 Model;

uniform vec2 mapSize;
uniform float heightScale;

uniform sampler2D heightMap;

in vec3 in_position;
in vec2 in_tex;


out vec2 texCoord;
out vec2 splatTexCoord;
//flat out float layer;
out vec4 vEyeSpacePos;
out vec3 n;

//selection map output
out vec4 vertex;

void main(void)
{
	vec2[] offsets = vec2[]
	(
		vec2(0,0),
		vec2(0,1),
		vec2(1,0),
		vec2(0,-1),
		vec2(-1,0)
	);
	vec3[5] pos;

	texCoord = in_tex;

	vec4 hm0 = texture2D(heightMap, in_position.xy / mapSize);
	pos[0] = vec3(in_position.xy, hm0.g * heightScale);
	//layer = hm0.b * 255.0;
	splatTexCoord = in_position.xy / mapSize;

	for(int i = 1; i < 5; i++){
		vec2 xy = in_position.xy + offsets[i];
		float h = texture2D( heightMap, xy / mapSize).g * heightScale;
		pos[i] = vec3(xy, h);
	}


	n = normalize(
		cross(pos[2] - pos[0], pos[1] - pos[0])
	  + cross(pos[3] - pos[0], pos[2] - pos[0]) 
	  + cross(pos[4] - pos[0], pos[3] - pos[0]) 
	  + cross(pos[1] - pos[0], pos[4] - pos[0]) / 4.0);
	  /*
	vec3 va = normalize( vec3(1.0, 0.0, pos[2].z - pos[0].z) );
	vec3 vb = normalize( vec3(0.0, 1.0, pos[1].z - pos[0].z) );

	n = normalize( cross(va, vb) );
	*/
	  n = (Normal * vec4(n,0)).xyz;
	vEyeSpacePos = ModelView *  vec4(pos[0], 1);
	vertex = vec4((pos[0].xy) / (mapSize-vec2(1.0,1.0)), pos[0].z / heightScale, 1.0);
	gl_Position = Projection * vEyeSpacePos;
}
