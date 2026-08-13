// Ponto de entrada: monta o pipeline (terreno -> água -> vista), liga a
// interface do professor e roda o laço de animação.

import { criarContexto, criarQuad, paraTela } from './gl/gl.js';
import { Terreno } from './sim/terreno.js';
import { SimulacaoDeAgua } from './sim/agua.js';
import { Vista, hexParaVec3 } from './render/vista.js';
import { PonteKinect } from './fonte/ponte-kinect.js';
import { listarCenarios, corDeAmostra } from './cenarios.js';
import { montarQuiz } from './ui/quiz.js';
import { iniciarCalibracao } from './ui/calibracao.js';

// Grade da simulação. 4:3 para casar com o campo de visão do Kinect.
const GRADE_L = 256;
const GRADE_A = 192;

const el = (id) => document.getElementById(id);
const canvas = el('palco');

const estado = {
  fonte: 'demo',
  pausado: false,
  tempo: 0,
  cenario: null,
  cenarios: [],
};

function avisar(mensagem) {
  const caixa = el('aviso');
  caixa.textContent = mensagem;
  caixa.hidden = !mensagem;
  if (mensagem) {
    clearTimeout(avisar.timer);
    avisar.timer = setTimeout(() => { caixa.hidden = true; }, 6000);
  }
}

let gl;
try {
  gl = criarContexto(canvas);
} catch (erro) {
  avisar(erro.message);
  throw erro;
}

const quad = criarQuad(gl);
const terreno = new Terreno(gl, quad, GRADE_L, GRADE_A);
const agua = new SimulacaoDeAgua(gl, quad, GRADE_L, GRADE_A);
const vista = new Vista(gl, quad);
const ponte = new PonteKinect();

agua.definirMascaraDeChuva(terreno.texturaChuva);

// ---------------------------------------------------------------- cenários

function aplicarCenario(cenario) {
  estado.cenario = cenario;
  vista.definirPaleta(cenario.paleta);

  try {
    vista.compilar(cenario.trechoShader);
  } catch {
    avisar(`O cenário "${cenario.nome}" tem um erro no shader; usando a exibição padrão.`);
  }

  const curvas = cenario.curvasDeNivel ?? {};
  vista.curvas = curvas.ativas !== false;
  el('curvas').checked = vista.curvas;
  if (curvas.intervalo) {
    vista.intervalo = curvas.intervalo;
    el('intervalo').value = curvas.intervalo;
  }

  const cfgAgua = cenario.agua ?? {};
  vista.aguaAtiva = cfgAgua.ativa !== false;
  if (cfgAgua.corRaso) vista.corRaso = hexParaVec3(cfgAgua.corRaso);
  if (cfgAgua.corFundo) vista.corFundo = hexParaVec3(cfgAgua.corFundo);
  if (typeof cfgAgua.evaporacao === 'number') {
    agua.evaporacao = cfgAgua.evaporacao;
    el('evaporacao').value = String(cfgAgua.evaporacao / 0.02);
  }
  if (typeof cfgAgua.chuvaInicial === 'number') {
    agua.taxaChuva = cfgAgua.chuvaInicial * 0.35;
    el('chuva').value = String(cfgAgua.chuvaInicial);
  }

  const marcadores = cenario.marcadores ?? {};
  el('secaoMarcadores').hidden = !marcadores.ativos;
  el('tituloMarcadores').textContent = marcadores.rotulo ?? 'Marcadores';
  terreno.limparMarcadores();

  quiz.definir(cenario.quiz);
  mostrarNotas(cenario);

  for (const botao of document.querySelectorAll('.cenario')) {
    botao.classList.toggle('ativo', botao.dataset.id === cenario.id);
  }
}

function montarListaDeCenarios(cenarios, categoria = 'todos') {
  const lista = el('listaCenarios');
  lista.textContent = '';

  const visiveis = categoria === 'todos'
    ? cenarios
    : cenarios.filter((c) => (c.categoria ?? 'educacao') === categoria);

  for (const cenario of visiveis) {
    const botao = document.createElement('button');
    botao.className = 'cenario';
    botao.dataset.id = cenario.id;
    botao.classList.toggle('ativo', cenario.id === estado.cenario?.id);

    const amostra = document.createElement('span');
    amostra.className = 'amostra';
    amostra.style.background = corDeAmostra(cenario);

    const texto = document.createElement('span');
    const nome = document.createElement('span');
    nome.className = 'nome';
    nome.textContent = cenario.nome;

    const marca = document.createElement('span');
    marca.className = 'categoria';
    marca.textContent = ROTULOS[cenario.categoria ?? 'educacao'] ?? '';

    const desc = document.createElement('span');
    desc.className = 'desc';
    desc.textContent = cenario.descricao ?? '';

    texto.append(nome, marca, document.createElement('br'), desc);
    botao.append(amostra, texto);
    botao.addEventListener('click', () => aplicarCenario(cenario));
    lista.appendChild(botao);
  }
}

