// Mar com ondas que quebram na linha de costa. O "nível do mar" é uma altura
// fixa da paleta, então a costa se redesenha conforme os alunos movem a areia.

const float NIVEL_DO_MAR = 0.30;

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  float profundidade = NIVEL_DO_MAR - altura;

  if (profundidade > 0.0) {
    // Ondulação do mar aberto, mais forte onde é fundo.
    float onda = sin(uv.x * 42.0 + tempo * 1.6) * 0.5 + 0.5;
    onda *= sin(uv.y * 31.0 - tempo * 1.1) * 0.5 + 0.5;
    cor.rgb += vec3(0.05, 0.09, 0.12) * onda * smoothstep(0.0, 0.12, profundidade);

    // Rebentação: espuma na faixa rasa junto da praia.
    float raso = 1.0 - smoothstep(0.0, 0.035, profundidade);
    float rebentacao = smoothstep(0.55, 0.95, ruido(vec2(uv.x * 60.0, uv.y * 60.0 - tempo * 2.5)));
    cor.rgb = mix(cor.rgb, vec3(0.95, 0.99, 1.0), raso * rebentacao * 0.75);
  } else {
    // Terra firme: um grão de vegetação para não ficar chapado.
    cor.rgb *= 0.94 + 0.12 * ruido(uv * 150.0);
  }

  // Linha de costa marcada, para o aluno enxergar o contorno da ilha.
  float costa = 1.0 - smoothstep(0.0, 0.006, abs(profundidade));
  cor.rgb = mix(cor.rgb, vec3(1.0, 0.98, 0.88), costa * 0.6);

  return cor;
}
