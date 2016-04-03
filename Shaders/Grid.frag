#version 330
precision mediump float;

layout (std140) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	mat4 shadowTexMat;
	vec4 lightPos;
	vec4 Color;
	vec4 Shared; //x=ScreenGama, y=ShadingPass
};
layout (std140) uniform materialData{
	vec3 Diffuse;
	vec3 Ambient;
	vec3 Specular;
	float Shininess;
};
layout (std140) uniform fogData
{ 
	vec4 fogColor;
	vec4 fog;	// x fStart This is only for linear fog
				// y fEnd  This is only for linear fog
				// z fDensity For exp and exp2 equation   
				// w iEquation 0 = linear, 1 = exp, 2 = exp2
};

uniform sampler2DArray tex;
uniform sampler2D splatTex;
uniform sampler2DShadow shadowTex;

uniform float sel_radius;
uniform vec2 sel_center;
uniform vec4 sel_color;

in vec2 texCoord;
in vec2 splatTexCoord;
in vec3 n;
in vec4 vEyeSpacePos;
in vec4 vertex;
in vec4 shadowCoord;

layout(location = 0) out vec4 out_frag_color;
layout(location = 1) out vec4 out_frag_selection;


vec2 EncodeFloatRGBA( float v ) {
  vec2 enc = vec2(1.0, 255.0) * v;
  enc = fract(enc);
  enc -= enc.yy * vec2(1.0/255.0,0.0);
  return enc;
}
   
float getFogFactor(float FogCoord)
{
	float Start = fog.x;
	float End = fog.y;
	float Density = fog.z;
	float Equation = fog.w;

   float Result = 0.0;
   if(Equation == 0.0)
      Result = (End-FogCoord)/(End-Start);
   else if(Equation == 1.0)
      Result = exp(-Density*FogCoord);
   else if(Equation == 2.0)
      Result = exp(-pow(Density*FogCoord, 2.0));
      
   Result = 1.0-clamp(Result, 0.0, 1.0);
   
   return Result;
}

vec2 poissonDisk[16] = vec2[](
	vec2( -0.94201624, -0.39906216 ),
	vec2( 0.94558609, -0.76890725 ),
	vec2( -0.094184101, -0.92938870 ),
	vec2( 0.34495938, 0.29387760 ),
	vec2( -0.91588581, 0.45771432 ),
	vec2( -0.81544232, -0.87912464 ),
	vec2( -0.38277543, 0.27676845 ),
	vec2( 0.97484398, 0.75648379 ),
	vec2( 0.44323325, -0.97511554 ),
	vec2( 0.53742981, -0.47373420 ),
	vec2( -0.26496911, -0.41893023 ),
	vec2( 0.79197514, 0.19090188 ),
	vec2( -0.24188840, 0.99706507 ),
	vec2( -0.81409955, 0.91437590 ),
	vec2( 0.19984126, 0.78641367 ),
	vec2( 0.14383161, -0.14100790 )
);

void main(void)
{

	if (Shared.y == 1.0){
		//shadow pass
		gl_FragDepth = gl_FragCoord.z;
	}else{
		//normal pass
		float selCoef = 0.0;
		float fFogCoord = abs(vEyeSpacePos.z/vEyeSpacePos.w);
		if (sel_color.a > 0.0){
			vec2 uv = splatTexCoord - sel_center;
			float dist =  sqrt(dot(uv, uv));
			float border = sqrt(fFogCoord)*0.00005;
			selCoef = 1.0 + smoothstep(sel_radius, sel_radius + border, dist) 
				            - smoothstep(sel_radius - border, sel_radius, dist);		
		}

		vec3 vLight;
		vec3 vEye = normalize(-vEyeSpacePos.xyz);

		if (lightPos.w == 0.0)
			vLight = normalize(-lightPos.xyz);
		else
			vLight = normalize(lightPos.xyz - vEyeSpacePos.xyz);

		//blinn phong
		vec3 halfDir = normalize(vLight + vEye);
		float specAngle = max(dot(halfDir, n), 0.0);
		vec3 Ispec = Specular * pow(specAngle, Shininess);
		float cosTheta = dot(n,vLight);
		vec3 Idiff = Diffuse * max(cosTheta, 0.0);


		vec4 splat = texture (splatTex, splatTexCoord);

		vec3 t1 = texture( tex, vec3(texCoord, splat.r * 255.0)).rgb;
		vec3 t2 = texture( tex, vec3(texCoord, splat.g * 255.0)).rgb;

		vec3 c = mix (t1, t2, splat.b);

		//Shadow
		// ...variable bias
		cosTheta = clamp(cosTheta, 0.0, 1.0);
		float bias = 0.005*tan(acos(cosTheta));
		bias = clamp(bias, 0,0.01);
		float visibility=1.0;
		// Sample the shadow map 4 times

		//float bias = 0.01;
		for (int i=0;i<4;i++)
		{
			visibility -= 0.2 * (1.0 - texture (shadowTex,
				vec3(shadowCoord.st + poissonDisk[i]/700.0,
				(shadowCoord.z+bias)/shadowCoord.w)));
		}
		/*
		if ( texture( shadowTex, shadowCoord.xy ).z  <  shadowCoord.z){ 
		    visibility = 0.0; 
		}*/
		vec3 colorLinear = c * (Ambient + Idiff) + Ispec;
		colorLinear = colorLinear * visibility;
		vec4 gcc = mix(sel_color, vec4(colorLinear,1.0), selCoef);
		gcc = mix(gcc , fogColor, getFogFactor(fFogCoord));
		float ScreenGama = Shared.x;
		out_frag_color = vec4(pow(gcc.rgb, vec3(1.0/ScreenGama)), gcc.a);


	//	ivec2 i = floatBitsToInt(vertex.xy);
	//	vec4 res = intBitsToFloat(ivec4(i.x , i.y , 1, 1));
	//	int x = floatBitsToInt(vertex.x);
	//	int y = floatBitsToInt(vertex.y);
	//	vec4 res = vec4(intBitsToFloat(x), intBitsToFloat(y), intBitsToFloat(x*256), 1.0);

		//out_frag_selection = vertex;
		//out_frag_selection = vec4(vertex.x, fract(vertex.x * 255.0), vertex.y, 1.0);

		out_frag_selection = vec4(EncodeFloatRGBA(vertex.x), EncodeFloatRGBA(vertex.y));
		gl_FragDepth = gl_FragCoord.z;
	}
}

