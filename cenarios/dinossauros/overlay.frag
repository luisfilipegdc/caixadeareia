// Paisagem jurássica: vegetação densa no calor úmido, pegadas no barro da
// beira d'água e ninhos marcados chocando nas partes altas.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Samambaias e coníferas gigantes, densas nas cotas baixas e úmidas.
  float umido = clamp((agua + aguaEm(uv + u_texel * 4.0) + aguaEm(uv - u_texel * 4.0)) * 50.0, 0.0, 1.0);
  float faixa = 1.0 - smoothstep(0.35, 0.8, altura);
  float mata = smoothstep(0.55, 0.9, ruido(uv * 75.0)) * faixa;
  cor.rgb = mix(cor.rgb, mix(vec3(0.16, 0.33, 0.15), vec3(0.09, 0.22, 0.12), ruido(uv * 200.0)), mata * 0.7);

  // Copas maiores, espalhadas.
  float copa = smoothstep(0.96, 0.995, ruido(uv * 22.0)) * faixa;
  cor.rgb = mix(cor.rgb, vec3(0.10, 0.26, 0.13), copa * 0.85);

  // Pegadas: só onde o barro está mole, na margem do pântano.
  float margem = smoothstep(0.0002, 0.0012, agua) * (1.0 - smoothstep(0.0015, 0.005, agua));
  vec2 celula = uv * vec2(26.0, 34.0);
  vec2 dentro = fract(celula) - 0.5;
  float sequencia = aleatorio(floor(celula));
  float passo = smoothstep(0.34, 0.2, length(dentro * vec2(1.0, 0.65)));
  float pegada = passo * step(0.82, sequencia) * margem;
  cor.rgb = mix(cor.rgb, cor.rgb * 0.5, pegada * 0.8);

  // Vapor quente subindo do pântano.
  float vapor = smoothstep(0.7, 1.0, ruido(uv * 18.0 + vec2(0.0, -tempo * 0.35))) * umido;
  cor.rgb = mix(cor.rgb, vec3(0.75, 0.82, 0.72), vapor * 0.2);

  // Ninhos: ovos claros em uma cova de areia.
  float ninho = marcadorEm(uv);
  if (ninho > 0.2) {
    float cova = smoothstep(0.2, 0.5, ninho);
    cor.rgb = mix(cor.rgb, vec3(0.55, 0.47, 0.34), cova * 0.7);
    float ovos = smoothstep(0.9, 1.0, ruido(uv * 260.0));
    float choco = 0.75 + 0.25 * sin(tempo * 2.2 + uv.y * 50.0);
    cor.rgb = mix(cor.rgb, vec3(0.93, 0.90, 0.78) * choco, ovos * cova);
  }

  return cor;
}
