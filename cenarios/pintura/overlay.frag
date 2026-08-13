// Modo livre: a paleta gira lentamente e o relevo ganha contornos suaves,
// como uma aquarela que responde às mãos das crianças.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Deslocamento lento da cor pela altura: a mesma montanha muda de tom.
  float giro = sin(tempo * 0.25 + altura * 6.2831) * 0.5 + 0.5;
  vec3 complementar = cor.gbr;
  cor.rgb = mix(cor.rgb, complementar, giro * 0.3);

  // Bordas de aquarela nas transições de altura.
  float faixa = altura * 9.0;
  float contorno = 1.0 - smoothstep(0.0, 1.8, abs(fract(faixa) - 0.5) / max(fwidth(faixa), 1e-5));
  cor.rgb = mix(cor.rgb, cor.rgb * 0.55 + 0.25, contorno * 0.5);

  // Granulado de papel, para não parecer plástico.
  cor.rgb *= 0.93 + 0.14 * ruido(uv * 300.0);

  // Brilho suave acompanhando quem está mexendo na areia agora.
  float e = alturaEm(uv + vec2(u_texel.x, 0.0));
  float o = alturaEm(uv - vec2(u_texel.x, 0.0));
  float relevo = clamp(abs(e - o) * 40.0, 0.0, 1.0);
  cor.rgb += vec3(0.18, 0.16, 0.22) * relevo;

  return cor;
}
