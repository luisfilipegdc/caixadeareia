#!/usr/bin/env node
// Ponte Kinect -> WebSocket.
//
// Único pedaço nativo do projeto. Lê os quadros de profundidade do sensor e
// reenvia crus para o navegador, no formato descrito em
// src/fonte/ponte-kinect.js. Tudo mais (relevo, água, cenários) vive na web.
//
//   node ponte/ponte.js                 sensor real, porta 8787
//   node ponte/ponte.js --simulado      relevo sintético, para testar sem Kinect
//   node ponte/ponte.js --porta 9000

import { WebSocketServer } from 'ws';

const argumentos = processarArgumentos(process.argv.slice(2));
const PORTA = argumentos.porta ?? 8787;
const LARGURA = 640;
const ALTURA = 480;
const CABECALHO = 12;

function processarArgumentos(lista) {
  const saida = { simulado: false };
  for (let i = 0; i < lista.length; i++) {
    if (lista[i] === '--simulado') saida.simulado = true;
    else if (lista[i] === '--porta') saida.porta = Number(lista[++i]);
  }
  return saida;
}

// --------------------------------------------------------------- sensores

// Cada driver expõe: iniciar(aoQuadro) -> { parar() } e devolve profundidade
// em milímetros num Uint16Array de LARGURA*ALTURA.
const DRIVERS = [
  {
    nome: 'kinect2',
    // Kinect for Xbox One / Kinect for Windows v2
    async carregar() {
      const { default: Kinect2 } = await import('kinect2');
      const kinect = new Kinect2();
      if (!kinect.open()) throw new Error('sensor não abriu');
      return {
        iniciar(aoQuadro) {
          kinect.on('depthFrame', (buffer) => {
            aoQuadro(new Uint16Array(buffer.buffer, buffer.byteOffset, buffer.length / 2));
          });
          kinect.openDepthReader();
        },
        parar() { kinect.close(); },
      };
    },
  },
  {
    nome: 'freenect',
    // Kinect v1: Xbox 360 (1414 e 1473) e Kinect for Windows (1517), via
    // libfreenect. É o driver que o AR Sandbox original usa.
    async carregar() {
      const { default: freenect } = await import('freenect');
      const contexto = freenect.createContext();
      return {
        iniciar(aoQuadro) {
          contexto.on('depth', (buffer) => {
            aoQuadro(new Uint16Array(buffer.buffer, buffer.byteOffset, buffer.length / 2));
          });
          contexto.setDepthCallback?.();
          contexto.resume();
        },
        parar() { contexto.pause(); },
      };
    },
  },
  {
    nome: 'kinect',
    // node-kinect, outra ligação com a libfreenect. Serve de alternativa
    // quando o pacote "freenect" não compila na máquina.
    async carregar() {
      const { default: kinect } = await import('kinect');
      const sensor = kinect();
      return {
        iniciar(aoQuadro) {
          sensor.on('depth', (buffer) => {
            aoQuadro(new Uint16Array(buffer.buffer, buffer.byteOffset, buffer.length / 2));
          });
          sensor.resume();
        },
        parar() { sensor.pause(); },
      };
    },
  },
  {
    nome: 'ponte-externa',
    // Escotilha de emergência: qualquer programa que escreva quadros de
    // profundidade crus (uint16, 640x480) na entrada padrão da ponte serve
    // como driver. Útil quando o único jeito de falar com o sensor na
    // máquina é um executável próprio.
    async carregar() {
      if (process.env.PONTE_STDIN !== '1') throw new Error('desligado (defina PONTE_STDIN=1)');
      return {
        iniciar(aoQuadro) {
          const esperado = 640 * 480 * 2;
          let acumulado = Buffer.alloc(0);
          process.stdin.on('data', (pedaco) => {
            acumulado = Buffer.concat([acumulado, pedaco]);
            while (acumulado.length >= esperado) {
              const quadro = acumulado.subarray(0, esperado);
              acumulado = acumulado.subarray(esperado);
              aoQuadro(new Uint16Array(quadro.buffer, quadro.byteOffset, esperado / 2));
            }
          });
        },
        parar() { process.stdin.pause(); },
      };
    },
  },
];

async function abrirSensor() {
  const falhas = [];
  for (const driver of DRIVERS) {
    try {
      const instancia = await driver.carregar();
      console.log(`[ponte] sensor aberto pelo driver "${driver.nome}"`);
      return { ...instancia, nome: driver.nome };
    } catch (erro) {
      falhas.push(`${driver.nome}: ${erro.message}`);
    }
  }
  throw new Error(`nenhum driver disponível\n  ${falhas.join('\n  ')}`);
}

// Sensor falso: uma paisagem que respira, para validar toda a cadeia sem
// hardware nenhum.
function sensorSimulado() {
  let temporizador;
  return {
    nome: 'simulado',
    iniciar(aoQuadro) {
      let t = 0;
      const quadro = new Uint16Array(LARGURA * ALTURA);
      temporizador = setInterval(() => {
        t += 0.05;
        for (let y = 0; y < ALTURA; y++) {
          for (let x = 0; x < LARGURA; x++) {
            const u = x / LARGURA - 0.5;
            const v = y / ALTURA - 0.5;
            const morro = Math.exp(-((u + 0.2) ** 2 + v ** 2) * 12) * 180;
            const onda = Math.sin(u * 9 + t) * Math.cos(v * 7 - t * 0.6) * 25;
            // 1000mm até o fundo da caixa, relevo subindo em direção ao sensor.
            quadro[y * LARGURA + x] = Math.round(1000 - morro - onda - 60);
          }
        }
        aoQuadro(quadro);
      }, 33);
    },
    parar() { clearInterval(temporizador); },
  };
}

