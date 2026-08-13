// Camadas de cor bem separadas (a paleta já entrega as faixas) mais uma forma
// geométrica guia projetada no centro, trocando a cada poucos segundos.

float formaQuadrado(vec2 p) {
  vec2 d = abs(p) - vec2(0.30);
  return max(d.x, d.y);
}

float formaCirculo(vec2 p) {
  return length(p) - 0.32;
}

float formaTriangulo(vec2 p) {
  p.y -= 0.10;
  float d = max(abs(p.x) * 0.866 + p.y * 0.5, -p.y);
  return d - 0.22;
}

float formaEstrela(vec2 p) {
  float a = atan(p.y, p.x);
  float r = length(p);
  return r - (0.20 + 0.12 * cos(a * 5.0));
}

vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  // Contorno das camadas: ajuda a criança a enxergar onde uma cor vira outra.
  float faixa = altura * 7.0;
  float borda = 1.0 - smoothstep(0.0, 1.5, abs(fract(faixa) - 0.5) / max(fwidth(faixa), 1e-5));
  cor.rgb = mix(cor.rgb, vec3(1.0), borda * 0.25);

  // Uma forma por vez, ciclo de 8 segundos.
  vec2 p = (uv - 0.5) * vec2(1.0, u_texel.x / u_texel.y);
  int qual = int(mod(floor(tempo / 8.0), 4.0));
  float d;
  if (qual == 0) d = formaQuadrado(p);
  else if (qual == 1) d = formaTriangulo(p);
  else if (qual == 2) d = formaCirculo(p);
  else d = formaEstrela(p);

  float traco = 1.0 - smoothstep(0.0, 0.012, abs(d));
  float dentro = 1.0 - smoothstep(-0.012, 0.0, d);

  cor.rgb = mix(cor.rgb, cor.rgb * 1.25 + 0.08, dentro * 0.35);
  cor.rgb = mix(cor.rgb, vec3(1.0), traco * 0.9);

  return cor;
}
