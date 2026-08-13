// Superfície marciana: poeira fina em movimento, crateras marcadas nas áreas
// planas e sondas pousadas pelos alunos.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Regolito: granulação fina por toda a superfície.
  cor.rgb *= 0.9 + 0.2 * ruido(uv * 240.0);

  // Crateras: anéis escuros com borda clara, nas partes mais planas.
  float e = alturaEm(uv + vec2(u_texel.x, 0.0));
  float o = alturaEm(uv - vec2(u_texel.x, 0.0));
  float n = alturaEm(uv + vec2(0.0, u_texel.y));
  float s = alturaEm(uv - vec2(0.0, u_texel.y));
  float plano = 1.0 - clamp(length(vec2(e - o, n - s)) * 30.0, 0.0, 1.0);

  vec2 celula = uv * 9.0;
  vec2 centro = floor(celula) + 0.5;
  float sorteio = aleatorio(floor(celula));
  float raio = 0.16 + sorteio * 0.2;
  float d = length(celula - centro) - raio;

  float bacia = 1.0 - smoothstep(-0.06, 0.0, d);
  float borda = 1.0 - smoothstep(0.0, 0.05, abs(d));
  float existe = step(0.55, sorteio) * plano;

  cor.rgb = mix(cor.rgb, cor.rgb * 0.62, bacia * existe * 0.8);
  cor.rgb = mix(cor.rgb, cor.rgb * 1.3 + 0.05, borda * existe * 0.7);

  // Tempestade de poeira atravessando a cena.
  float poeira = ruido(uv * vec2(6.0, 14.0) + vec2(tempo * 0.22, 0.0));
  cor.rgb = mix(cor.rgb, vec3(0.78, 0.55, 0.36), smoothstep(0.62, 1.0, poeira) * 0.22);

  // Sondas: um ponto metálico com painéis e luz piscando.
  float sonda = marcadorEm(uv);
  if (sonda > 0.3) {
    float corpo = smoothstep(0.3, 0.75, sonda);
    cor.rgb = mix(cor.rgb, vec3(0.72, 0.74, 0.78), corpo * 0.85);
    float luz = step(0.9, fract(tempo * 1.3));
    cor.rgb = mix(cor.rgb, vec3(0.3, 0.9, 1.0), corpo * luz * 0.7);
  }

  return cor;
}
