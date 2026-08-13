// Quiz do cenário: perguntas de múltipla escolha conduzidas pelo professor.

export function montarQuiz({ secao, pergunta, alternativas, anterior, proxima }) {
  let perguntas = [];
  let indice = 0;
  let respondida = false;

  function desenhar() {
    const atual = perguntas[indice];
    if (!atual) return;

    respondida = false;
    pergunta.textContent = `${indice + 1}/${perguntas.length} — ${atual.pergunta}`;
    alternativas.textContent = '';

    atual.alternativas.forEach((texto, i) => {
      const botao = document.createElement('button');
      botao.textContent = texto;
      botao.addEventListener('click', () => responder(botao, i, atual));
      alternativas.appendChild(botao);
    });

    anterior.disabled = indice === 0;
    proxima.disabled = indice === perguntas.length - 1;
  }

  function responder(botao, escolha, atual) {
    if (respondida) return;
    respondida = true;

    const botoes = [...alternativas.querySelectorAll('button')];
    botoes.forEach((b, i) => {
      if (i === atual.correta) b.classList.add('certa');
      else if (i === escolha) b.classList.add('errada');
    });

    if (atual.explicacao) {
      const nota = document.createElement('p');
      nota.className = 'dica';
      nota.textContent = atual.explicacao;
      alternativas.appendChild(nota);
    }
  }

  anterior.addEventListener('click', () => { if (indice > 0) { indice--; desenhar(); } });
  proxima.addEventListener('click', () => { if (indice < perguntas.length - 1) { indice++; desenhar(); } });

  return {
    definir(lista) {
      perguntas = Array.isArray(lista) ? lista : [];
      indice = 0;
      secao.hidden = perguntas.length === 0;
      if (perguntas.length) desenhar();
    },
  };
}