// ------------------------------------------------------------- servidor

const servidor = new WebSocketServer({ port: PORTA });
const clientes = new Set();
let numeroDoQuadro = 0;
let sensor = null;

servidor.on('connection', (socket) => {
  clientes.add(socket);
  // Controle de fluxo: só mandamos o próximo quadro depois que o navegador
  // confirmou o anterior. Sem isso, um PC fraco vai acumulando quadros
  // atrasados — a areia responde com atraso e a queda da ponte demora a
  // aparecer, porque a fila ainda está sendo consumida.
  socket.pronto = true;
  socket.enviadoEm = 0;

  socket.on('message', (dados, binario) => {
    if (!binario && dados.toString() === 'pronto') socket.pronto = true;
  });

  console.log(`[ponte] navegador conectado (${clientes.size} ativo(s))`);
  socket.send(JSON.stringify({
    tipo: 'ola',
    sensor: sensor?.nome ?? 'nenhum',
    largura: LARGURA,
    altura: ALTURA,
    mensagem: sensor ? `Sensor "${sensor.nome}" transmitindo.` : 'Ponte no ar, sem sensor.',
  }));
  socket.on('close', () => {
    clientes.delete(socket);
    console.log(`[ponte] navegador saiu (${clientes.size} ativo(s))`);
  });
  socket.on('error', () => clientes.delete(socket));
});

servidor.on('listening', () => {
  console.log(`[ponte] escutando em ws://localhost:${PORTA}`);
});

servidor.on('error', (erro) => {
  if (erro.code === 'EADDRINUSE') {
    // Já existe uma ponte na porta. Isso é normal quando o executável sobe a
    // sua ponte interna e o usuário já tinha aberto a dele, com o driver do
    // sensor instalado. A ponte de fora é a boa: saímos de cena em silêncio.
    console.log(`[ponte] porta ${PORTA} já está em uso; deixando a ponte existente atender`);
    process.exit(0);
  }
  console.error(`[ponte] erro no servidor: ${erro.message}`);
  process.exit(1);
});

let quadrosCapturados = 0;

function transmitir(profundidade) {
  // Contado antes da saída antecipada: a taxa que interessa no relatório é a
  // do sensor, não a do envio, senão sem navegador aberto parece que a
  // captura parou.
  quadrosCapturados++;
  if (!clientes.size) return;

  const pacote = Buffer.allocUnsafe(CABECALHO + profundidade.length * 2);
  pacote.write('KNCT', 0, 'ascii');
  pacote.writeUInt16LE(LARGURA, 4);
  pacote.writeUInt16LE(ALTURA, 6);
  pacote.writeUInt32LE(++numeroDoQuadro >>> 0, 8);
  Buffer.from(profundidade.buffer, profundidade.byteOffset, profundidade.length * 2)
    .copy(pacote, CABECALHO);

  const agora = Date.now();
  for (const cliente of clientes) {
    if (cliente.readyState !== cliente.OPEN) continue;

    // Descarta o quadro se o cliente ainda não confirmou o anterior: relevo
    // velho não serve para nada. A janela de 250ms evita travar de vez caso
    // uma confirmação se perca.
    const atrasado = !cliente.pronto && agora - cliente.enviadoEm < 250;
    if (atrasado || cliente.bufferedAmount > pacote.length) continue;

    cliente.pronto = false;
    cliente.enviadoEm = agora;
    cliente.send(pacote, { binary: true });
  }
}

// Relatório periódico. A origem aparece em toda linha porque o começo da
// execução some do terminal em poucos minutos, e é justamente ali que fica a
// informação de o relevo ser real ou simulado.
function relatarPeriodicamente() {
  let ultimo = quadrosCapturados;
  setInterval(() => {
    const taxa = Math.round((quadrosCapturados - ultimo) / 10);
    ultimo = quadrosCapturados;
    const origem = sensor?.nome === 'simulado' ? 'SIMULADO (sem sensor)' : (sensor?.nome ?? 'sem sensor');
    console.log(`[ponte] ${origem} — ${taxa} quadros por segundo, ${clientes.size} navegador(es)`);
  }, 10000).unref();
}

async function principal() {
  sensor = argumentos.simulado ? sensorSimulado() : await abrirSensor().catch((erro) => {
    console.error(`[ponte] ${erro.message}`);
    console.error('[ponte] caindo para o modo simulado; use --simulado para não ver este aviso');
    return sensorSimulado();
  });

  sensor.iniciar(transmitir);
  relatarPeriodicamente();
}

for (const sinal of ['SIGINT', 'SIGTERM']) {
  process.on(sinal, () => {
    console.log('\n[ponte] encerrando');
    sensor?.parar();
    // Fecha os navegadores explicitamente: server.close() só para de aceitar
    // conexões novas, e sem isso o cliente demora a perceber a queda. O
    // process.exit precisa esperar o quadro de fechamento sair da fila, senão
    // o navegador só descobre a queda quando o TCP expira.
    for (const cliente of clientes) cliente.close(1001, 'ponte encerrando');
    servidor.close();
    setTimeout(() => process.exit(0), 150);
  });
}

principal();