const ROTULOS = {
  educacao: 'educação',
  jogos: 'jogos',
  criatividade: 'criatividade',
  relaxamento: 'relaxamento',
};

function montarFiltros(cenarios) {
  const caixa = el('filtros');
  caixa.textContent = '';

  const presentes = [...new Set(cenarios.map((c) => c.categoria ?? 'educacao'))];
  const opcoes = [['todos', 'todos'], ...presentes.map((c) => [c, ROTULOS[c] ?? c])];

  for (const [valor, rotulo] of opcoes) {
    const botao = document.createElement('button');
    botao.textContent = rotulo;
    botao.classList.toggle('ativo', valor === 'todos');
    botao.addEventListener('click', () => {
      for (const outro of caixa.querySelectorAll('button')) outro.classList.remove('ativo');
      botao.classList.add('ativo');
      montarListaDeCenarios(cenarios, valor);
    });
    caixa.appendChild(botao);
  }
}

function mostrarNotas(cenario) {
  const secao = el('secaoNotas');
  const lista = el('notas');
  lista.textContent = '';
  const notas = cenario.notasDoProfessor ?? [];
  secao.hidden = notas.length === 0;
  for (const nota of notas) {
    const item = document.createElement('li');
    item.textContent = nota;
    lista.appendChild(item);
  }
}

const quiz = montarQuiz({
  secao: el('secaoQuiz'),
  pergunta: el('quizPergunta'),
  alternativas: el('quizAlternativas'),
  anterior: el('quizAnterior'),
  proxima: el('quizProxima'),
});

// ------------------------------------------------------------------ ponte

ponte.addEventListener('conectado', () => {
  el('statusPonte').textContent = 'Conectado. Aguardando quadros...';
});
ponte.addEventListener('resolucao', (e) => {
  const { largura, altura } = e.detail;
  el('statusPonte').textContent = `Kinect ativo — ${largura}×${altura}.`;
});
ponte.addEventListener('desconectado', (e) => {
  el('statusPonte').textContent = e.detail.esperado
    ? 'Desconectado.'
    : 'Conexão perdida. Tentando de novo...';
});
ponte.addEventListener('erro', (e) => {
  el('statusPonte').textContent = e.detail.mensagem;
});
ponte.addEventListener('status', (e) => {
  if (e.detail?.mensagem) el('statusPonte').textContent = e.detail.mensagem;
  // O relevo simulado é convincente demais para passar despercebido: sem
  // aviso na tela, o professor calibra uma areia que não existe.
  if (e.detail?.sensor) marcarSimulado(e.detail.sensor === 'simulado');
});

function marcarSimulado(simulado) {
  estado.simulado = simulado;
  el('selo').hidden = !simulado || estado.fonte !== 'ponte';
}

// --------------------------------------------------------------- interface

function trocarFonte(fonte) {
  estado.fonte = fonte;
  el('modoAtual').textContent = fonte;
  el('controlesDemo').hidden = fonte !== 'demo';
  el('controlesPonte').hidden = fonte !== 'ponte';
  el('selo').hidden = !(fonte === 'ponte' && estado.simulado);
  if (fonte !== 'ponte') ponte.desligar();
}

el('fonte').addEventListener('change', (e) => trocarFonte(e.target.value));
el('novoTerreno').addEventListener('click', () => terreno.gerar());
el('conectarPonte').addEventListener('click', () => ponte.conectar(el('ponteUrl').value.trim()));

el('planoBase').addEventListener('input', (e) => { terreno.baseMm = Number(e.target.value) * 10; });
el('alturaUtil').addEventListener('input', (e) => { terreno.utilMm = Number(e.target.value) * 10; });
el('suavizacao').addEventListener('input', (e) => { terreno.suavizacao = Number(e.target.value); });
el('recortar').addEventListener('click', () => {
  iniciarCalibracao(canvas, (matriz) => {
    terreno.recorte = matriz;
    avisar('Recorte da areia atualizado.');
  }, avisar);
});

el('chuva').addEventListener('input', (e) => { agua.taxaChuva = Number(e.target.value) * 0.35; });
el('evaporacao').addEventListener('input', (e) => { agua.evaporacao = Number(e.target.value) * 0.02; });
el('dilluvio').addEventListener('click', () => agua.encher(0.05));
el('secar').addEventListener('click', () => { agua.zerar(); terreno.limparChuva(); });
el('limparMarcadores').addEventListener('click', () => terreno.limparMarcadores());

el('curvas').addEventListener('change', (e) => { vista.curvas = e.target.checked; });
el('intervalo').addEventListener('input', (e) => { vista.intervalo = Number(e.target.value); });
el('espelhar').addEventListener('change', (e) => { vista.espelhar = e.target.checked; });
el('telaCheia').addEventListener('click', alternarTelaCheia);

