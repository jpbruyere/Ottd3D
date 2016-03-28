using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using GameLib;

namespace Ottd3D
{
	public class Mat4InstancedShader : Tetra.Shader
	{
		public Mat4InstancedShader () : base(){}

		public override void Init ()
		{
			vertSource = @"
			#version 330
			precision highp float;

			layout (location = 0) in vec3 in_position;
			layout (location = 1) in vec2 in_tex;
			layout (location = 2) in vec3 in_normal;
			layout (location = 3) in vec3 in_tangent;
			layout (location = 4) in mat4 in_model;

			layout (std140) uniform block_data{
				mat4 Projection;
				mat4 ModelView;
				mat4 Normal;
				vec4 lightPos;
				vec4 Color;
			};

			out vec2 texCoord;			
			out vec3 n;
			out vec3 t;
			out vec4 vEyeSpacePos;
			

			void main(void)
			{				

				texCoord = in_tex;
				n = vec3(Normal * in_model * vec4(in_normal, 0));
				t = vec3(Normal * in_model * vec4(in_tangent, 0));

				vec3 pos = in_position.xyz;

				vEyeSpacePos = ModelView * in_model * vec4(pos, 1);
				
				gl_Position = Projection * ModelView * in_model * vec4(pos, 1);
			}";

			fragSource = @"
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
			in vec3 t;
			
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

			vec3 CalcBumpedNormal()
			{
			    vec3 Normal = normalize(n);
			    vec3 Tangent = normalize(t);
			    Tangent = normalize(Tangent - dot(Tangent, Normal) * Normal);
			    vec3 Bitangent = cross(Tangent, Normal);
			    vec3 BumpMapNormal = texture(normal, texCoord).xyz;
			    BumpMapNormal = 2.0 * BumpMapNormal - vec3(1.0, 1.0, 1.0);
			    vec3 NewNormal;
			    mat3 TBN = mat3(Tangent, Bitangent, Normal);
			    NewNormal = TBN * BumpMapNormal;
			    NewNormal = normalize(NewNormal);
			    return NewNormal;
			}

			const vec3 diffuse = vec3(0.7, 0.7, 0.7);
			const vec3 ambient = vec3(0.01, 0.01, 0.01);
			const vec3 specular = vec3(0.0,0.0,0.0);
			const float shininess = 1.0;
			const float screenGamma = 1.0;

			void main(void)
			{

				vec4 diffTex = texture( tex, texCoord) * Color;
//				if (diffTex.a < 0.1)
//					discard;
				vec3 vLight;
				vec3 vEye = normalize(-vEyeSpacePos.xyz);

				if (lightPos.w == 0.0)
					vLight = normalize(-lightPos.xyz);
				else
					vLight = normalize(lightPos.xyz - vEyeSpacePos.xyz);

				//blinn phong
				vec3 halfDir = normalize(vLight + vEye);
				float specAngle = max(dot(halfDir, n), 0.0);
				vec3 Ispec = specular * pow(specAngle, shininess);
				vec3 Idiff = diffuse * max(dot(n,vLight), 0.0);

				float fFogCoord = abs(vEyeSpacePos.z/vEyeSpacePos.w);

				vec3 colorLinear = diffTex.rgb + diffTex.rgb * (ambient + Idiff) + Ispec;
//				out_frag_color = vec4(colorLinear, diffTex.a);
				vec4 gcc = vec4(pow(colorLinear, vec3(1.0/screenGamma)), diffTex.a);
				out_frag_color = mix(gcc , fogColor, getFogFactor(fFogCoord));

				/*
				out_frag_color = vec4( 
					mix(diffTex.rgb * Idiff , fogColor.rgb, getFogFactor(fFogCoord)),diffTex.a);

				out_frag_color = vec4(diffTex.rgb * Idiff , diffTex.a)*fogColor;
				*/
			}";
						
			base.Init ();
		}
		public int DiffuseTexture, NormalTexture;

		protected override void BindSamplesSlots ()
		{
			base.BindSamplesSlots ();
			GL.Uniform1(GL.GetUniformLocation (pgmId, "normal"), 1);
		}
		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			GL.BindAttribLocation(pgmId, 3, "in_tangent");
			GL.BindAttribLocation(pgmId, Tetra.IndexedVAO.instanceBufferIndex, "in_model");
		}
		int bi1, bi2;
		protected override void GetUniformLocations ()
		{	
			bi1 = GL.GetUniformBlockIndex (pgmId, "block_data");
			bi2 = GL.GetUniformBlockIndex (pgmId, "fogData");
			GL.UniformBlockBinding(pgmId, bi1, 0);
			GL.UniformBlockBinding(pgmId, bi2, 1);
		}	
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
			GL.ActiveTexture (TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, NormalTexture);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, DiffuseTexture);
		}
	}
}