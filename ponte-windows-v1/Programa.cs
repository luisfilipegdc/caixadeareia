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
                    servidor = new ServidorWebSocket(porta)
                    {
                        MensagemDeBoasVindas = "{\"tipo\":\"ola\",\"sensor\":\""
                            + (simulado ? "simulado" : "kinect-v1")
                            + "\",\"mensagem\":\"" + (simulado
                                ? "Ponte no ar em modo simulado."
                                : "Kinect v1 transmitindo pelo SDK 1.8.") + "\"}",
                    };
                    Console.Error.WriteLine($"[ponte] escutando em ws://localhost:{porta}");
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

                if (!veio) continue;

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
                    Console.Error.WriteLine($"[ponte] {quadrosDesdeAviso / 10} quadros por segundo, "
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

        /// <summary>Um morro que respira, para validar a cadeia inteira sem sensor.</summary>
        private static void GerarRelevoFalso(ushort[] destino, double t)
        {
            for (int y = 0; y < Altura; y++)
            {
                double v = (double)y / Altura - 0.5;
                for (int x = 0; x < Largura; x++)
                {
                    double u = (double)x / Largura - 0.5;
                    double morro = Math.Exp(-((u + 0.2) * (u + 0.2) + v * v) * 12) * 180;
                    double onda = Math.Sin(u * 9 + t) * Math.Cos(v * 7 - t * 0.6) * 25;
                    destino[y * Largura + x] = (ushort)Math.Max(0, 1000 - morro - onda - 60);
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
