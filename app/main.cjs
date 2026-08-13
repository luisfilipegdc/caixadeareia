// Casca Electron: gera o executável para a escola.
//
// Sobe o servidor local, sobe a ponte do Kinect como processo filho e abre a
// janela em tela cheia. O usuário liga o PC, dá dois cliques e usa.

const { app, BrowserWindow, globalShortcut, screen } = require('electron');
const { fork } = require('node:child_process');
const path = require('node:path');

let janela = null;
let ponte = null;
let servidor = null;

async function subirServidor() {
  const modulo = await import(path.join(__dirname, '..', 'servidor-local.js'));
  const { servidor: instancia, porta } = await modulo.iniciarServidor(0); // porta livre
  servidor = instancia;
  return porta;
}

function subirPonte() {
  const caminho = path.join(__dirname, '..', 'ponte', 'ponte.js');
  ponte = fork(caminho, [], { stdio: ['ignore', 'pipe', 'pipe', 'ipc'] });
  ponte.stdout?.on('data', (d) => process.stdout.write(`[ponte] ${d}`));
  ponte.stderr?.on('data', (d) => process.stderr.write(`[ponte] ${d}`));
  ponte.on('exit', (codigo) => {
    ponte = null;
    if (codigo) console.error(`[app] a ponte terminou com código ${codigo}`);
  });
}

async function criarJanela() {
  const porta = await subirServidor();
  subirPonte();

  // Com projetor ligado, prefere o segundo monitor.
  const telas = screen.getAllDisplays();
  const alvo = telas.find((t) => t.bounds.x !== 0 || t.bounds.y !== 0) ?? screen.getPrimaryDisplay();

  janela = new BrowserWindow({
    x: alvo.bounds.x,
    y: alvo.bounds.y,
    width: alvo.bounds.width,
    height: alvo.bounds.height,
    backgroundColor: '#0b1220',
    autoHideMenuBar: true,
    fullscreen: telas.length > 1,
    webPreferences: {
      contextIsolation: true,
      nodeIntegration: false,
      backgroundThrottling: false,
    },
  });

  janela.loadURL(`http://127.0.0.1:${porta}/`);
  janela.on('closed', () => { janela = null; });
}

app.whenReady().then(async () => {
  await criarJanela();

  globalShortcut.register('F11', () => janela?.setFullScreen(!janela.isFullScreen()));
  globalShortcut.register('CommandOrControl+R', () => janela?.reload());
  globalShortcut.register('CommandOrControl+Shift+I', () => janela?.webContents.toggleDevTools());

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) criarJanela();
  });
});

app.on('window-all-closed', () => app.quit());

app.on('will-quit', () => {
  globalShortcut.unregisterAll();
  ponte?.kill();
  servidor?.close();
});
