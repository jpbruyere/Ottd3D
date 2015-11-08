//
//  SkyBox.cs
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
using OpenTK.Graphics.OpenGL;
using System.Runtime.InteropServices;

namespace Ottd3D
{
	public class SkyBox
	{
		int vbo_cube_vertices;
		int ibo_cube_indices;

		public SkyBox ()
		{
			// cube vertices for vertex buffer object
			float[] cube_vertices = new float[] {
				-1.0f,  1.0f,  1.0f,
				-1.0f, -1.0f,  1.0f,
				1.0f, -1.0f,  1.0f,
				1.0f,  1.0f,  1.0f,
				-1.0f,  1.0f, -1.0f,
				-1.0f, -1.0f, -1.0f,
				1.0f, -1.0f, -1.0f,
				1.0f,  1.0f, -1.0f,
			};
			vbo_cube_vertices = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ArrayBuffer, vbo_cube_vertices);
			GL.BufferData(BufferTarget.ArrayBuffer,cube_vertices.Length * sizeof(float), cube_vertices,BufferUsageHint.StaticDraw);
			//glBindBuffer(GL_ARRAY_BUFFER, 0);

			// cube indices for index buffer object
			ushort[] cube_indices = new ushort[] {
				0, 1, 2, 3,
				3, 2, 6, 7,
				7, 6, 5, 4,
				4, 5, 1, 0,
				0, 3, 7, 4,
				1, 2, 6, 5,
			};

			ibo_cube_indices = GL.GenBuffer();
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, ibo_cube_indices);
			GL.BufferData(BufferTarget.ElementArrayBuffer, cube_indices.Length * sizeof(ushort), cube_indices, BufferUsageHint.StaticDraw);
			GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
		}
	}
}

