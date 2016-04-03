using System;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using OpenTK;
using GameLib;

namespace Ottd3D
{
	public class DeformablesShader : InstancedShader
	{
		public DeformablesShader (string vertResPath, string fragResPath = null, string geomResPath = null)
			: base(vertResPath, fragResPath, geomResPath){}

		int bonesLoc;

		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 3, "in_weights");

			GL.BindAttribLocation(pgmId, 8, "in_quat0");
			GL.BindAttribLocation(pgmId, 9, "in_quat1");
			GL.BindAttribLocation(pgmId, 10, "in_quat2");
			GL.BindAttribLocation(pgmId, 11, "in_quat3");
			GL.BindAttribLocation(pgmId, 12, "in_bpos0");
			GL.BindAttribLocation(pgmId, 13, "in_bpos1");
			GL.BindAttribLocation(pgmId, 14, "in_bpos2");
			GL.BindAttribLocation(pgmId, 15, "in_bpos3");
		}

		protected override void GetUniformLocations ()
		{
			base.GetUniformLocations ();
			bonesLoc = GL.GetUniformLocation (pgmId, "bones");
		}
		public void SetBones(float[] bones){
			GL.Uniform3 (bonesLoc, 12, bones);
		}
	}
}