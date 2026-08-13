// Composição da imagem projetada: mapa hipsométrico + curvas de nível +
// sombreamento do relevo + água, com um trecho opcional fornecido pelo
// cenário ativo.
//
// O cenário injeta uma função GLSL no shader final:
//
//   vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor)
//
// Assim vulcão, biomas, marcadores e efeitos entram sem tocar no motor.

import { criarPrograma, Textura } from '../gl/gl.js';

const CABECALHO = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;

uniform sampler2D u_terreno;
uniform sampler2D u_agua;
uniform sampler2D u_paleta;
uniform vec2 u_texel;
uniform float u_tempo;
uniform float u_intervalo;
uniform float u_curvas;
uniform float u_espelhar;
uniform float u_aguaAtiva;
uniform vec3 u_corRaso;
uniform vec3 u_corFundo;

float alturaEm(vec2 uv) { return texture(u_terreno, uv).r; }
float aguaEm(vec2 uv) { return texture(u_agua, uv).r; }
`;

const CENARIO_PADRAO = `
vec4 cenario(vec2 uv, float altura, float agua, float tempo, vec4 cor) {
  return cor;
}`;

const CORPO = `
// Normal do relevo por diferenças centrais, para o sombreamento.
vec3 normalDoRelevo(vec2 uv, float escala) {
  float e = alturaEm(uv + vec2(u_texel.x, 0.0));
  float o = alturaEm(uv - vec2(u_texel.x, 0.0));
  float n = alturaEm(uv + vec2(0.0, u_texel.y));
  float s = alturaEm(uv - vec2(0.0, u_texel.y));
  return normalize(vec3((o - e) * escala, (s - n) * escala, 2.0 * u_texel.x * escala));
}

void main() {
  vec2 uv = v_uv;
  if (u_espelhar > 0.5) uv.x = 1.0 - uv.x;

  float altura = alturaEm(uv);
  float agua = aguaEm(uv);

  vec4 cor = texture(u_paleta, vec2(clamp(altura, 0.001, 0.999), 0.5));

  // Relevo sombreado: dá volume à areia mesmo com a projeção achatada.
  vec3 normal = normalDoRelevo(uv, 1.0);
  float luz = clamp(dot(normal, normalize(vec3(-0.6, 0.7, 0.8))), 0.0, 1.0);
  cor.rgb *= 0.75 + 0.45 * luz;

  // Curvas de nível com espessura constante na tela (antialiasing por fwidth).
  if (u_curvas > 0.5) {
    float faixa = altura / max(u_intervalo, 0.001);
    float largura = fwidth(faixa);
    float linha = abs(fract(faixa - 0.5) - 0.5) / max(largura, 1e-5);
    float traco = 1.0 - clamp(linha - 0.6, 0.0, 1.0);
    cor.rgb = mix(cor.rgb, cor.rgb * 0.35, traco * 0.85);
  }

  if (u_aguaAtiva > 0.5 && agua > 0.0006) {
    float d = clamp(agua * 14.0, 0.0, 1.0);
    vec3 corAgua = mix(u_corRaso, u_corFundo, d);

    // Brilho na superfície acompanhando a inclinação da lâmina d'água.
    float e = aguaEm(uv + vec2(u_texel.x, 0.0)) + alturaEm(uv + vec2(u_texel.x, 0.0));
    float o = aguaEm(uv - vec2(u_texel.x, 0.0)) + alturaEm(uv - vec2(u_texel.x, 0.0));
    float brilho = clamp(abs(e - o) * 30.0, 0.0, 1.0);

    cor.rgb = mix(cor.rgb, corAgua, clamp(0.25 + d * 0.75, 0.0, 0.95));
    cor.rgb += brilho * 0.25;
  }

  saida = cenario(uv, altura, agua, u_tempo, cor);
  saida.a = 1.0;
}`;

export class Vista {
  constructor(gl, quad) {
    this.gl = gl;
    this.quad = quad;
    this.paleta = new Textura(gl, {
      largura: 256, altura: 1,
      formatoInterno: gl.RGBA8, formato: gl.RGBA, tipo: gl.UNSIGNED_BYTE,
      filtro: gl.LINEAR,
      dados: new Uint8Array(256 * 4),
    });
    this.programa = null;
    this.fonteAtual = null;
    this.compilar(CENARIO_PADRAO);
    this.intervalo = 0.04;
    this.curvas = true;
    this.espelhar = false;
    this.aguaAtiva = true;
    this.corRaso = [0.31, 0.76, 0.97];
    this.corFundo = [0.04, 0.24, 0.57];
  }

  compilar(trechoCenario) {
    const fonte = CABECALHO + (trechoCenario || CENARIO_PADRAO) + CORPO;
    if (fonte === this.fonteAtual) return;
    try {
      this.programa = criarPrograma(this.gl, fonte);
      this.fonteAtual = fonte;
    } catch (erro) {
      // Um cenário com shader quebrado não pode derrubar a aula.
      console.error('Shader do cenário rejeitado, usando o padrão:', erro);
      if (!this.programa) {
        this.programa = criarPrograma(this.gl, CABECALHO + CENARIO_PADRAO + CORPO);
      }
      throw erro;
    }
  }

  // `paradas` é uma lista [{ h: 0..1, cor: '#rrggbb' }] em ordem crescente.
  definirPaleta(paradas) {
    const ordenadas = [...paradas].sort((a, b) => a.h - b.h);
    const dados = new Uint8Array(256 * 4);
    for (let i = 0; i < 256; i++) {
      const t = i / 255;
      let a = ordenadas[0];
      let b = ordenadas[ordenadas.length - 1];
      for (let k = 0; k < ordenadas.length - 1; k++) {
        if (t >= ordenadas[k].h && t <= ordenadas[k + 1].h) {
          a = ordenadas[k];
          b = ordenadas[k + 1];
          break;
        }
      }
      const intervalo = b.h - a.h;
      const f = intervalo > 1e-6 ? (t - a.h) / intervalo : 0;
      const ca = hexParaRgb(a.cor);
      const cb = hexParaRgb(b.cor);
      dados[i * 4 + 0] = Math.round(ca[0] + (cb[0] - ca[0]) * f);
      dados[i * 4 + 1] = Math.round(ca[1] + (cb[1] - ca[1]) * f);
      dados[i * 4 + 2] = Math.round(ca[2] + (cb[2] - ca[2]) * f);
      dados[i * 4 + 3] = 255;
    }
    this.paleta.enviar(dados);
  }

  desenhar({ terreno, agua, tempo, texel }) {
    this.programa.usar().setTodos({
      u_terreno: terreno,
      u_agua: agua,
      u_paleta: this.paleta,
      u_texel: texel,
      u_tempo: tempo,
      u_intervalo: this.intervalo,
      u_curvas: this.curvas ? 1 : 0,
      u_espelhar: this.espelhar ? 1 : 0,
      u_aguaAtiva: this.aguaAtiva ? 1 : 0,
      u_corRaso: this.corRaso,
      u_corFundo: this.corFundo,
    });
    this.quad.desenhar();
  }
}

export function hexParaRgb(hex) {
  const limpo = hex.replace('#', '').trim();
  const cheio = limpo.length === 3 ? limpo.split('').map((c) => c + c).join('') : limpo;
  const n = parseInt(cheio, 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

export function hexParaVec3(hex) {
  return hexParaRgb(hex).map((c) => c / 255);
}