function alternarTelaCheia() {
  if (document.fullscreenElement) document.exitFullscreen();
  else document.documentElement.requestFullscreen?.().catch(() => avisar('O navegador recusou a tela cheia.'));
}

function alternarPainel(mostrar) {
  el('painel').hidden = !mostrar;
  el('abrirPainel').hidden = mostrar;
}
el('fecharPainel').addEventListener('click', () => alternarPainel(false));
el('abrirPainel').addEventListener('click', () => alternarPainel(true));

addEventListener('keydown', (e) => {
  if (e.target.matches('input, select, textarea')) return;
  if (e.key === 'Tab') { e.preventDefault(); alternarPainel(el('painel').hidden); }
  if (e.key === 'f' || e.key === 'F') alternarTelaCheia();
  if (e.key === ' ') { e.preventDefault(); estado.pausado = !estado.pausado; }
  if (e.key === 'c' || e.key === 'C') { agua.zerar(); terreno.limparChuva(); }
  if (e.key === 'n' || e.key === 'N') terreno.gerar();
  if (e.key >= '1' && e.key <= '9') {
    const cenario = estado.cenarios[Number(e.key) - 1];
    if (cenario) aplicarCenario(cenario);
  }
});

// ------------------------------------------------------- pincel de mouse

let arrastando = 0;

function posicaoNoCampo(evento) {
  const r = canvas.getBoundingClientRect();
  let u = (evento.clientX - r.left) / r.width;
  const v = 1 - (evento.clientY - r.top) / r.height;
  if (vista.espelhar) u = 1 - u;
  return [u, v];
}

canvas.addEventListener('contextmenu', (e) => e.preventDefault());

canvas.addEventListener('pointerdown', (e) => {
  canvas.setPointerCapture(e.pointerId);
  arrastando = e.button === 2 ? -1 : 1;
  pincelar(e);
});

canvas.addEventListener('pointermove', (e) => { if (arrastando) pincelar(e); });
canvas.addEventListener('pointerup', () => { arrastando = 0; });
canvas.addEventListener('pointercancel', () => { arrastando = 0; });

function pincelar(evento) {
  const [u, v] = posicaoNoCampo(evento);
  if (evento.altKey) terreno.marcar(u, v, 0.03);
  else if (evento.shiftKey) terreno.chover(u, v, 0.05);
  else if (estado.fonte === 'demo') terreno.esculpir(u, v, 0.07, arrastando * 0.035);
}

// ------------------------------------------------------------------- laço

function ajustarTamanho() {
  const escala = Math.min(devicePixelRatio || 1, 2);
  const l = Math.round(canvas.clientWidth * escala);
  const a = Math.round(canvas.clientHeight * escala);
  if (canvas.width !== l || canvas.height !== a) {
    canvas.width = l;
    canvas.height = a;
  }
}

let ultimoInstante = performance.now();
let contador = 0;
let acumulado = 0;

function laco(agora) {
  requestAnimationFrame(laco);

  const dt = Math.min((agora - ultimoInstante) / 1000, 0.05);
  ultimoInstante = agora;
  if (estado.pausado) return;
  estado.tempo += dt;

  if (estado.fonte === 'ponte' && ponte.profundidade) {
    terreno.aplicarQuadroKinect(ponte.profundidade, ponte.largura, ponte.altura);
    terreno.atualizarChuvaDaMao();
  }

  // Passos fixos mantêm a simulação estável em qualquer taxa de quadros.
  const passos = 3;
  for (let i = 0; i < passos; i++) agua.passo(terreno.textura, 0.016);

  ajustarTamanho();
  paraTela(gl, canvas.width, canvas.height);
  vista.desenhar({
    terreno: terreno.textura,
    agua: agua.textura,
    marcadores: terreno.texturaMarcadores,
    tempo: estado.tempo,
    texel: [1 / GRADE_L, 1 / GRADE_A],
  });

  acumulado += dt;
  contador++;
  if (acumulado >= 0.5) {
    el('fps').textContent = `${Math.round(contador / acumulado)} fps`;
    acumulado = 0;
    contador = 0;
  }
}

// ------------------------------------------------------------------ boot

listarCenarios()
  .then((cenarios) => {
    estado.cenarios = cenarios;
    montarFiltros(cenarios);
    aplicarCenario(cenarios[0]);
    montarListaDeCenarios(cenarios);
  })
  .catch((erro) => avisar(erro.message));

trocarFonte('demo');
requestAnimationFrame(laco);

if ('serviceWorker' in navigator && location.protocol.startsWith('http')) {
  navigator.serviceWorker.register('sw.js').catch(() => {
    // Sem service worker o app continua funcionando, só perde o modo offline.
  });
}
