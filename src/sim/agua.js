// Simulação de água pelo método dos tubos virtuais (Mei et al.), o mesmo
// princípio usado pelo SARndbox: cada célula troca vazão com as quatro
// vizinhas conforme a diferença de altura total (terreno + água).
//
// Tudo roda em shaders sobre texturas de ponto flutuante. As unidades são
// normalizadas: a célula mede 1, e a altura do terreno vive em [0, 1].

import { criarPrograma, PingPong, Textura, Alvo } from '../gl/gl.js';

const FS_FLUXO = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;

uniform sampler2D u_terreno;
uniform sampler2D u_agua;
uniform sampler2D u_fluxo;
uniform vec2 u_texel;
uniform float u_dt;
uniform float u_gravidade;
uniform float u_amortecimento;

float alturaTotal(vec2 uv) {
  return texture(u_terreno, uv).r + texture(u_agua, uv).r;
}

void main() {
  vec2 uv = v_uv;
  float h = alturaTotal(uv);
  vec4 fluxo = texture(u_fluxo, uv);

  // x = esquerda, y = direita, z = cima (+v), w = baixo (-v)
  vec4 delta = vec4(
    h - alturaTotal(uv + vec2(-u_texel.x, 0.0)),
    h - alturaTotal(uv + vec2( u_texel.x, 0.0)),
    h - alturaTotal(uv + vec2(0.0,  u_texel.y)),
    h - alturaTotal(uv + vec2(0.0, -u_texel.y))
  );

  vec4 novo = max(vec4(0.0), fluxo * u_amortecimento + u_dt * u_gravidade * delta);

  // As bordas da caixa são paredes: nada escoa para fora do domínio.
  if (uv.x - u_texel.x < 0.0) novo.x = 0.0;
  if (uv.x + u_texel.x > 1.0) novo.y = 0.0;
  if (uv.y + u_texel.y > 1.0) novo.z = 0.0;
  if (uv.y - u_texel.y < 0.0) novo.w = 0.0;

  // Nenhuma célula pode enviar mais água do que possui neste passo.
  float disponivel = texture(u_agua, uv).r;
  float soma = novo.x + novo.y + novo.z + novo.w;
  if (soma > 0.0) {
    novo *= min(1.0, disponivel / (soma * u_dt));
  }

  saida = novo;
}`;

const FS_ALTURA = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;

uniform sampler2D u_agua;
uniform sampler2D u_fluxo;
uniform sampler2D u_chuva;
uniform vec2 u_texel;
uniform float u_dt;
uniform float u_taxaChuva;
uniform float u_evaporacao;

void main() {
  vec2 uv = v_uv;
  vec4 saiu = texture(u_fluxo, uv);

  // O que cada vizinho manda na minha direção.
  float entrou =
      texture(u_fluxo, uv + vec2(-u_texel.x, 0.0)).y
    + texture(u_fluxo, uv + vec2( u_texel.x, 0.0)).x
    + texture(u_fluxo, uv + vec2(0.0,  u_texel.y)).w
    + texture(u_fluxo, uv + vec2(0.0, -u_texel.y)).z;

  float total = saiu.x + saiu.y + saiu.z + saiu.w;
  float d = texture(u_agua, uv).r + u_dt * (entrou - total);

  d += texture(u_chuva, uv).r * u_taxaChuva * u_dt;
  d -= u_evaporacao * u_dt;

  saida = vec4(max(d, 0.0), 0.0, 0.0, 1.0);
}`;

export class SimulacaoDeAgua {
  constructor(gl, quad, largura, altura) {
    this.gl = gl;
    this.quad = quad;
    this.largura = largura;
    this.altura = altura;
    this.texel = [1 / largura, 1 / altura];

    const escalarOpts = {
      largura, altura,
      formatoInterno: gl.R32F, formato: gl.RED, tipo: gl.FLOAT,
      filtro: gl.LINEAR,
    };
    const vetorOpts = {
      largura, altura,
      formatoInterno: gl.RGBA32F, formato: gl.RGBA, tipo: gl.FLOAT,
      filtro: gl.NEAREST,
    };

    this.profundidade = new PingPong(gl, escalarOpts);
    this.fluxo = new PingPong(gl, vetorOpts);
    this.chuva = new Textura(gl, { ...escalarOpts, filtro: gl.LINEAR });
    this.alvoChuva = new Alvo(gl, this.chuva);

    this.progFluxo = criarPrograma(gl, FS_FLUXO);
    this.progAltura = criarPrograma(gl, FS_ALTURA);

    this.gravidade = 0.9;
    this.amortecimento = 0.985;
    this.taxaChuva = 0.0;
    this.evaporacao = 0.004;

    this.zerar();
  }

  get textura() { return this.profundidade.leitura; }

  zerar() {
    this.profundidade.limpar();
    this.fluxo.limpar();
    this.alvoChuva.limpar();
  }

  // `mascara` é uma Textura R32F com 1 onde deve chover (mãos, comando do
  // professor, nascentes de um cenário) e 0 no resto.
  definirMascaraDeChuva(textura) {
    this.mascaraExterna = textura;
  }

  encher(quantidade) {
    // Um "dilúvio" instantâneo: reaproveita o passo de altura com chuva alta.
    this.pulsoDeChuva = quantidade;
  }

  passo(terreno, dt) {
    const gl = this.gl;
    const mascara = this.mascaraExterna ?? this.chuva;

    this.fluxo.escrita.ligar();
    this.progFluxo.usar().setTodos({
      u_terreno: terreno,
      u_agua: this.profundidade.leitura,
      u_fluxo: this.fluxo.leitura,
      u_texel: this.texel,
      u_dt: dt,
      u_gravidade: this.gravidade,
      u_amortecimento: this.amortecimento,
    });
    this.quad.desenhar();
    this.fluxo.trocar();

    const extra = this.pulsoDeChuva ?? 0;
    this.pulsoDeChuva = 0;

    this.profundidade.escrita.ligar();
    this.progAltura.usar().setTodos({
      u_agua: this.profundidade.leitura,
      u_fluxo: this.fluxo.leitura,
      u_chuva: mascara,
      u_texel: this.texel,
      u_dt: dt,
      u_taxaChuva: this.taxaChuva + extra / Math.max(dt, 1e-4),
      u_evaporacao: this.evaporacao,
    });
    this.quad.desenhar();
    this.profundidade.trocar();

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  }
}
