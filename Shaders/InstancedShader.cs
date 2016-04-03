//
//  InstancedShader.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2016 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using OpenTK.Graphics.OpenGL;

namespace Ottd3D
{
	public class InstancedShader : Tetra.Shader
	{
		public InstancedShader (string vertResPath, string fragResPath = null, string geomResPath = null)
			: base(vertResPath, fragResPath, geomResPath){}

		public int DiffuseTexture, NormalTexture, ShadowMap;

		protected override void BindSamplesSlots ()
		{
			base.BindSamplesSlots ();

			GL.Uniform1(GL.GetUniformLocation (pgmId, "normal"), 1);
			GL.Uniform1(GL.GetUniformLocation (pgmId, "depth"), 2);
			GL.Uniform1(GL.GetUniformLocation (pgmId, "shadowTex"),7);
		}
		protected override void BindVertexAttributes ()
		{
			base.BindVertexAttributes ();

			GL.BindAttribLocation(pgmId, 2, "in_normal");
			//GL.BindAttribLocation(pgmId, 3, "in_tangent");
			GL.BindAttribLocation(pgmId, 4, "in_model");
		}
		protected override void GetUniformLocations ()
		{	
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "block_data"), 0);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "fogData"), 1);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "materialData"), 2);
		}
		public override void Enable ()
		{
			GL.UseProgram (pgmId);
			GL.ActiveTexture (TextureUnit.Texture7);
			GL.BindTexture(TextureTarget.Texture2D, ShadowMap);
			GL.ActiveTexture (TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, NormalTexture);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2D, DiffuseTexture);
		}
	}
}

