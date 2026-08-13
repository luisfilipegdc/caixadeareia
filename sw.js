// Service worker: depois da primeira visita a caixa abre sem internet.
// Estratégia: rede primeiro (para o professor receber cenários novos), com o
// cache como rede de segurança quando a escola está offline.

const CACHE = 'caixa-de-areia-v1';

const ESSENCIAIS = [
  '.',
  'index.html',
  'manifest.webmanifest',
  'css/app.css',
  'icones/icone.svg',
  'src/main.js',
  'src/cenarios.js',
  'src/gl/gl.js',
  'src/sim/terreno.js',
  'src/sim/agua.js',
  'src/render/vista.js',
  'src/scan/homografia.js',
  'src/fonte/ponte-kinect.js',
  'src/ui/quiz.js',
  'src/ui/calibracao.js',
  'cenarios/index.json',
];

self.addEventListener('install', (evento) => {
  evento.waitUntil(
    caches.open(CACHE)
      .then((cache) => cache.addAll(ESSENCIAIS))
      .then(() => self.skipWaiting())
      .catch(() => self.skipWaiting()), // uma URL faltando não bloqueia a instalação
  );
});

self.addEventListener('activate', (evento) => {
  evento.waitUntil(
    caches.keys()
      .then((chaves) => Promise.all(chaves.filter((c) => c !== CACHE).map((c) => caches.delete(c))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener('fetch', (evento) => {
  const { request } = evento;
  if (request.method !== 'GET') return;
  if (!request.url.startsWith(self.location.origin)) return;

  evento.respondWith(
    fetch(request)
      .then((resposta) => {
        const copia = resposta.clone();
        caches.open(CACHE).then((cache) => cache.put(request, copia)).catch(() => {});
        return resposta;
      })
      .catch(() => caches.match(request).then((guardado) => guardado
        ?? new Response('Offline e sem cópia guardada deste arquivo.', {
          status: 504,
          headers: { 'Content-Type': 'text/plain; charset=utf-8' },
        }))),
  );
});
