// Calibração de 4 cantos.
//
// O professor clica nos quatro cantos da areia como eles aparecem na imagem
// projetada, em sentido horário a partir do canto inferior esquerdo. Com isso
// montamos a homografia que alinha a projeção ao que o Kinect enxerga.

import { homografia, CANTOS_PADRAO } from '../scan/homografia.js';

const ORDEM = [
  'inferior esquerdo',
  'inferior direito',
  'superior direito',
  'superior esquerdo',
];

export function iniciarCalibracao(canvas, aoConcluir, avisar) {
  const pontos = [];
  const camada = document.createElement('div');

  Object.assign(camada.style, {
    position: 'fixed',
    inset: '0',
    zIndex: '20',
    cursor: 'crosshair',
    background: 'rgba(4, 10, 22, .55)',
    color: '#e8eefc',
    font: '16px system-ui, sans-serif',
    display: 'grid',
    placeContent: 'center',
    textAlign: 'center',
    padding: '24px',
  });

  const texto = document.createElement('p');
  camada.appendChild(texto);
  document.body.appendChild(camada);

  function atualizarTexto() {
    texto.innerHTML = `Clique no canto <strong>${ORDEM[pontos.length]}</strong> da areia`
      + `<br><small>${pontos.length}/4 — Esc cancela</small>`;
  }

  function marcar(evento) {
    const r = canvas.getBoundingClientRect();
    const u = (evento.clientX - r.left) / r.width;
    const v = 1 - (evento.clientY - r.top) / r.height;
    pontos.push([u, v]);

    const marca = document.createElement('div');
    Object.assign(marca.style, {
      position: 'fixed',
      left: `${evento.clientX - 6}px`,
      top: `${evento.clientY - 6}px`,
      width: '12px',
      height: '12px',
      borderRadius: '50%',
      background: '#4fc3f7',
      pointerEvents: 'none',
    });
    camada.appendChild(marca);

    if (pontos.length === 4) concluir();
    else atualizarTexto();
  }

  function concluir() {
    // Do espaço projetado de volta ao sensor: é o caminho que o shader faz.
    const matriz = homografia(CANTOS_PADRAO, pontos);
    encerrar();
    if (!matriz) {
      avisar?.('Os quatro pontos não formam um quadrilátero válido. Tente de novo.');
      return;
    }
    aoConcluir(matriz);
  }

  function cancelar(evento) {
    if (evento.key !== 'Escape') return;
    encerrar();
    avisar?.('Calibração cancelada.');
  }

  function encerrar() {
    camada.remove();
    removeEventListener('keydown', cancelar);
  }

  camada.addEventListener('click', marcar);
  addEventListener('keydown', cancelar);
  atualizarTexto();
}
