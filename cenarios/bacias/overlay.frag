// Realça os divisores de água: pontos onde o relevo é convexo nas duas
// direções, ou seja, cumes de onde a chuva escorre para os dois lados.
vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  float e = alturaEm(uv + vec2(u_texel.x, 0.0));
  float o = alturaEm(uv - vec2(u_texel.x, 0.0));
  float n = alturaEm(uv + vec2(0.0, u_texel.y));
  float s = alturaEm(uv - vec2(0.0, u_texel.y));

  // Laplaciano: negativo em cume, positivo em vale.
  float curvatura = (e + o + n + s) - 4.0 * altura;
  float cume = smoothstep(0.0006, 0.004, -curvatura) * smoothstep(0.25, 0.5, altura);

  float pulso = 0.75 + 0.25 * sin(tempo * 2.0);
  cor.rgb = mix(cor.rgb, vec3(1.0), cume * 0.65 * pulso);

  return cor;
}
