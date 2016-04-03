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


void main(void)
{	
	if (Shared.y == 1.0){
		//shadow pass
		gl_FragDepth = gl_FragCoord.z;
	}else{
		//normal pass

		vec4 diffTex = texture( tex, texCoord) * Color;
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
		vec3 Idiff = Diffuse * max(dot(n,vLight), 0.0);

		float fFogCoord = abs(vEyeSpacePos.z/vEyeSpacePos.w);

		vec3 colorLinear = diffTex.rgb * (Ambient + Idiff) + Ispec;

		float ScreenGama = Shared.x;
		vec4 gcc = vec4(pow(colorLinear, vec3(1.0/ScreenGama)), diffTex.a);
		out_frag_color = mix(gcc , fogColor, getFogFactor(fFogCoord));
		gl_FragDepth = gl_FragCoord.z;
	}
}