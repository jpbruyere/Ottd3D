#version 330
precision highp float;

in vec2 texCoord;
out vec4 out_frag_color;

uniform sampler2D tex;

uniform float radius;
uniform vec2 center;
uniform vec4 color;

vec4 bkg_color = vec4(0.0, 0.0, 0.0, 0.0);
   

void main ()
{	
	float border = radius * 0.5;
	vec2 uv = texCoord;
	vec4 c = texture( tex, uv);

	uv -= center;

	float dist = sqrt(dot(uv, uv));

	float t = smoothstep(radius, radius * 0.5, dist);

	if (dist > radius)
		gl_FragColor = vec4(c.rgb, 1.0);
	else{
		int texPtr = int(color.r * 255.0);
		int shift = texPtr * 4;

		uint C = uint(c.r * 0xFF)
				+ (uint(c.g * 0xFF)<<8)
				+ (uint(c.b * 0xFF)<<16);

		uint CT = (C >> shift) & 0xFu;
		uint CTInc = min(uint(color.g * 0xF),0xFu);
		if (CT + CTInc > 0xFu)
			CTInc = 0xFu - CT;
		CTInc = CTInc << shift;
		vec4 inc = vec4(0.0,0.0,0.0,0.0);
		inc.r = float(CTInc & 0xFFu)/0xFF;
		inc.g = float((CTInc & 0xFF00u)>>8)/0xFF;
		inc.b = float((CTInc & 0xFF0000u)>>16)/0xFF;


		gl_FragColor = inc+c;
	}
} 
