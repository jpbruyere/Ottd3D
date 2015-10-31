using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using GameLib;

namespace Ottd3D
{
	public class SingleLightShader : Shader
	{
		public SingleLightShader ()
		{
			vertSource = @"
			#version 330

			precision highp float;

			uniform mat4 Projection;
			uniform mat4 ModelView;
			uniform mat4 Model;
			uniform mat4 Normal;

			uniform vec4 lightPos;
			uniform float heightScale;
			uniform vec2 mapSize;
			
			uniform sampler2D heightMap;

			in vec3 in_position;
			in vec2 in_tex;
			in vec3 in_normal;
			out vec2 texCoord;
			out vec3 v;
			out vec3 N;
			out vec3 lPos;
			out vec4 vPos;
			

			void main(void)
			{				

				texCoord = in_tex;
				N = normalize(vec3(Normal * Model * vec4(in_normal, 0)));


				vec3 pos = in_position;
				pos.x += mod (gl_InstanceID, 100.0);
				pos.y += floor(gl_InstanceID/100.0);

				vec4 hm0 = texture2D(heightMap, (Model * vec4(pos, 1.0)).xy / mapSize);
				pos.z += hm0.g * heightScale;


				v = vec3(ModelView * Model * vec4(pos, 0));

				lPos = vec3(ModelView * lightPos);
				gl_Position = Projection * ModelView * Model * vec4(pos, 1);
			}";

			fragSource = @"
			#version 330
			precision highp float;

			uniform vec4 color;
			uniform sampler2D tex;


			in vec2 texCoord;			
			in vec3 v;
			in vec3 N;
			in vec3 lPos;
			
			
			out vec4 out_frag_color;

			void main(void)
			{
				vec3 L = normalize(lPos-v);
				float NdotL = dot(N, L);
				if ( NdotL < 0.0) // light source on the wrong side?   
					NdotL = dot(-N, L);
   				vec3 Idiff = vec3(1.0,1.0,1.0) * max(NdotL, 0.0);  
   				Idiff = clamp(Idiff, 0.0, 1.0);    
				vec4 diffTex = texture( tex, texCoord);

				out_frag_color =  vec4(color.rgb * Idiff ,1.0) * color; 
			}";
			Compile ();
		}

		protected int mapSizeLoc, lightPosLocation, heightScaleLoc;

		public int DisplacementMap;
		public int DiffuseTexture;

		Vector4 lightPos;
		float heightScale = 1f;
		Vector2 mapSize;

		public Vector2 MapSize {
			set { mapSize = value; }
		}
		public Vector4 LightPos {
			set { lightPos = value; }
		}
		public float HeightScale {
			set { heightScale = value; }
		}
		protected override void BindSamplesSlots ()
		{
			base.BindSamplesSlots ();
			GL.Uniform1(GL.GetUniformLocation (pgmId, "heightMap"), 1);
		}
		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
		}
		protected override void GetUniformLocations ()
		{
			base.GetUniformLocations ();

			mapSizeLoc = GL.GetUniformLocation (pgmId, "mapSize");
			heightScaleLoc = GL.GetUniformLocation (pgmId, "heightScale");
			lightPosLocation = GL.GetUniformLocation (pgmId, "lightPos");
		}	
		public override void Enable ()
		{
			base.Enable ();

			GL.Uniform2 (mapSizeLoc, mapSize);
			GL.Uniform4 (lightPosLocation, lightPos);
			GL.Uniform1 (heightScaleLoc, heightScale);

			GL.ActiveTexture (TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, DisplacementMap);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2DArray, DiffuseTexture);
		}
	}
}

