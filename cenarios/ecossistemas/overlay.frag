// Vegetação que nasce onde há altitude adequada e umidade acumulada, e
// espuma na linha onde a água encosta na terra.
float manchas(vec2 p) {
  return fract(sin(dot(floor(p), vec2(41.7, 289.1))) * 21943.13);
}

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Umidade aproximada: água na célula e na vizinhança imediata.
  float umidade = agua
    + aguaEm(uv + vec2(u_texel.x * 3.0, 0.0))
    + aguaEm(uv - vec2(u_texel.x * 3.0, 0.0))
    + aguaEm(uv + vec2(0.0, u_texel.y * 3.0))
    + aguaEm(uv - vec2(0.0, u_texel.y * 3.0));

  float faixa = smoothstep(0.26, 0.34, altura) * (1.0 - smoothstep(0.78, 0.9, altura));
  float densidade = clamp(umidade * 60.0, 0.0, 1.0) * faixa;

  float grao = manchas(uv * 190.0);
  float mata = step(1.0 - densidade * 0.55, grao);
  vec3 verde = mix(vec3(0.22, 0.45, 0.20), vec3(0.11, 0.30, 0.14), grao);
  cor.rgb = mix(cor.rgb, verde, mata * 0.75);

  // Espuma na margem: onde a lâmina d'água é bem fina.
  float margem = smoothstep(0.0003, 0.0009, agua) * (1.0 - smoothstep(0.0009, 0.0025, agua));
  cor.rgb = mix(cor.rgb, vec3(0.92, 0.97, 1.0), margem * 0.5);

  return cor;
}
