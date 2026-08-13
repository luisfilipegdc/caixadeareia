// Servidor estático mínimo, sem dependências.
//
// Serve para duas coisas: rodar o projeto na máquina durante o
// desenvolvimento (`npm run web`) e servir os arquivos de dentro do
// executável, já que módulos ES e fetch() não funcionam via file://.

import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import { extname, join, normalize, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const RAIZ = resolve(fileURLToPath(new URL('.', import.meta.url)));

const TIPOS = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.webmanifest': 'application/manifest+json; charset=utf-8',
  '.frag': 'text/plain; charset=utf-8',
  '.png': 'image/png',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

export function iniciarServidor(porta = 8080) {
  const servidor = createServer(async (req, res) => {
    try {
      const url = new URL(req.url, 'http://localhost');
      const pedido = url.pathname === '/' ? '/index.html' : url.pathname;

      // Impede que "../" escape da pasta do projeto.
      const caminho = join(RAIZ, normalize(decodeURIComponent(pedido)));
      if (!caminho.startsWith(RAIZ)) {
        res.writeHead(403).end('Acesso negado');
        return;
      }

      const conteudo = await readFile(caminho);
      res.writeHead(200, {
        'Content-Type': TIPOS[extname(caminho)] ?? 'application/octet-stream',
        'Cache-Control': 'no-cache',
      });
      res.end(conteudo);
    } catch (erro) {
      const codigo = erro.code === 'ENOENT' ? 404 : 500;
      res.writeHead(codigo, { 'Content-Type': 'text/plain; charset=utf-8' });
      res.end(codigo === 404 ? 'Arquivo não encontrado' : 'Erro no servidor');
    }
  });

  return new Promise((cumprir, rejeitar) => {
    servidor.on('error', rejeitar);
    servidor.listen(porta, '127.0.0.1', () => cumprir({ servidor, porta: servidor.address().port }));
  });
}

// Executado direto pela linha de comando.
if (process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url))) {
  const porta = Number(process.env.PORTA ?? 8080);
  iniciarServidor(porta).then(({ porta: usada }) => {
    console.log(`Caixa de Areia em http://localhost:${usada}`);
  });
}
