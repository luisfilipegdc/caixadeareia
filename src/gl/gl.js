// Utilitários mínimos de WebGL2: programas, texturas de ponto flutuante,
// framebuffers e o quad de tela cheia que todos os passos usam.

export const VS_QUAD = `#version 300 es
in vec2 a_pos;
out vec2 v_uv;
void main() {
  v_uv = a_pos * 0.5 + 0.5;
  gl_Position = vec4(a_pos, 0.0, 1.0);
}`;

export function criarContexto(canvas) {
  const gl = canvas.getContext('webgl2', {
    antialias: false,
    alpha: false,
    depth: false,
    powerPreference: 'high-performance',
    preserveDrawingBuffer: false,
  });
  if (!gl) throw new Error('Este navegador não tem WebGL2. Use Chrome, Edge ou Firefox atualizados.');
  if (!gl.getExtension('EXT_color_buffer_float')) {
    throw new Error('A placa de vídeo não suporta texturas de ponto flutuante (EXT_color_buffer_float).');
  }
  gl.getExtension('OES_texture_float_linear');
  return gl;
}

function compilar(gl, tipo, fonte) {
  const sh = gl.createShader(tipo);
  gl.shaderSource(sh, fonte);
  gl.compileShader(sh);
  if (!gl.getShaderParameter(sh, gl.COMPILE_STATUS)) {
    const log = gl.getShaderInfoLog(sh);
    gl.deleteShader(sh);
    throw new Error(`Erro ao compilar shader:\n${log}`);
  }
  return sh;
}

export function criarPrograma(gl, fsFonte, vsFonte = VS_QUAD) {
  const vs = compilar(gl, gl.VERTEX_SHADER, vsFonte);
  const fs = compilar(gl, gl.FRAGMENT_SHADER, fsFonte);
  const prog = gl.createProgram();
  gl.attachShader(prog, vs);
  gl.attachShader(prog, fs);
  gl.bindAttribLocation(prog, 0, 'a_pos');
  gl.linkProgram(prog);
  gl.deleteShader(vs);
  gl.deleteShader(fs);
  if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
    const log = gl.getProgramInfoLog(prog);
    gl.deleteProgram(prog);
    throw new Error(`Erro ao linkar programa:\n${log}`);
  }
  return new Programa(gl, prog);
}

class Programa {
  constructor(gl, prog) {
    this.gl = gl;
    this.prog = prog;
    this.locais = new Map();
    this.unidade = 0;
  }

  local(nome) {
    if (!this.locais.has(nome)) {
      this.locais.set(nome, this.gl.getUniformLocation(this.prog, nome));
    }
    return this.locais.get(nome);
  }

  usar() {
    this.gl.useProgram(this.prog);
    this.unidade = 0;
    return this;
  }

  // Aceita número, array de 2/3/4 números, booleano ou textura.
  set(nome, valor) {
    const gl = this.gl;
    const loc = this.local(nome);
    if (loc === null) return this;
    if (valor instanceof Textura) {
      const u = this.unidade++;
      gl.activeTexture(gl.TEXTURE0 + u);
      gl.bindTexture(gl.TEXTURE_2D, valor.tex);
      gl.uniform1i(loc, u);
    } else if (typeof valor === 'number') {
      gl.uniform1f(loc, valor);
    } else if (typeof valor === 'boolean') {
      gl.uniform1i(loc, valor ? 1 : 0);
    } else if (valor.length === 2) {
      gl.uniform2f(loc, valor[0], valor[1]);
    } else if (valor.length === 3) {
      gl.uniform3f(loc, valor[0], valor[1], valor[2]);
    } else if (valor.length === 4) {
      gl.uniform4f(loc, valor[0], valor[1], valor[2], valor[3]);
    } else if (valor.length === 9) {
      gl.uniformMatrix3fv(loc, false, valor);
    } else {
      throw new Error(`Uniforme "${nome}" com valor não suportado.`);
    }
    return this;
  }

  setTodos(mapa) {
    for (const [nome, valor] of Object.entries(mapa)) this.set(nome, valor);
    return this;
  }
}

export class Textura {
  constructor(gl, { largura, altura, formatoInterno = gl.RGBA32F, formato = gl.RGBA, tipo = gl.FLOAT, filtro = gl.NEAREST, dados = null }) {
    this.gl = gl;
    this.largura = largura;
    this.altura = altura;
    this.formatoInterno = formatoInterno;
    this.formato = formato;
    this.tipo = tipo;
    this.tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, this.tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, formatoInterno, largura, altura, 0, formato, tipo, dados);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, filtro);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, filtro);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  }

  enviar(dados) {
    const gl = this.gl;
    gl.bindTexture(gl.TEXTURE_2D, this.tex);
    gl.texSubImage2D(gl.TEXTURE_2D, 0, 0, 0, this.largura, this.altura, this.formato, this.tipo, dados);
    return this;
  }

  enviarImagem(fonte) {
    const gl = this.gl;
    gl.bindTexture(gl.TEXTURE_2D, this.tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, this.formatoInterno, this.formato, this.tipo, fonte);
    return this;
  }

  destruir() {
    this.gl.deleteTexture(this.tex);
  }
}

export class Alvo {
  constructor(gl, textura) {
    this.gl = gl;
    this.textura = textura;
    this.fb = gl.createFramebuffer();
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.fb);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, textura.tex, 0);
    const estado = gl.checkFramebufferStatus(gl.FRAMEBUFFER);
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    if (estado !== gl.FRAMEBUFFER_COMPLETE) {
      throw new Error(`Framebuffer incompleto (0x${estado.toString(16)}).`);
    }
  }

  ligar() {
    const gl = this.gl;
    gl.bindFramebuffer(gl.FRAMEBUFFER, this.fb);
    gl.viewport(0, 0, this.textura.largura, this.textura.altura);
    return this;
  }

  limpar(r = 0, g = 0, b = 0, a = 0) {
    this.ligar();
    this.gl.clearColor(r, g, b, a);
    this.gl.clear(this.gl.COLOR_BUFFER_BIT);
    return this;
  }
}

// Par de texturas para simulações que leem o estado anterior e escrevem o novo.
export class PingPong {
  constructor(gl, opcoes) {
    this.a = { tex: new Textura(gl, opcoes) };
    this.b = { tex: new Textura(gl, opcoes) };
    this.a.alvo = new Alvo(gl, this.a.tex);
    this.b.alvo = new Alvo(gl, this.b.tex);
  }

  get leitura() { return this.a.tex; }
  get escrita() { return this.b.alvo; }

  trocar() {
    const t = this.a;
    this.a = this.b;
    this.b = t;
  }

  limpar() {
    this.a.alvo.limpar();
    this.b.alvo.limpar();
  }
}

export function criarQuad(gl) {
  const vao = gl.createVertexArray();
  const buf = gl.createBuffer();
  gl.bindVertexArray(vao);
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
  gl.enableVertexAttribArray(0);
  gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
  gl.bindVertexArray(null);
  return {
    desenhar() {
      gl.bindVertexArray(vao);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
    },
  };
}

export function paraTela(gl, largura, altura) {
  gl.bindFramebuffer(gl.FRAMEBUFFER, null);
  gl.viewport(0, 0, largura, altura);
}
