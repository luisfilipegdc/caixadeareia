#!/usr/bin/env node
// Diagnóstico da ponte: responde por que o Kinect não conecta.
//
//   node ponte/diagnostico.js
//
// Verifica, em ordem, tudo que precisa estar no lugar: versão do Node,
// drivers instalados, SDK do sensor, sensor reconhecido pelo sistema e porta
// livre. Ao final imprime o próximo passo concreto.

import { existsSync, readdirSync } from 'node:fs';
import { execFileSync } from 'node:child_process';
import { createServer } from 'node:net';
import { createRequire } from 'node:module';

const exigir = createRequire(import.meta.url);
const PORTA = Number(process.argv.includes('--porta') ? process.argv[process.argv.indexOf('--porta') + 1] : 8787);

const problemas = [];
const linha = (marca, texto) => console.log(`  ${marca}  ${texto}`);
const ok = (t) => linha('ok  ', t);
const falha = (t, causa) => { linha('FALHA', t); problemas.push(causa); };
const aviso = (t) => linha('aviso', t);

console.log('\nDiagnóstico da ponte Kinect\n');

// 1. Node -------------------------------------------------------------------
const major = Number(process.versions.node.split('.')[0]);
if (major >= 18) ok(`Node ${process.versions.node}`);
else falha(`Node ${process.versions.node} — a ponte precisa da versão 18 ou maior`,
  'Instale o Node 20 LTS em https://nodejs.org e rode tudo de novo.');

console.log(`  ....  Sistema: ${process.platform} ${process.arch}`);

// 2. Dependência de rede ----------------------------------------------------
try {
  exigir.resolve('ws');
  ok('módulo "ws" instalado');
} catch {
  falha('módulo "ws" ausente', 'Rode "npm install" na pasta do projeto.');
}

// 3. Drivers do sensor ------------------------------------------------------
const drivers = [
  { nome: 'kinect2', sensores: 'Kinect for Xbox One / Kinect for Windows v2' },
  { nome: 'freenect', sensores: 'Kinect v1: Xbox 360 (1414/1473) e Kinect for Windows (1517)' },
  { nome: 'kinect', sensores: 'Kinect v1, ligação alternativa com a libfreenect' },
];

let algumDriver = false;
for (const driver of drivers) {
  try {
    exigir.resolve(driver.nome);
  } catch {
    linha('....', `driver "${driver.nome}" não instalado (${driver.sensores})`);
    continue;
  }
  try {
    // Resolver não basta: módulo nativo pode estar instalado e não carregar.
    exigir(driver.nome);
    ok(`driver "${driver.nome}" instalado e carregando — ${driver.sensores}`);
    algumDriver = true;
  } catch (erro) {
    falha(`driver "${driver.nome}" instalado mas não carrega: ${erro.message.split('\n')[0]}`,
      `O módulo "${driver.nome}" foi baixado mas o binário não funciona. `
      + 'Isso costuma ser arquitetura trocada (32 x 64 bits) ou compilação incompleta. '
      + `Tente: npm rebuild ${driver.nome}`);
  }
}

if (!algumDriver) {
  problemas.push(
    'Nenhum driver de sensor instalado. Sem isso a ponte só roda em modo simulado.\n'
    + '      Kinect v2 (Xbox One):  instale o "Kinect for Windows SDK 2.0" e rode  npm install kinect2\n'
    + '      Kinect v1 (360 / for Windows):  npm install freenect   ou   npm install kinect\n'
    + '      No Windows, o driver do v1 costuma não compilar; veja a seção\n'
    + '      "Kinect v1 no Windows" no README antes de insistir.');
}

// 4. SDK no Windows ---------------------------------------------------------
if (process.platform === 'win32') {
  const caminhos = [
    'C:\\Program Files\\Microsoft SDKs\\Kinect\\v2.0_1409',
    'C:\\Program Files\\Microsoft SDKs\\Kinect',
  ];
  const achado = caminhos.find((c) => existsSync(c));
  if (achado) ok(`Kinect SDK encontrado em ${achado}`);
  else aviso('Kinect for Windows SDK 2.0 não encontrado — necessário para compilar o driver kinect2');

  // 5. O sistema enxerga o sensor? ------------------------------------------
  try {
    const saida = execFileSync('powershell', ['-NoProfile', '-Command',
      "Get-PnpDevice | Where-Object { $_.FriendlyName -like '*Kinect*' } "
      + '| Select-Object -ExpandProperty FriendlyName'],
    { encoding: 'utf8', timeout: 15000 });

    const encontrados = saida.split('\n').map((s) => s.trim()).filter(Boolean);
    if (encontrados.length) {
      ok(`o Windows reconhece o sensor: ${encontrados.join(', ')}`);
    } else {
      falha('nenhum dispositivo Kinect reconhecido pelo Windows',
        'O sensor não aparece no Gerenciador de Dispositivos. Confira: cabo de força ligado '
        + '(o Kinect não vive só do USB), porta USB 3.0 para o v2, e o adaptador oficial.');
    }
  } catch {
    aviso('não consegui consultar os dispositivos USB');
  }
} else if (process.platform === 'linux') {
  // No Linux o caminho é o libfreenect, que o freenect empacota.
  try {
    const saida = execFileSync('lsusb', [], { encoding: 'utf8', timeout: 10000 });
    const encontrados = saida.split('\n').filter((l) => /kinect|microsoft corp/i.test(l));
    if (encontrados.length) ok(`o sistema reconhece o sensor:\n        ${encontrados.join('\n        ')}`);
    else falha('nenhum dispositivo Kinect no barramento USB',
      'Confira a fonte de energia (o Kinect não vive só do USB) e o adaptador oficial.');
  } catch {
    aviso('não consegui listar os dispositivos USB (lsusb não disponível)');
  }
} else {
  aviso(`o driver do Kinect existe para Windows e Linux; aqui é ${process.platform}`);
}

// 6. Porta ------------------------------------------------------------------
await new Promise((pronto) => {
  const teste = createServer();
  teste.once('error', (erro) => {
    if (erro.code === 'EADDRINUSE') {
      aviso(`a porta ${PORTA} já está ocupada — provavelmente outra ponte já está no ar`);
    } else {
      falha(`não consegui abrir a porta ${PORTA}: ${erro.message}`, 'Verifique o firewall.');
    }
    pronto();
  });
  teste.once('listening', () => {
    ok(`porta ${PORTA} livre`);
    teste.close(pronto);
  });
  teste.listen(PORTA, '127.0.0.1');
});

// Veredito ------------------------------------------------------------------
console.log('');
if (!problemas.length) {
  console.log('Tudo certo. Rode:  npm run ponte\n');
} else {
  console.log(`${problemas.length} ponto(s) a resolver:\n`);
  problemas.forEach((p, i) => console.log(`  ${i + 1}. ${p}\n`));
  console.log('Enquanto isso, dá para usar tudo em modo demonstração:  npm run ponte:simulada\n');
}
