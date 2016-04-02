using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using GameLib;

namespace Ottd3D
{
	public class Mat4InstancedShader : Tetra.Shader
	{
		public Mat4InstancedShader (string vertResPath, string fragResPath = null, string geomResPath = null)
			: base(vertResPath, fragResPath, geomResPath){}

		public int DiffuseTexture, NormalTexture,DepthTexture;

		protected override void BindSamplesSlots ()
		{
			base.BindSamplesSlots ();
			GL.Uniform1(GL.GetUniformLocation (pgmId, "normal"), 1);
			GL.Uniform1(GL.GetUniformLocation (pgmId, "depth"), 2);
		}
		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			//GL.BindAttribLocation(pgmId, 3, "in_tangent");
			GL.BindAttribLocation(pgmId, 3, "in_weights");
			GL.BindAttribLocation(pgmId, 4, "in_model");
			GL.BindAttribLocation(pgmId, 8, "in_quat0");
			GL.BindAttribLocation(pgmId, 9, "in_quat1");
			GL.BindAttribLocation(pgmId, 10, "in_quat2");
			GL.BindAttribLocation(pgmId, 11, "in_quat3");
			GL.BindAttribLocation(pgmId, 12, "in_bpos0");
			GL.BindAttribLocation(pgmId, 13, "in_bpos1");
			GL.BindAttribLocation(pgmId, 14, "in_bpos2");
			GL.BindAttribLocation(pgmId, 15, "in_bpos3");
		}
		int bonesLoc;
		protected override void GetUniformLocations ()
		{	
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "block_data"), 0);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "fogData"), 1);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "materialData"), 2);

			bonesLoc = GL.GetUniformLocation (pgmId, "bones");
		}
		public void SetBones(float[] bones){
			GL.Uniform3 (bonesLoc, 12, bones);
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