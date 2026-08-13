// Cheia cíclica: um nível de inundação sobe e desce, revelando quais partes
// do relevo — e quais casas marcadas — ficam debaixo d'água.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Ciclo de ~34s: enchente lenta, vazante lenta.
  float fase = sin(tempo * 0.185) * 0.5 + 0.5;
  float nivel = mix(0.16, 0.52, fase);
  float cotaMaxima = 0.52;

  // Área de risco: fica abaixo da cheia máxima, mesmo que agora esteja seca.
  float risco = 1.0 - smoothstep(cotaMaxima - 0.01, cotaMaxima + 0.01, altura);
  float listra = step(0.5, fract((uv.x + uv.y) * 55.0));
  cor.rgb = mix(cor.rgb, vec3(0.85, 0.2, 0.18), risco * listra * 0.22);

  // Lâmina de inundação atual.
  float submerso = nivel - altura;
  if (submerso > 0.0) {
    float d = clamp(submerso * 4.0, 0.0, 1.0);
    vec3 corCheia = mix(vec3(0.45, 0.62, 0.55), vec3(0.15, 0.28, 0.42), d);

    // Água barrenta de enchente, com sujeira em movimento.
    float barro = ruido(uv * 70.0 + vec2(tempo * 0.4, -tempo * 0.25));
    corCheia = mix(corCheia, vec3(0.42, 0.33, 0.20), barro * 0.35);

    cor.rgb = mix(cor.rgb, corCheia, clamp(0.55 + d * 0.45, 0.0, 0.95));

    // Linha d'água brilhando na borda da cheia.
    float borda = 1.0 - smoothstep(0.0, 0.008, submerso);
    cor.rgb = mix(cor.rgb, vec3(0.9, 0.95, 1.0), borda * 0.7);
  }

  // Casas marcadas: verdes acima da água, piscando em vermelho quando alagam.
  float casa = marcadorEm(uv);
  if (casa > 0.2) {
    float alagada = step(altura, nivel);
    float alerta = 0.5 + 0.5 * sin(tempo * 7.0);
    vec3 corCasa = mix(vec3(0.95, 0.95, 0.9), vec3(1.0, 0.2, 0.15) * alerta + 0.15, alagada);
    cor.rgb = mix(cor.rgb, corCasa, smoothstep(0.2, 0.6, casa) * 0.85);
  }

  return cor;
}
