// Carregamento dos cenários.
//
// Um cenário é uma pasta em cenarios/<id>/ com um cenario.json e, se quiser,
// um overlay.frag. Adicionar um cenário novo é criar a pasta e citar o id em
// cenarios/index.json — nenhum código do motor muda.

const BASE = 'cenarios';

export async function listarCenarios() {
  const resposta = await fetch(`${BASE}/index.json`, { cache: 'no-cache' });
  if (!resposta.ok) throw new Error(`Não achei ${BASE}/index.json (${resposta.status}).`);
  const { cenarios } = await resposta.json();

  const carregados = await Promise.all(cenarios.map((id) => carregarCenario(id).catch((erro) => {
    console.error(`Cenário "${id}" ignorado:`, erro);
    return null;
  })));

  const validos = carregados.filter(Boolean);
  if (!validos.length) throw new Error('Nenhum cenário pôde ser carregado.');
  return validos;
}

export async function carregarCenario(id) {
  const resposta = await fetch(`${BASE}/${id}/cenario.json`, { cache: 'no-cache' });
  if (!resposta.ok) throw new Error(`cenario.json ausente (${resposta.status}).`);
  const dados = await resposta.json();

  if (!Array.isArray(dados.paleta) || dados.paleta.length < 2) {
    throw new Error('O cenário precisa de uma paleta com pelo menos duas paradas.');
  }

  if (dados.overlay) {
    const shader = await fetch(`${BASE}/${id}/${dados.overlay}`, { cache: 'no-cache' });
    if (!shader.ok) throw new Error(`overlay "${dados.overlay}" ausente (${shader.status}).`);
    dados.trechoShader = await shader.text();
  }

  dados.id = dados.id || id;
  dados.pasta = `${BASE}/${id}`;
  return dados;
}

/** Cor representativa do cenário, para a miniatura do painel. */
export function corDeAmostra(cenario) {
  const meio = cenario.paleta[Math.floor(cenario.paleta.length / 2)];
  return meio?.cor ?? '#888';
}
