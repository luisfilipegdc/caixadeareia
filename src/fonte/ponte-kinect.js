// Cliente da ponte nativa do Kinect.
//
// A ponte (pasta ponte/) lê o sensor e envia quadros de profundidade crus por
// WebSocket. O protocolo é binário e mínimo, para caber no orçamento de
// latência de 30fps:
//
//   [ 4 bytes  ] "KNCT"
//   [ uint16   ] largura
//   [ uint16   ] altura
//   [ uint32   ] número do quadro
//   [ w*h*u16  ] profundidade em milímetros (0 = leitura inválida)
//
// Mensagens de texto (JSON) carregam status e capacidades do sensor.

export class PonteKinect extends EventTarget {
  constructor(url = 'ws://localhost:8787') {
    super();
    this.url = url;
    this.ws = null;
    this.largura = 0;
    this.altura = 0;
    this.profundidade = null;
    this.quadro = 0;
    this.conectado = false;
    this.tentativas = 0;
    this.reconectar = true;
  }

  conectar(url = this.url) {
    this.url = url;
    this.reconectar = true;
    this._abrir();
  }

  _abrir() {
    this.desligar(false);

    // Página servida por HTTPS (Vercel, GitHub Pages) falando com uma ponte
    // local: o navegador só libera quando o destino é localhost, que conta
    // como origem confiável. Qualquer outro endereço é bloqueado como
    // conteúdo misto, e o erro que aparece no console não explica isso.
    if (location.protocol === 'https:' && this.url.startsWith('ws://')
        && !/^wss?:\/\/(localhost|127\.0\.0\.1|\[::1\])(:|$|\/)/.test(this.url)) {
      this._avisar('erro', {
        mensagem: 'O navegador bloqueia ws:// vindo de uma página https, exceto para localhost. '
          + 'Use o executável, rode o site local (npm run web) ou coloque a ponte atrás de wss://.',
      });
      return;
    }

    let ws;
    try {
      ws = new WebSocket(this.url);
    } catch (erro) {
      this._avisar('erro', { mensagem: `Endereço inválido: ${this.url}` });
      return;
    }
    ws.binaryType = 'arraybuffer';
    this.ws = ws;

    ws.onopen = () => {
      this.conectado = true;
      this.tentativas = 0;
      this._avisar('conectado', { url: this.url });
    };

    ws.onmessage = (evento) => {
      if (typeof evento.data === 'string') {
        try {
          this._avisar('status', JSON.parse(evento.data));
        } catch {
          // Mensagem de texto fora do protocolo: ignorada de propósito.
        }
        return;
      }
      this._receberQuadro(evento.data);
    };

    ws.onerror = () => {
      this._avisar('erro', { mensagem: 'Falha na conexão com a ponte.' });
    };

    ws.onclose = () => {
      const estavaConectado = this.conectado;
      this.conectado = false;
      this._avisar('desconectado', { esperado: !this.reconectar });
      if (this.reconectar) {
        // Recuo exponencial até 8s: a ponte pode estar reiniciando.
        const espera = Math.min(8000, 500 * 2 ** this.tentativas++);
        this.timer = setTimeout(() => this._abrir(), estavaConectado ? 500 : espera);
      }
    };
  }

  _receberQuadro(buffer) {
    if (buffer.byteLength < 12) return;
    const cabecalho = new DataView(buffer);
    if (cabecalho.getUint32(0, false) !== 0x4b4e4354) return; // "KNCT"

    const largura = cabecalho.getUint16(4, true);
    const altura = cabecalho.getUint16(6, true);
    const quadro = cabecalho.getUint32(8, true);
    const esperado = 12 + largura * altura * 2;
    if (buffer.byteLength < esperado) return;

    if (largura !== this.largura || altura !== this.altura) {
      this.largura = largura;
      this.altura = altura;
      this._avisar('resolucao', { largura, altura });
    }

    this.quadro = quadro;
    this.profundidade = new Uint16Array(buffer, 12, largura * altura);

    // Confirma o recebimento: a ponte só manda o próximo quadro depois disso,
    // então quem não dá conta do ritmo simplesmente recebe menos quadros em
    // vez de acumular atraso.
    if (this.ws?.readyState === WebSocket.OPEN) this.ws.send('pronto');

    this._avisar('quadro', { largura, altura, quadro });
  }

  desligar(definitivo = true) {
    if (definitivo) this.reconectar = false;
    clearTimeout(this.timer);
    if (this.ws) {
      this.ws.onopen = this.ws.onmessage = this.ws.onerror = this.ws.onclose = null;
      if (this.ws.readyState <= WebSocket.OPEN) this.ws.close();
      this.ws = null;
    }
    this.conectado = false;
  }

  _avisar(tipo, detalhe) {
    this.dispatchEvent(new CustomEvent(tipo, { detail: detalhe }));
  }
}
