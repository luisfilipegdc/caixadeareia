// Jogo do jardineiro: onde há semente marcada e água por perto, a planta
// cresce e floresce; sem água, ela murcha e fica marrom.

float umidade(vec2 uv) {
  float soma = aguaEm(uv);
  for (int i = 0; i < 6; i++) {
    float a = float(i) * 1.047;
    soma += aguaEm(uv + vec2(cos(a), sin(a)) * u_texel * 5.0);
  }
  return soma / 7.0;
}

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  float semente = marcadorEm(uv);
  if (semente <= 0.08) {
    // Solo nu: só um grão de terra para não ficar chapado.
    cor.rgb *= 0.92 + 0.16 * ruido(uv * 160.0);
    return cor;
  }

  float agu = clamp(umidade(uv) * 120.0, 0.0, 1.0);
  float vigor = smoothstep(0.08, 0.6, semente) * agu;

  // Copa: quanto mais vigor, maior e mais verde.
  float raio = smoothstep(0.55 - vigor * 0.4, 0.15, 1.0 - semente);
  float folhas = ruido(uv * 130.0 + tempo * 0.15);
  vec3 viva = mix(vec3(0.25, 0.55, 0.22), vec3(0.13, 0.36, 0.16), folhas);
  vec3 murcha = mix(vec3(0.45, 0.36, 0.18), vec3(0.32, 0.24, 0.12), folhas);
  vec3 planta = mix(murcha, viva, vigor);

  cor.rgb = mix(cor.rgb, planta, raio * 0.9);

  // Flores só aparecem quando a planta está bem regada.
  float flor = smoothstep(0.985, 1.0, ruido(uv * 210.0)) * smoothstep(0.6, 0.95, vigor);
  cor.rgb = mix(cor.rgb, vec3(0.95, 0.72, 0.85), flor);

  // Barra de saúde: um halo que vai do vermelho ao verde.
  float halo = smoothstep(0.1, 0.2, semente) * (1.0 - smoothstep(0.2, 0.32, semente));
  vec3 corSaude = mix(vec3(0.9, 0.25, 0.2), vec3(0.3, 0.85, 0.35), vigor);
  cor.rgb = mix(cor.rgb, corSaude, halo * 0.55);

  return cor;
}
