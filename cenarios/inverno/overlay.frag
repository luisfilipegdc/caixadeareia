// Neve que só gruda onde a encosta é suave, e água parada que vira gelo.

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Declividade do terreno: em paredão a neve escorrega.
  float e = alturaEm(uv + vec2(u_texel.x, 0.0));
  float o = alturaEm(uv - vec2(u_texel.x, 0.0));
  float n = alturaEm(uv + vec2(0.0, u_texel.y));
  float s = alturaEm(uv - vec2(0.0, u_texel.y));
  float declive = length(vec2(e - o, n - s)) * 22.0;

  float acumulo = smoothstep(0.3, 0.75, altura) * (1.0 - smoothstep(0.35, 0.9, declive));
  float floco = ruido(uv * 180.0) * 0.5 + ruido(uv * 60.0) * 0.5;
  cor.rgb = mix(cor.rgb, vec3(0.97, 0.98, 1.0), acumulo * (0.55 + floco * 0.45));

  // Brilho de cristal na neve mais espessa.
  float cintila = smoothstep(0.985, 1.0, ruido(uv * 320.0 + floor(tempo * 3.0)));
  cor.rgb += vec3(0.6, 0.7, 0.85) * cintila * acumulo;

  // Rocha exposta onde a encosta é íngreme demais.
  cor.rgb = mix(cor.rgb, cor.rgb * 0.62, smoothstep(0.6, 1.2, declive) * 0.7);

  if (agua > 0.0006) {
    // Água parada congela; água correndo continua líquida.
    float ePar = aguaEm(uv + vec2(u_texel.x, 0.0)) + e;
    float oPar = aguaEm(uv - vec2(u_texel.x, 0.0)) + o;
    float velocidade = clamp(abs(ePar - oPar) * 60.0, 0.0, 1.0);
    float gelo = (1.0 - velocidade) * smoothstep(0.001, 0.004, agua);

    vec3 corGelo = mix(vec3(0.72, 0.87, 0.93), vec3(0.85, 0.94, 0.98), ruido(uv * 45.0));
    cor.rgb = mix(cor.rgb, corGelo, gelo * 0.8);

    // Rachaduras na crosta de gelo.
    float trinca = ruido(uv * 38.0);
    float linha = smoothstep(0.47, 0.5, trinca) * (1.0 - smoothstep(0.5, 0.53, trinca));
    cor.rgb = mix(cor.rgb, vec3(0.55, 0.72, 0.82), linha * gelo * 0.8);
  }

  return cor;
}
