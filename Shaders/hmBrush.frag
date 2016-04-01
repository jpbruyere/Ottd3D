#version 330
precision highp float;

in vec2 texCoord;
out vec4 out_frag_color;

uniform sampler2D tex;

uniform float radius;
uniform vec2 center;
uniform vec4 color;

void main ()
{	

	float border = radius * 0.2;
	vec2 uv = texCoord;
	vec4 c = texture( tex, uv);

	uv -= center;

	float dist = sqrt(dot(uv, uv));

	float t = smoothstep(radius, radius - border, dist);

	if (dist > radius)
		gl_FragColor = vec4(c.rgb, 1.0);
	else
		gl_FragColor = vec4( color.rgb * t + c.rgb, 1.0);

} 
