#version 330
precision mediump float;

layout (std140) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	vec4 lightPos;
	vec4 Color;
};
layout (std140) uniform fogData
{ 
	vec4 fogColor;
	float fStart; // This is only for linear fog
	float fEnd; // This is only for linear fog
	float fDensity; // For exp and exp2 equation   
	int iEquation; // 0 = linear, 1 = exp, 2 = exp2
};

uniform sampler2DArray tex;
uniform sampler2D splatTex;

uniform float sel_radius;
uniform vec2 sel_center;
uniform vec4 sel_color;

in vec2 texCoord;
in vec2 splatTexCoord;
in vec3 n;
in vec4 vEyeSpacePos;
in vec4 vertex;

layout(location = 0) out vec4 out_frag_color;
layout(location = 1) out vec4 out_frag_selection;


vec2 EncodeFloatRGBA( float v ) {
  vec2 enc = vec2(1.0, 255.0) * v;
  enc = fract(enc);
  enc -= enc.yy * vec2(1.0/255.0,0.0);
  return enc;
}
   
float getFogFactor(float fFogCoord)
{
   float fResult = 0.0;
   if(iEquation == 0)
      fResult = (fEnd-fFogCoord)/(fEnd-fStart);
   else if(iEquation == 1)
      fResult = exp(-fDensity*fFogCoord);
   else if(iEquation == 2)
      fResult = exp(-pow(fDensity*fFogCoord, 2.0));
      
   fResult = 1.0-clamp(fResult, 0.0, 1.0);
   
   return fResult;
}

void main(void)
{
	float selCoef = 0.0;
	float fFogCoord = abs(vEyeSpacePos.z/vEyeSpacePos.w);
	if (sel_color.a > 0.0){
		vec2 uv = splatTexCoord - sel_center * 0.5;
		float dist =  sqrt(dot(uv, uv));
		float border = sqrt(fFogCoord)*0.00005;
		selCoef = 1.0 + smoothstep(sel_radius, sel_radius + border, dist) 
			            - smoothstep(sel_radius - border, sel_radius, dist);		
	}

	vec3 l;
	if (lightPos.w == 0.0)
		l = normalize(-lightPos.xyz);
	else
		l = normalize(lightPos.xyz - vEyeSpacePos.xyz);

	float nl = clamp(max(dot(n,l), 0.0),0.7,1.0);

	vec4 splat = texture (splatTex, splatTexCoord);

	vec3 t1 = texture( tex, vec3(texCoord, splat.r * 255.0)).rgb;
	vec3 t2 = texture( tex, vec3(texCoord, splat.g * 255.0)).rgb;

	vec4 c = vec4(mix (t1, t2, splat.b) * nl, 1.0);

	// Add fog

	c = mix(c, fogColor, getFogFactor(fFogCoord));

	out_frag_color = mix(sel_color, c, selCoef);

//	ivec2 i = floatBitsToInt(vertex.xy);
//	vec4 res = intBitsToFloat(ivec4(i.x , i.y , 1, 1));
//	int x = floatBitsToInt(vertex.x);
//	int y = floatBitsToInt(vertex.y);
//	vec4 res = vec4(intBitsToFloat(x), intBitsToFloat(y), intBitsToFloat(x*256), 1.0);

	//out_frag_selection = vertex;
	//out_frag_selection = vec4(vertex.x, fract(vertex.x * 255.0), vertex.y, 1.0);

	out_frag_selection = vec4(EncodeFloatRGBA(vertex.x), EncodeFloatRGBA(vertex.y));	
}

