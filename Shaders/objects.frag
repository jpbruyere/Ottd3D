#version 330			

precision highp float;

layout (std140) uniform fogData
{ 
	vec4 fogColor;
	vec4 fog;	// x fStart This is only for linear fog
				// y fEnd  This is only for linear fog
				// z fDensity For exp and exp2 equation   
				// w iEquation 0 = linear, 1 = exp, 2 = exp2
};

uniform sampler2D tex;
uniform sampler2D normal;
uniform sampler2DShadow shadowTex;

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

in vec2 texCoord;
in vec4 shadowCoord;			
in vec4 vEyeSpacePos;
in vec3 n;			

out vec4 out_frag_color;

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

//			vec3 CalcBumpedNormal()
//			{
//			    vec3 Normal = normalize(n);
//			    vec3 Tangent = normalize(t);
//			    Tangent = normalize(Tangent - dot(Tangent, Normal) * Normal);
//			    vec3 Bitangent = cross(Tangent, Normal);
//			    vec3 BumpMapNormal = texture(normal, texCoord).xyz;
//			    BumpMapNormal = 2.0 * BumpMapNormal - vec3(1.0, 1.0, 1.0);
//			    vec3 NewNormal;
//			    mat3 TBN = mat3(Tangent, Bitangent, Normal);
//			    NewNormal = TBN * BumpMapNormal;
//			    NewNormal = normalize(NewNormal);
//			    return NewNormal;
//			}

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

const int SHADOWPASS = 1 << 0;
const int NORMALPASS = 1 << 1;

void main(void)
{
	int pass = floatBitsToInt(Shared.y);

	if (bool(pass & SHADOWPASS)){
		gl_FragDepth = gl_FragCoord.z;
	}else{
		vec4 diffTex = texture( tex, texCoord);// * Color;
		if (diffTex.a == 0.0)
			discard;
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

		//Shadow
		// ...variable bias
		cosTheta = clamp(cosTheta, 0.0, 1.0);
		float bias = 0.005*tan(acos(cosTheta));
		bias = clamp(bias, 0,0.001);
		float visibility=1.0;
		// Sample the shadow map 4 times

		for (int i=0; i<4; i++)
		{
			visibility -= 0.2 * (1.0 - texture (shadowTex,
				vec3(shadowCoord.st + poissonDisk[i]/700.0,
					(shadowCoord.z+bias)/shadowCoord.w)));
		}

		float fFogCoord = abs(vEyeSpacePos.z/vEyeSpacePos.w);

		vec3 colorLinear = diffTex.rgb * (Ambient + Idiff) + Ispec;
		colorLinear = colorLinear * visibility;
		colorLinear = mix(colorLinear , fogColor.rgb, getFogFactor(fFogCoord));

		float ScreenGamma = Shared.x;

		out_frag_color = vec4(pow(colorLinear, vec3(1.0/ScreenGamma)), diffTex.a);
		gl_FragDepth = gl_FragCoord.z;
	}
}