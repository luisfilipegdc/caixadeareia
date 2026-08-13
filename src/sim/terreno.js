// Campo de altura da areia.
//
// Guarda o relevo em uma textura RGBA32F (r = altura normalizada 0..1,
// g = máscara de mão detectada acima da areia) e sabe alimentá-lo a partir de
// três fontes: um terreno sintético (demonstração), o pincel do mouse e os
// quadros de profundidade do Kinect.

import { criarPrograma, PingPong, Textura, Alvo } from '../gl/gl.js';
import { identidade } from '../scan/homografia.js';

const RUIDO = `
float aleatorio(vec2 p) {
  return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float ruido(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  return mix(
    mix(aleatorio(i + vec2(0.0, 0.0)), aleatorio(i + vec2(1.0, 0.0)), u.x),
    mix(aleatorio(i + vec2(0.0, 1.0)), aleatorio(i + vec2(1.0, 1.0)), u.x),
    u.y);
}

float fbm(vec2 p) {
  float soma = 0.0;
  float amplitude = 0.5;
  for (int i = 0; i < 6; i++) {
    soma += amplitude * ruido(p);
    p *= 2.03;
    amplitude *= 0.5;
  }
  return soma;
}`;

const FS_DEMO = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;
uniform float u_semente;
${RUIDO}

void main() {
  vec2 p = v_uv * 4.0 + u_semente;
  float h = fbm(p);
  // Um vale central para a água ter para onde correr.
  float vale = smoothstep(0.0, 0.45, abs(v_uv.y - 0.5 - 0.12 * sin(v_uv.x * 6.0)));
  h = mix(h * 0.55, h, vale);
  // Bordas mais baixas, como areia encostada na parede da caixa.
  vec2 d = min(v_uv, 1.0 - v_uv);
  h *= smoothstep(0.0, 0.08, min(d.x, d.y));
  saida = vec4(clamp(h, 0.0, 1.0), 0.0, 0.0, 1.0);
}`;

const FS_PINCEL = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;
uniform sampler2D u_anterior;
uniform vec2 u_centro;
uniform vec2 u_proporcao;
uniform float u_raio;
uniform float u_forca;

void main() {
  vec4 estado = texture(u_anterior, v_uv);
  float dist = length((v_uv - u_centro) * u_proporcao) / u_raio;
  float peso = exp(-dist * dist * 3.0);
  estado.r = clamp(estado.r + u_forca * peso, 0.0, 1.0);
  saida = estado;
}`;

const FS_KINECT = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;

uniform highp usampler2D u_profundidade;   // milímetros crus do sensor
uniform sampler2D u_anterior;              // relevo do quadro anterior
uniform mat3 u_recorte;                    // homografia areia -> projeção
uniform vec2 u_texelSensor;
uniform float u_base;        // distância do sensor ao fundo da caixa (mm)
uniform float u_util;        // profundidade útil de areia (mm)
uniform float u_suavizacao;  // 0 = cru, 1 = congelado
uniform float u_limiarMao;   // quantos mm acima da areia contam como mão

// A homografia é definida no espaço da projeção; aqui percorremos o caminho
// inverso, do pixel projetado para o pixel do sensor.
vec2 paraSensor(vec2 uv) {
  vec3 p = u_recorte * vec3(uv, 1.0);
  return p.xy / p.z;
}

float amostrar(vec2 uv) {
  return float(texture(u_profundidade, uv).r);
}

void main() {
  vec2 uvSensor = paraSensor(v_uv);
  vec4 anterior = texture(u_anterior, v_uv);

  if (uvSensor.x < 0.0 || uvSensor.x > 1.0 || uvSensor.y < 0.0 || uvSensor.y > 1.0) {
    saida = vec4(0.0, 0.0, 0.0, 1.0);
    return;
  }

  // Média dos vizinhos válidos: o Kinect devolve 0 em pixels sem leitura, e
  // buracos crus viram picos absurdos no relevo.
  float soma = 0.0;
  float validos = 0.0;
  for (int dy = -1; dy <= 1; dy++) {
    for (int dx = -1; dx <= 1; dx++) {
      float d = amostrar(uvSensor + vec2(float(dx), float(dy)) * u_texelSensor);
      if (d > 0.0) { soma += d; validos += 1.0; }
    }
  }

  if (validos < 1.0) {
    // Sem leitura nenhuma: mantém o que já sabíamos daquele ponto.
    saida = vec4(anterior.r, anterior.g * 0.9, 0.0, 1.0);
    return;
  }

  float mm = soma / validos;
  float altura = clamp((u_base - mm) / max(u_util, 1.0), 0.0, 1.0);

  // Qualquer coisa mais perto do sensor que o topo da areia é mão ou objeto.
  float acima = (u_base - u_util - u_limiarMao) - mm;
  float mao = clamp(acima / u_limiarMao, 0.0, 1.0);

  // A mão não deve virar montanha: onde há mão, congela o relevo anterior.
  float alturaFinal = mix(altura, anterior.r, mao);
  alturaFinal = mix(alturaFinal, anterior.r, u_suavizacao);

  saida = vec4(alturaFinal, max(mao, anterior.g * 0.75), 0.0, 1.0);
}`;

const FS_MASCARA = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;
uniform sampler2D u_terreno;
uniform sampler2D u_anterior;
uniform float u_decaimento;

void main() {
  float mao = texture(u_terreno, v_uv).g;
  float antes = texture(u_anterior, v_uv).r * u_decaimento;
  saida = vec4(max(mao, antes), 0.0, 0.0, 1.0);
}`;

