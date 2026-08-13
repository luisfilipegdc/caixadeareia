// Savana viva x deserto rachado, conforme a umidade que o relevo retém.
// A vegetação nasce perto da água; onde ela sumiu, o solo racha.

float umidadePerto(vec2 uv, float alcance) {
  float soma = 0.0;
  for (int i = 0; i < 8; i++) {
    float a = float(i) * 0.785398;
    soma += aguaEm(uv + vec2(cos(a), sin(a)) * u_texel * alcance);
  }
  return soma / 8.0;
}

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  float perto = umidadePerto(uv, 6.0) + agua;
  float verde = clamp(perto * 90.0, 0.0, 1.0) * (1.0 - smoothstep(0.75, 0.95, altura));

  // Capim alto: manchas irregulares, mais densas junto da água.
  float capim = ruido(uv * 90.0) * 0.6 + ruido(uv * 220.0) * 0.4;
  float mata = smoothstep(0.75 - verde * 0.6, 0.95 - verde * 0.5, capim);
  cor.rgb = mix(cor.rgb, mix(vec3(0.42, 0.52, 0.20), vec3(0.20, 0.36, 0.14), capim), mata * verde * 0.85);

  // Acácias esparsas nas cotas médias.
  float copa = smoothstep(0.965, 0.995, ruido(uv * 26.0));
  float faixa = smoothstep(0.25, 0.4, altura) * (1.0 - smoothstep(0.7, 0.85, altura));
  cor.rgb = mix(cor.rgb, vec3(0.16, 0.28, 0.13), copa * faixa * (0.35 + verde * 0.6));

  // Solo rachado onde a savana secou.
  float seco = 1.0 - clamp(perto * 140.0, 0.0, 1.0);
  float celulas = ruido(uv * 55.0);
  float trinca = smoothstep(0.46, 0.5, celulas) * (1.0 - smoothstep(0.5, 0.54, celulas));
  cor.rgb = mix(cor.rgb, cor.rgb * 0.45, trinca * seco * smoothstep(0.2, 0.45, altura));

  // Rebanhos marcados pelo professor, pulsando junto ao chão.
  float rebanho = marcadorEm(uv);
  if (rebanho > 0.25) {
    float vivo = 0.55 + 0.45 * sin(tempo * 2.6 + uv.x * 30.0);
    vec3 corRebanho = mix(vec3(0.85, 0.75, 0.55), vec3(0.35, 0.22, 0.12), vivo);
    cor.rgb = mix(cor.rgb, corRebanho, smoothstep(0.25, 0.7, rebanho) * 0.8);
  }

  return cor;
}
