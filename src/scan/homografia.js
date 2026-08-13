// Homografia de 4 pontos: mapeia os cantos da areia vistos pelo Kinect para
// o retângulo da imagem projetada. É o que faz a projeção cair exatamente
// sobre o relevo real.

// Resolve o sistema linear A·x = b por eliminação de Gauss com pivotamento.
function resolver(A, b) {
  const n = b.length;
  const M = A.map((linha, i) => [...linha, b[i]]);

  for (let coluna = 0; coluna < n; coluna++) {
    let pivo = coluna;
    for (let linha = coluna + 1; linha < n; linha++) {
      if (Math.abs(M[linha][coluna]) > Math.abs(M[pivo][coluna])) pivo = linha;
    }
    if (Math.abs(M[pivo][coluna]) < 1e-12) return null; // pontos degenerados
    [M[coluna], M[pivo]] = [M[pivo], M[coluna]];

    for (let linha = 0; linha < n; linha++) {
      if (linha === coluna) continue;
      const fator = M[linha][coluna] / M[coluna][coluna];
      if (fator === 0) continue;
      for (let k = coluna; k <= n; k++) M[linha][k] -= fator * M[coluna][k];
    }
  }

  return M.map((linha, i) => linha[n] / linha[i]);
}

/**
 * Calcula a matriz 3x3 que leva os quatro pontos de origem aos de destino.
 * @param {Array<[number,number]>} origem  4 pontos (u,v) na imagem do sensor
 * @param {Array<[number,number]>} destino 4 pontos (x,y) no espaço projetado
 * @returns {Float32Array|null} matriz em ordem de coluna (pronta para uniformMatrix3fv)
 */
export function homografia(origem, destino) {
  if (origem.length !== 4 || destino.length !== 4) {
    throw new Error('A homografia precisa de exatamente 4 pares de pontos.');
  }

  const A = [];
  const b = [];
  for (let i = 0; i < 4; i++) {
    const [u, v] = origem[i];
    const [x, y] = destino[i];
    A.push([u, v, 1, 0, 0, 0, -x * u, -x * v]);
    b.push(x);
    A.push([0, 0, 0, u, v, 1, -y * u, -y * v]);
    b.push(y);
  }

  const h = resolver(A, b);
  if (!h) return null;

  // Ordem de coluna, como o WebGL espera.
  return new Float32Array([
    h[0], h[3], h[6],
    h[1], h[4], h[7],
    h[2], h[5], 1,
  ]);
}

export function identidade() {
  return new Float32Array([1, 0, 0, 0, 1, 0, 0, 0, 1]);
}

/** Aplica a homografia a um ponto, para conferência fora do shader. */
export function aplicar(H, [u, v]) {
  const x = H[0] * u + H[3] * v + H[6];
  const y = H[1] * u + H[4] * v + H[7];
  const w = H[2] * u + H[5] * v + H[8];
  return [x / w, y / w];
}

/** Cantos padrão do retângulo unitário, na ordem SE, SD, ND, NE. */
export const CANTOS_PADRAO = [[0, 0], [1, 0], [1, 1], [0, 1]];
