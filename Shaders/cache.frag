#version 330
precision lowp float;

uniform sampler2D tex;
uniform sampler2D depthTex;

in vec2 texCoord;
out vec4 out_frag_color;

void main(void)
{
	out_frag_color = texture( tex, texCoord);
	//gl_FragDepth = texture(depthTex, vec3(texCoord, gl_FragCoord.z/gl_FragCoord.w));
	float d = texture(depthTex, texCoord).x;

	gl_FragDepth = d;	
}
