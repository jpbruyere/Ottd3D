//
//  CircleShader.cs
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
using GameLib;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace Ottd3D
{
	public class CircleShader : ShadedTexture
	{
		public CircleShader (string effectId, int _width = -1, int _height = -1, int initTex = 0)
			: base(effectId, _width, _height, initTex)
		{}
		int radiusLoc, centerLoc, colorLoc;

		float radius = 0.5f;
		Vector2 center;
		Vector4 color = new Vector4(1,1,1,1);

		public float Radius { 
			set { radius = value; }
			get { return radius; }
		}
		public Vector2 Center {
			set { center = value; }
			get { return center; }
		}
		public Vector4 Color {
			set { color = value; }
			get { return color; }
		}


		protected override void GetUniformLocations ()
		{
			base.GetUniformLocations ();

			radiusLoc = GL.GetUniformLocation(pgmId, "radius");
			centerLoc = GL.GetUniformLocation(pgmId, "center");
			colorLoc = GL.GetUniformLocation(pgmId, "color");
		}
		public override void Enable ()
		{
			base.Enable ();
			GL.Uniform1(radiusLoc, radius);
			GL.Uniform2(centerLoc, center);
			GL.Uniform4(colorLoc, color);
		}
	}
}

