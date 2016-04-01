//
//  ShaderEditorWindow.cs
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
using Crow;
using System.Xml.Serialization;
using System.ComponentModel;
using Tetra;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;

namespace Ottd3D
{
	[DefaultStyle("#Crow.Styles.Window.style")]
	[DefaultTemplate("#Ottd3D.ui.tmpWindow.crow")]
	public class ShaderEditorWindow : Window
	{
		public List<Tetra.Shader> Shaders {
			get { return Tetra.Shader.RegisteredShaders; }
		}
		Tetra.Shader editedShader;
		[XmlAttributeAttribute()][DefaultValue(null)]
		public virtual Tetra.Shader EditedShader {
			get { return editedShader; }
			set {
				if (editedShader == value)
					return;
				editedShader = value; 
				NotifyValueChanged ("EditedShader", editedShader);
				NotifyValueChanged ("ShaderSource", ShaderSource);
			}
		}
		ShaderType shaderType;
		[XmlAttributeAttribute()][DefaultValue(ShaderType.VertexShader)]
		public virtual ShaderType ShaderType {
			get { return shaderType; }
			set {
				if (shaderType == value)
					return;
				shaderType = value; 
				NotifyValueChanged ("ShaderType", shaderType);
				NotifyValueChanged ("ShaderSource", ShaderSource);
			}
		} 
		public string ShaderSource {
			get { return EditedShader == null ? "" : EditedShader.GetSource(ShaderType); }
			set {
				if (EditedShader == null)
					return;
				if (string.Equals (value, ShaderSource, StringComparison.Ordinal))
					return;
				EditedShader.SetSource(ShaderType, value);
				NotifyValueChanged ("ShaderSource", value);
			}
		}
		void onApplyShader (object sender, EventArgs e){
			if (EditedShader != null)
				EditedShader.Compile ();
		}
		public ShaderEditorWindow () : base()
		{
		}

		void onShaderSelect (object sender, SelectionChangeEventArgs e)
		{
			EditedShader = e.NewValue as Tetra.Shader;			
		}
		void onChangeShaderType (object sender, EventArgs e){
			RadioButton rb = sender as RadioButton;
			switch (rb.Caption) {
			case "VS":
				ShaderType = ShaderType.VertexShader;
				break;
			case "FS":
				ShaderType = ShaderType.FragmentShader;
				break;
			case "GS":
				ShaderType = ShaderType.GeometryShader;
				break;
			} 
		}

	}
}

