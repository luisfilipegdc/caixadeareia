// A "água" do simulador vira lava: emissiva, com crosta escura esfriando nas
// bordas e brasas nas partes altas da rocha.
vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  if (agua > 0.0004) {
    float quente = clamp(agua * 22.0, 0.0, 1.0);
    vec3 lava = mix(vec3(1.0, 0.85, 0.25), vec3(0.85, 0.12, 0.05), quente);

    // Crosta: nas lâminas mais finas a lava já escureceu.
    float crosta = smoothstep(0.004, 0.0009, agua);
    lava = mix(lava, vec3(0.18, 0.07, 0.06), crosta * 0.8);

    float pulso = 0.85 + 0.15 * sin(tempo * 3.0 + uv.y * 40.0);
    cor.rgb = mix(cor.rgb, lava * pulso, clamp(0.4 + quente, 0.0, 1.0));
    cor.rgb += vec3(0.35, 0.12, 0.0) * quente;
  }

  // Brasas apenas no alto do cone, contidas para não lavar a imagem inteira.
  float brasa = smoothstep(0.9, 1.0, altura);
  cor.rgb += vec3(0.45, 0.12, 0.02) * brasa * (0.45 + 0.35 * sin(tempo * 1.7));

  return cor;
}
