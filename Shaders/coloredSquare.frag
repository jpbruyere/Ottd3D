#version 330
precision highp float;

uniform vec4 color;

in vec2 texCoord;
out vec4 out_frag_color;

void main()
{
	out_frag_color = color;
}

