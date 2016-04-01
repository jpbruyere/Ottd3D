#version 330			

precision highp float;


layout (std140) uniform fogData
{ 
	vec4 fogColor;
	float fStart; // This is only for linear fog
	float fEnd; // This is only for linear fog
	float fDensity; // For exp and exp2 equation   
	int iEquation; // 0 = linear, 1 = exp, 2 = exp2
};


uniform sampler2D tex;
uniform sampler2D normal;
uniform sampler2D depth;

layout (std140) uniform block_data{
	mat4 Projection;
	mat4 ModelView;
	mat4 Normal;
	vec4 lightPos;
	vec4 Color;
};

in vec2 texCoord;			
in vec4 vEyeSpacePos;
in vec3 n;			

out vec4 out_frag_color;

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

const vec3 diffuse = vec3(0.3, 0.3, 0.3);
const vec3 ambient = vec3(0.01, 0.01, 0.01);
const vec3 specular = vec3(0.0,0.0,0.0);
const float shininess = 1.0;
const float screenGamma = 1.0;

const float zFar=512.0;
const float zNear = 0.1;

void main(void)
{
	float z_b = texture(depth, gl_FragCoord.xy / vec2(1024.0,800.0)).r;
    float z_n = 2.0 * z_b - 1.0;
    float z_e = 2.0 * zNear * zFar / (zFar + zNear - z_n * (zFar - zNear));
    float z = gl_FragCoord.z/gl_FragCoord.w;

    if (z_e < gl_FragCoord.w)
    	discard;
	//if (d==0.01)
	//	discard;

	out_frag_color = vec4(1.0,0.0,0.0,1.0);//texture( tex, texCoord) * Color;

}