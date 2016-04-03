//
//  VertexDispShader.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2015 jp
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
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Tetra;

namespace Ottd3D
{
	public class VertexDispShader : Shader, IDisposable
	{
		public VertexDispShader (string vertResId, string fragResId = null) :
			base(vertResId,fragResId)
		{
		}

		protected int   mapSizeLoc, heightScaleLoc,
						selRadiusLoc, selCenterLoc, selColorLoc;

		public int	DisplacementMap,
					DiffuseTexture,
					SplatTexture,
					ShadowMap;

		Vector2 mapSize;
		float heightScale = 1f;

		//selection
		float selRadius = 0.5f;
		Vector2 selCenter;
		Vector4 selColor = new Vector4(0,0,0,0);

		public Vector2 MapSize {
			set { mapSize = value; }
		}
		public float HeightScale {
			set { heightScale = value; }
		}
		public float SelectionRadius { 
			set { selRadius = value; }
			get { return selRadius; }
		}
		public Vector2 SelectionCenter {
			set { selCenter = value; }
			get { return selCenter; }
		}
		public Vector4 SelectionColor {
			set { selColor = value; }
			get { return selColor; }
		}

		protected override void GetUniformLocations ()
		{
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex(pgmId, "block_data"), 0);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex(pgmId, "fogData"), 1);
			GL.UniformBlockBinding(pgmId, GL.GetUniformBlockIndex (pgmId, "materialData"), 2);



			mapSizeLoc = GL.GetUniformLocation (pgmId, "mapSize");
			heightScaleLoc = GL.GetUniformLocation (pgmId, "heightScale");
			selRadiusLoc = GL.GetUniformLocation(pgmId, "sel_radius");
			selCenterLoc = GL.GetUniformLocation(pgmId, "sel_center");
			selColorLoc = GL.GetUniformLocation(pgmId, "sel_color");
		}
		protected override void BindSamplesSlots ()
		{
			base.BindSamplesSlots ();

			GL.Uniform1(GL.GetUniformLocation (pgmId, "heightMap"),1);
			GL.Uniform1(GL.GetUniformLocation (pgmId, "splatTex"),5);
			GL.Uniform1(GL.GetUniformLocation (pgmId, "shadowTex"),7);
		}
		public override void Enable ()
		{
			base.Enable ();

			GL.Uniform2 (mapSizeLoc, mapSize);
			GL.Uniform1 (heightScaleLoc, heightScale);
			GL.Uniform1(selRadiusLoc, selRadius);
			GL.Uniform2(selCenterLoc, selCenter);
			GL.Uniform4(selColorLoc, selColor);


			GL.ActiveTexture (TextureUnit.Texture5);
			GL.BindTexture(TextureTarget.Texture2D, SplatTexture);
			GL.ActiveTexture (TextureUnit.Texture1);
			GL.BindTexture(TextureTarget.Texture2D, DisplacementMap);
			GL.ActiveTexture (TextureUnit.Texture0);
			GL.BindTexture(TextureTarget.Texture2DArray, DiffuseTexture);
		}

		#region IDisposable implementation
		public override void Dispose ()
		{
			
			base.Dispose ();
		}
		#endregion

	}
}

