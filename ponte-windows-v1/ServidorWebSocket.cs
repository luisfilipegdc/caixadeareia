using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CaixaDeAreia
{
    /// <summary>
    /// Servidor WebSocket mínimo, escrito à mão.
    ///
    /// Poderia usar HttpListener, mas ele exige registro de URL com permissão
    /// de administrador em boa parte das instalações do Windows — e a escola
    /// não vai abrir prompt elevado para dar aula. Um TcpListener em 127.0.0.1
    /// não pede nada.
    ///
    /// Fala o mesmo protocolo da ponte em Node: quadros binários com cabeçalho
    /// "KNCT" e confirmação de recebimento por mensagem de texto "pronto".
    /// </summary>
    internal sealed class ServidorWebSocket : IDisposable
    {
        private const string GuidWebSocket = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly TcpListener _escuta;
        private readonly List<Cliente> _clientes = new List<Cliente>();
        private readonly object _trava = new object();
        private bool _encerrando;

        public int Porta { get; }
        public string MensagemDeBoasVindas { get; set; } = "";

        private sealed class Cliente
        {
            public TcpClient Tcp;
            public NetworkStream Fluxo;
            public bool Pronto = true;
            public DateTime EnviadoEm = DateTime.MinValue;
        }

        public ServidorWebSocket(int porta)
        {
            Porta = porta;
            _escuta = new TcpListener(IPAddress.Loopback, porta);
            _escuta.Start();
            AceitarProximo();
        }

        public int Conectados
        {
            get { lock (_trava) { return _clientes.Count; } }
        }

        private void AceitarProximo()
        {
            _escuta.BeginAceitarSeguro(resultado =>
            {
                if (_encerrando) return;

                TcpClient tcp = null;
                try { tcp = _escuta.EndAcceptTcpClient(resultado); }
                catch { /* servidor fechando */ }

                AceitarProximo();

                if (tcp == null) return;
                try { ApertarMao(tcp); }
                catch (Exception erro)
                {
                    Console.Error.WriteLine("[ponte] handshake falhou: " + erro.Message);
                    try { tcp.Close(); } catch { }
                }
            });
        }

        private void ApertarMao(TcpClient tcp)
        {
            tcp.NoDelay = true;
            NetworkStream fluxo = tcp.GetStream();

            // Lê o cabeçalho HTTP até a linha em branco.
            var pedido = new StringBuilder();
            var byteAByte = new byte[1];
            while (!pedido.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (fluxo.Read(byteAByte, 0, 1) == 0) throw new IOException("conexão fechou durante o handshake");
                pedido.Append((char)byteAByte[0]);
                if (pedido.Length > 8192) throw new IOException("cabeçalho grande demais");
            }

            Match chave = Regex.Match(pedido.ToString(), @"Sec-WebSocket-Key:\s*(.+)", RegexOptions.IgnoreCase);
            if (!chave.Success) throw new IOException("pedido não é WebSocket");

            string aceite;
            using (var sha1 = SHA1.Create())
            {
                byte[] resumo = sha1.ComputeHash(Encoding.UTF8.GetBytes(chave.Groups[1].Value.Trim() + GuidWebSocket));
                aceite = Convert.ToBase64String(resumo);
            }

            byte[] resposta = Encoding.UTF8.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n"
                + "Upgrade: websocket\r\n"
                + "Connection: Upgrade\r\n"
                + "Sec-WebSocket-Accept: " + aceite + "\r\n\r\n");
            fluxo.Write(resposta, 0, resposta.Length);

            var cliente = new Cliente { Tcp = tcp, Fluxo = fluxo };
            lock (_trava) { _clientes.Add(cliente); }
            Console.Error.WriteLine($"[ponte] navegador conectado ({Conectados} ativo(s))");

            if (MensagemDeBoasVindas.Length > 0) EnviarTexto(cliente, MensagemDeBoasVindas);

            var leitor = new System.Threading.Thread(() => Ouvir(cliente)) { IsBackground = true };
            leitor.Start();
        }

        /// <summary>Lê as mensagens do navegador: só nos interessa a confirmação.</summary>
        private void Ouvir(Cliente cliente)
        {
            var cabecalho = new byte[2];
            try
            {
                while (!_encerrando)
                {
                    if (!LerExatamente(cliente.Fluxo, cabecalho, 2)) break;

                    int opcode = cabecalho[0] & 0x0F;
                    bool mascarado = (cabecalho[1] & 0x80) != 0;
                    long tamanho = cabecalho[1] & 0x7F;

                    if (tamanho == 126)
                    {
                        var extra = new byte[2];
                        if (!LerExatamente(cliente.Fluxo, extra, 2)) break;
                        tamanho = (extra[0] << 8) | extra[1];
                    }
                    else if (tamanho == 127)
                    {
                        var extra = new byte[8];
                        if (!LerExatamente(cliente.Fluxo, extra, 8)) break;
                        tamanho = 0;
                        for (int i = 0; i < 8; i++) tamanho = (tamanho << 8) | extra[i];
                    }

                    if (tamanho > 1 << 20) break; // navegador não manda nada grande aqui

                    var mascara = new byte[4];
                    if (mascarado && !LerExatamente(cliente.Fluxo, mascara, 4)) break;

                    var carga = new byte[tamanho];
                    if (tamanho > 0 && !LerExatamente(cliente.Fluxo, carga, (int)tamanho)) break;
                    if (mascarado)
                    {
                        for (int i = 0; i < carga.Length; i++) carga[i] ^= mascara[i % 4];
                    }

                    if (opcode == 0x8) break;                       // fechamento
                    if (opcode == 0x9) EnviarQuadro(cliente, 0xA, carga); // ping -> pong
                    if (opcode == 0x1 && Encoding.UTF8.GetString(carga) == "pronto") cliente.Pronto = true;
                }
            }
            catch { /* queda de conexão é rotina */ }
            finally { Remover(cliente); }
        }

        private static bool LerExatamente(NetworkStream fluxo, byte[] destino, int quantos)
        {
            int lidos = 0;
            while (lidos < quantos)
            {
                int agora = fluxo.Read(destino, lidos, quantos - lidos);
                if (agora <= 0) return false;
                lidos += agora;
            }
            return true;
        }

        /// <summary>
        /// Manda o quadro a quem já confirmou o anterior. Quem está atrasado
        /// simplesmente perde este quadro: relevo velho não serve para nada.
        /// </summary>
        public void Transmitir(byte[] pacote)
        {
            List<Cliente> alvos;
            lock (_trava) { alvos = new List<Cliente>(_clientes); }

            DateTime agora = DateTime.UtcNow;
            foreach (Cliente cliente in alvos)
            {
                if (!cliente.Pronto && (agora - cliente.EnviadoEm).TotalMilliseconds < 250) continue;

                cliente.Pronto = false;
                cliente.EnviadoEm = agora;
                if (!EnviarQuadro(cliente, 0x2, pacote)) Remover(cliente);
            }
        }

        private void EnviarTexto(Cliente cliente, string texto)
        {
            EnviarQuadro(cliente, 0x1, Encoding.UTF8.GetBytes(texto));
        }

        private bool EnviarQuadro(Cliente cliente, int opcode, byte[] carga)
        {
            try
            {
                byte[] cabecalho;
                if (carga.Length <= 125)
                {
                    cabecalho = new byte[] { (byte)(0x80 | opcode), (byte)carga.Length };
                }
                else if (carga.Length <= ushort.MaxValue)
                {
                    cabecalho = new byte[4];
                    cabecalho[0] = (byte)(0x80 | opcode);
                    cabecalho[1] = 126;
                    cabecalho[2] = (byte)(carga.Length >> 8);
                    cabecalho[3] = (byte)carga.Length;
                }
                else
                {
                    cabecalho = new byte[10];
                    cabecalho[0] = (byte)(0x80 | opcode);
                    cabecalho[1] = 127;
                    long n = carga.Length;
                    for (int i = 0; i < 8; i++) cabecalho[9 - i] = (byte)(n >> (8 * i));
                }

                lock (cliente)
                {
                    cliente.Fluxo.Write(cabecalho, 0, cabecalho.Length);
                    cliente.Fluxo.Write(carga, 0, carga.Length);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void Remover(Cliente cliente)
        {
            bool saiu;
            lock (_trava) { saiu = _clientes.Remove(cliente); }
            if (!saiu) return;

            try { cliente.Tcp.Close(); } catch { }
            Console.Error.WriteLine($"[ponte] navegador saiu ({Conectados} ativo(s))");
        }

        public void Dispose()
        {
            _encerrando = true;
            List<Cliente> alvos;
            lock (_trava) { alvos = new List<Cliente>(_clientes); _clientes.Clear(); }

            foreach (Cliente cliente in alvos)
            {
                // Quadro de fechamento: sem ele o navegador só percebe a queda
                // quando o TCP expira, e demora a reconectar.
                EnviarQuadro(cliente, 0x8, new byte[] { 0x03, 0xE9 });
                try { cliente.Tcp.Close(); } catch { }
            }

            try { _escuta.Stop(); } catch { }
        }
    }

    internal static class ExtensoesDeEscuta
    {
        /// <summary>BeginAcceptTcpClient que não estoura quando o servidor já parou.</summary>
        public static void BeginAceitarSeguro(this TcpListener escuta, AsyncCallback aoAceitar)
        {
            try { escuta.BeginAcceptTcpClient(aoAceitar, null); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
}
