// Realça o comportamento da água: correnteza rápida ganha espuma, água parada
// fica espelhada, e as nascentes marcadas borbulham.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  if (agua > 0.0004) {
    // Declive da superfície: proxy da velocidade da correnteza.
    float e = aguaEm(uv + vec2(u_texel.x, 0.0)) + alturaEm(uv + vec2(u_texel.x, 0.0));
    float o = aguaEm(uv - vec2(u_texel.x, 0.0)) + alturaEm(uv - vec2(u_texel.x, 0.0));
    float n = aguaEm(uv + vec2(0.0, u_texel.y)) + alturaEm(uv + vec2(0.0, u_texel.y));
    float s = aguaEm(uv - vec2(0.0, u_texel.y)) + alturaEm(uv - vec2(0.0, u_texel.y));
    float velocidade = clamp(length(vec2(e - o, n - s)) * 45.0, 0.0, 1.0);

    // Espuma correndo morro abaixo.
    float faixa = ruido(uv * vec2(70.0, 120.0) + vec2(0.0, -tempo * 2.2));
    float espuma = smoothstep(0.62, 0.92, faixa) * velocidade;
    cor.rgb = mix(cor.rgb, vec3(0.95, 0.99, 1.0), espuma * 0.7);

    // Água parada reflete o céu.
    float calma = (1.0 - velocidade) * smoothstep(0.002, 0.01, agua);
    cor.rgb = mix(cor.rgb, vec3(0.55, 0.78, 0.92), calma * 0.35);
  }

  // Nascentes marcadas: anéis concêntricos saindo do ponto.
  float fonte = marcadorEm(uv);
  if (fonte > 0.15) {
    float anel = sin(fonte * 26.0 - tempo * 5.0) * 0.5 + 0.5;
    cor.rgb = mix(cor.rgb, vec3(0.75, 0.95, 1.0), smoothstep(0.15, 0.8, fonte) * anel * 0.6);
  }

  return cor;
}
