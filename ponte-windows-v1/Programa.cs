using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace CaixaDeAreia
{
    /// <summary>
    /// Ponte Kinect v1 para Windows, usando o SDK 1.8.
    ///
    ///   PonteKinect.exe                  abre o sensor e serve em ws://localhost:8787
    ///   PonteKinect.exe --perto          modo perto (40cm a 3m), só no modelo 1517
    ///   PonteKinect.exe --angulo -10     inclina o motor do sensor
    ///   PonteKinect.exe --porta 9000     outra porta
    ///   PonteKinect.exe --simulado       relevo falso, para testar sem sensor
    ///   PonteKinect.exe --saida-padrao   despeja quadros crus na saída padrão
    ///
    /// O último modo existe para encadear com a ponte em Node:
    ///   PonteKinect.exe --saida-padrao | node ponte/ponte.js
    /// </summary>
    internal static class Programa
    {
        private const int Largura = Sensor.Largura;
        private const int Altura = Sensor.Altura;
        private const int Cabecalho = 12;

        private static int Main(string[] argumentos)
        {
            int porta = LerInteiro(argumentos, "--porta", 8787);
            int angulo = LerInteiro(argumentos, "--angulo", int.MinValue);
            bool perto = Tem(argumentos, "--perto");
            bool simulado = Tem(argumentos, "--simulado");
            bool saidaPadrao = Tem(argumentos, "--saida-padrao");
            bool naRede = Tem(argumentos, "--rede");

            Console.Error.WriteLine("Ponte Kinect v1 — Caixa de Areia Interativa\n");

            Sensor sensor = null;
            if (!simulado)
            {
                sensor = Sensor.Abrir(perto, angulo, out string motivo);
                if (sensor == null)
                {
                    Console.Error.WriteLine("[ponte] " + motivo);
                    Console.Error.WriteLine("[ponte] seguindo em modo simulado; use --simulado para não ver este aviso\n");
                    simulado = true;
                }
                else
                {
                    Console.Error.WriteLine($"[ponte] sensor aberto: {sensor.Nome}");
                }
            }

            var profundidade = new ushort[Largura * Altura];
            var pacote = new byte[Cabecalho + profundidade.Length * 2];
            pacote[0] = (byte)'K'; pacote[1] = (byte)'N'; pacote[2] = (byte)'C'; pacote[3] = (byte)'T';
            EscreverUInt16(pacote, 4, Largura);
            EscreverUInt16(pacote, 6, Altura);

            ServidorWebSocket servidor = null;
            Stream saida = null;

            if (saidaPadrao)
            {
                saida = Console.OpenStandardOutput();
                Console.Error.WriteLine("[ponte] despejando quadros crus na saída padrão");
            }
            else
            {
                try
                {
                    servidor = new ServidorWebSocket(porta, naRede)
                    {
                        MensagemDeBoasVindas = "{\"tipo\":\"ola\",\"sensor\":\""
                            + (simulado ? "simulado" : "kinect-v1")
                            + "\",\"mensagem\":\"" + (simulado
                                ? "Ponte no ar em modo simulado."
                                : "Kinect v1 transmitindo pelo SDK 1.8.") + "\"}",
                    };
                    Console.Error.WriteLine($"[ponte] escutando em ws://localhost:{porta}");
                    if (naRede)
                    {
                        foreach (string endereco in EnderecosDaMaquina())
                        {
                            Console.Error.WriteLine($"[ponte] na rede local: ws://{endereco}:{porta}");
                        }
                        Console.Error.WriteLine("[ponte] libere a porta no Firewall do Windows se o outro "
                            + "computador não conseguir conectar");
                    }
                }
                catch (Exception erro)
                {
                    Console.Error.WriteLine($"[ponte] não consegui abrir a porta {porta}: {erro.Message}");
                    Console.Error.WriteLine("[ponte] outra ponte já deve estar no ar; encerrando para não atrapalhar.");
                    sensor?.Dispose();
                    return 0;
                }
            }

            var encerrar = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; encerrar.Set(); };

            uint numeroDoQuadro = 0;
            int vaziosSeguidos = 0;
            var relogio = Stopwatch.StartNew();
            var ultimoAviso = TimeSpan.Zero;
            int quadrosDesdeAviso = 0;

            while (!encerrar.IsSet)
            {
                bool veio;
                if (simulado)
                {
                    GerarRelevoFalso(profundidade, relogio.Elapsed.TotalSeconds);
                    Thread.Sleep(33);
                    veio = true;
                }
                else
                {
                    veio = sensor.LerQuadro(profundidade);
                }

                if (!veio)
                {
                    // Sensor aberto e mudo é sintoma próprio: quase sempre
                    // outro programa segurando a câmera, ou cabo USB ruim.
                    if (++vaziosSeguidos == 10)
                    {
                        Console.Error.WriteLine("[ponte] o sensor abriu mas não está mandando quadros. "
                            + "Feche o Kinect Studio ou outro programa que use a câmera, e troque a porta USB.");
                    }
                    continue;
                }
                vaziosSeguidos = 0;

                EscreverUInt32(pacote, 8, ++numeroDoQuadro);
                Buffer.BlockCopy(profundidade, 0, pacote, Cabecalho, profundidade.Length * 2);

                if (saida != null)
                {
                    // No encadeamento vai só a profundidade crua, sem cabeçalho.
                    saida.Write(pacote, Cabecalho, profundidade.Length * 2);
                    saida.Flush();
                }
                else
                {
                    servidor.Transmitir(pacote);
                }

                quadrosDesdeAviso++;
                if (relogio.Elapsed - ultimoAviso > TimeSpan.FromSeconds(10))
                {
                    // A origem vai em toda linha de propósito: o começo da
                    // execução some do terminal em minutos, e sem isso não dá
                    // para saber se o que está na tela é a areia ou o relevo
                    // de mentira.
                    Console.Error.WriteLine($"[ponte] {(simulado ? "SIMULADO (sem sensor)" : "Kinect v1")} — "
                        + $"{quadrosDesdeAviso / 10} quadros por segundo, "
                        + (servidor != null ? $"{servidor.Conectados} navegador(es)" : "encadeado"));
                    quadrosDesdeAviso = 0;
                    ultimoAviso = relogio.Elapsed;
                }
            }

            Console.Error.WriteLine("\n[ponte] encerrando");
            servidor?.Dispose();
            sensor?.Dispose();
            return 0;
        }

        private static double[] _morroFixo;

        /// <summary>
        /// Um morro que respira, para validar a cadeia inteira sem sensor.
        ///
        /// A parte fixa é calculada uma vez só: fazendo exponencial em 300 mil
        /// pixels a cada quadro, o modo simulado rodava a 9 quadros por segundo
        /// e chegava a ser confundido com sensor real lendo mal.
        /// </summary>
        private static void GerarRelevoFalso(ushort[] destino, double t)
        {
            if (_morroFixo == null)
            {
                _morroFixo = new double[Largura * Altura];
                for (int y = 0; y < Altura; y++)
                {
                    double v = (double)y / Altura - 0.5;
                    for (int x = 0; x < Largura; x++)
                    {
                        double u = (double)x / Largura - 0.5;
                        _morroFixo[y * Largura + x] = Math.Exp(-((u + 0.2) * (u + 0.2) + v * v) * 12) * 180;
                    }
                }
            }

            // A onda vira produto de dois vetores, um por eixo: 1120 senos por
            // quadro em vez de 600 mil.
            var porColuna = new double[Largura];
            for (int x = 0; x < Largura; x++)
            {
                porColuna[x] = Math.Sin(((double)x / Largura - 0.5) * 9 + t);
            }

            for (int y = 0; y < Altura; y++)
            {
                double porLinha = Math.Cos(((double)y / Altura - 0.5) * 7 - t * 0.6) * 25;
                int inicio = y * Largura;
                for (int x = 0; x < Largura; x++)
                {
                    double valor = 1000 - _morroFixo[inicio + x] - porColuna[x] * porLinha - 60;
                    destino[inicio + x] = (ushort)Math.Max(0, valor);
                }
            }
        }

        private static void EscreverUInt16(byte[] destino, int posicao, int valor)
        {
            destino[posicao] = (byte)valor;
            destino[posicao + 1] = (byte)(valor >> 8);
        }

        private static void EscreverUInt32(byte[] destino, int posicao, uint valor)
        {
            destino[posicao] = (byte)valor;
            destino[posicao + 1] = (byte)(valor >> 8);
            destino[posicao + 2] = (byte)(valor >> 16);
            destino[posicao + 3] = (byte)(valor >> 24);
        }

        /// <summary>Endereços IPv4 desta máquina, para mostrar a quem vai conectar de fora.</summary>
        private static System.Collections.Generic.IEnumerable<string> EnderecosDaMaquina()
        {
            var achados = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var endereco in System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName()))
                {
                    if (endereco.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        achados.Add(endereco.ToString());
                    }
                }
            }
            catch
            {
                // Sem rede configurada: seguimos com o que der.
            }
            return achados;
        }

        private static bool Tem(string[] argumentos, string nome)
        {
            return Array.IndexOf(argumentos, nome) >= 0;
        }

        private static int LerInteiro(string[] argumentos, string nome, int padrao)
        {
            int onde = Array.IndexOf(argumentos, nome);
            if (onde < 0 || onde + 1 >= argumentos.Length) return padrao;
            return int.TryParse(argumentos[onde + 1], out int valor) ? valor : padrao;
        }
    }
}