const FS_PINCEL_CHUVA = `#version 300 es
precision highp float;
in vec2 v_uv;
out vec4 saida;
uniform sampler2D u_anterior;
uniform vec2 u_centro;
uniform vec2 u_proporcao;
uniform float u_raio;

void main() {
  float dist = length((v_uv - u_centro) * u_proporcao) / u_raio;
  float peso = exp(-dist * dist * 3.0);
  saida = vec4(min(1.0, texture(u_anterior, v_uv).r + peso), 0.0, 0.0, 1.0);
}`;

export class Terreno {
  constructor(gl, quad, largura, altura) {
    this.gl = gl;
    this.quad = quad;
    this.largura = largura;
    this.altura = altura;
    this.proporcao = [1, altura / largura];

    const opts = {
      largura, altura,
      formatoInterno: gl.RGBA32F, formato: gl.RGBA, tipo: gl.FLOAT,
      filtro: gl.LINEAR,
    };
    this.campo = new PingPong(gl, opts);

    const escalar = {
      largura, altura,
      formatoInterno: gl.R32F, formato: gl.RED, tipo: gl.FLOAT,
      filtro: gl.LINEAR,
    };
    this.mascara = new PingPong(gl, escalar);

    this.progDemo = criarPrograma(gl, FS_DEMO);
    this.progPincel = criarPrograma(gl, FS_PINCEL);
    this.progKinect = criarPrograma(gl, FS_KINECT);
    this.progMascara = criarPrograma(gl, FS_MASCARA);
    this.progPincelChuva = criarPrograma(gl, FS_PINCEL_CHUVA);

    this.recorte = identidade();
    this.baseMm = 1000;
    this.utilMm = 300;
    this.limiarMaoMm = 60;
    this.suavizacao = 0.55;
    this.sensor = null;

    this.gerar(Math.random() * 100);
  }

  get textura() { return this.campo.leitura; }
  get texturaChuva() { return this.mascara.leitura; }

  gerar(semente = Math.random() * 100) {
    this.campo.escrita.ligar();
    this.progDemo.usar().set('u_semente', semente);
    this.quad.desenhar();
    this.campo.trocar();
    this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
  }

  esculpir(u, v, raio, forca) {
    this.campo.escrita.ligar();
    this.progPincel.usar().setTodos({
      u_anterior: this.campo.leitura,
      u_centro: [u, v],
      u_proporcao: this.proporcao,
      u_raio: raio,
      u_forca: forca,
    });
    this.quad.desenhar();
    this.campo.trocar();
    this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
  }

  chover(u, v, raio) {
    this.mascara.escrita.ligar();
    this.progPincelChuva.usar().setTodos({
      u_anterior: this.mascara.leitura,
      u_centro: [u, v],
      u_proporcao: this.proporcao,
      u_raio: raio,
    });
    this.quad.desenhar();
    this.mascara.trocar();
    this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
  }

  // Recria a textura do sensor quando a ponte anuncia outra resolução.
  prepararSensor(largura, altura) {
    const gl = this.gl;
    if (this.sensor && this.sensor.largura === largura && this.sensor.altura === altura) return;
    this.sensor?.destruir();
    this.sensor = new Textura(gl, {
      largura, altura,
      formatoInterno: gl.R16UI, formato: gl.RED_INTEGER, tipo: gl.UNSIGNED_SHORT,
      filtro: gl.NEAREST,
    });
    this.texelSensor = [1 / largura, 1 / altura];
  }

  aplicarQuadroKinect(profundidade, largura, altura) {
    this.prepararSensor(largura, altura);
    this.sensor.enviar(profundidade);

    this.campo.escrita.ligar();
    this.progKinect.usar().setTodos({
      u_profundidade: this.sensor,
      u_anterior: this.campo.leitura,
      u_recorte: this.recorte,
      u_texelSensor: this.texelSensor,
      u_base: this.baseMm,
      u_util: this.utilMm,
      u_suavizacao: this.suavizacao,
      u_limiarMao: this.limiarMaoMm,
    });
    this.quad.desenhar();
    this.campo.trocar();
    this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
  }

  // Converte a mão detectada em chuva persistente por alguns quadros, para
  // que o gesto vire um chuvisco contínuo em vez de pingos isolados.
  atualizarChuvaDaMao(decaimento = 0.92) {
    this.mascara.escrita.ligar();
    this.progMascara.usar().setTodos({
      u_terreno: this.campo.leitura,
      u_anterior: this.mascara.leitura,
      u_decaimento: decaimento,
    });
    this.quad.desenhar();
    this.mascara.trocar();
    this.gl.bindFramebuffer(this.gl.FRAMEBUFFER, null);
  }

  limparChuva() {
    this.mascara.limpar();
  }
}
