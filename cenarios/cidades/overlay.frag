// Reproduz a atividade do material da FURB: os alunos fundam cidades e o
// sistema mostra quais respeitam a área de proteção ambiental às margens da
// água — círculo branco para regular, X vermelho para irregular.

// Quanto de água existe na vizinhança: define a faixa protegida (mata ciliar).
float proximidadeDaAgua(vec2 uv) {
  float maior = aguaEm(uv);
  for (int i = 0; i < 12; i++) {
    float a = float(i) * 0.5236;
    for (float r = 3.0; r <= 9.0; r += 3.0) {
      maior = max(maior, aguaEm(uv + vec2(cos(a), sin(a)) * u_texel * r));
    }
  }
  return maior;
}

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  float perto = proximidadeDaAgua(uv);
  float protegida = smoothstep(0.0004, 0.0025, perto);

  // Faixa de proteção pulsando de leve, para o aluno enxergar o limite.
  float respiro = 0.75 + 0.25 * sin(tempo * 1.5);
  cor.rgb = mix(cor.rgb, vec3(0.35, 0.85, 0.45), protegida * 0.35 * respiro);

  float cidade = marcadorEm(uv);
  if (cidade > 0.12) {
    float nucleo = smoothstep(0.12, 0.55, cidade);

    // Malha urbana: quarteirões desenhados sobre o terreno.
    vec2 grade = fract(uv * 90.0);
    float ruas = step(0.85, max(grade.x, grade.y));
    cor.rgb = mix(cor.rgb, mix(vec3(0.55, 0.53, 0.5), vec3(0.85, 0.85, 0.82), ruas), nucleo * 0.7);

    // Símbolo de conformidade desenhado no próprio campo do marcador, para
    // ficar sempre centrado onde o aluno clicou.
    float irregular = protegida;

    // Anel branco: ocupação regular.
    float anel = 1.0 - smoothstep(0.04, 0.11, abs(cidade - 0.55));

    // Irregular: anel vermelho mais grosso com o miolo pulsando.
    float alerta = 0.55 + 0.45 * sin(tempo * 5.0);
    float miolo = smoothstep(0.72, 0.95, cidade) * alerta;
    float marca = mix(anel, max(anel, miolo), irregular);

    vec3 corMarca = mix(vec3(1.0), vec3(1.0, 0.22, 0.18), irregular);
    cor.rgb = mix(cor.rgb, corMarca, marca * nucleo * 0.9);
  }

  return cor;
}
