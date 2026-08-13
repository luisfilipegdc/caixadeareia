using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace CaixaDeAreia
{
    /// <summary>
    /// Acesso ao Kinect v1 pelo SDK 1.8, inteiramente por reflexão.
    ///
    /// Por que reflexão: assim este projeto compila sem o SDK instalado (o
    /// runner do CI não tem), e o mesmo executável roda em máquina sem sensor.
    /// A API do SDK 1.8 é estável desde 2013, então o custo é baixo.
    ///
    /// Usamos o modelo de sondagem (OpenNextFrame) em vez de eventos: é uma
    /// chamada de método simples, muito mais fácil de acionar por reflexão do
    /// que assinar um evento com tipo de argumento desconhecido.
    /// </summary>
    internal sealed class Sensor : IDisposable
    {
        public const int Largura = 640;
        public const int Altura = 480;

        private static readonly string[] CaminhosDoSdk =
        {
            @"C:\Program Files\Microsoft SDKs\Kinect\v1.8\Assemblies\Microsoft.Kinect.dll",
            @"C:\Program Files (x86)\Microsoft SDKs\Kinect\v1.8\Assemblies\Microsoft.Kinect.dll",
        };

        private readonly object _sensor;
        private readonly object _fluxoDeProfundidade;
        private readonly MethodInfo _abrirProximoQuadro;
        private readonly short[] _cru = new short[Largura * Altura];

        public string Nome { get; private set; }

        private Sensor(object sensor, object fluxo, MethodInfo abrirProximoQuadro, string nome)
        {
            _sensor = sensor;
            _fluxoDeProfundidade = fluxo;
            _abrirProximoQuadro = abrirProximoQuadro;
            Nome = nome;
        }

        /// <summary>
        /// Abre o primeiro sensor conectado. Devolve null quando não há sensor
        /// ou o SDK não está instalado, com o motivo em <paramref name="motivo"/>.
        /// </summary>
        public static Sensor Abrir(bool modoPerto, int anguloDesejado, out string motivo)
        {
            Assembly kinect = CarregarBiblioteca(out motivo);
            if (kinect == null) return null;

            try
            {
                Type tipoSensor = kinect.GetType("Microsoft.Kinect.KinectSensor");
                if (tipoSensor == null)
                {
                    motivo = "Microsoft.Kinect.dll carregou mas não tem KinectSensor — versão inesperada do SDK.";
                    return null;
                }

                var colecao = (IEnumerable)tipoSensor
                    .GetProperty("KinectSensors", BindingFlags.Public | BindingFlags.Static)
                    .GetValue(null, null);

                object escolhido = null;
                int vistos = 0;
                foreach (object candidato in colecao)
                {
                    vistos++;
                    object estado = candidato.GetType().GetProperty("Status").GetValue(candidato, null);
                    if (estado != null && estado.ToString() == "Connected") { escolhido = candidato; break; }
                    Console.Error.WriteLine($"[ponte] sensor ignorado, estado = {estado}");
                }

                if (escolhido == null)
                {
                    motivo = vistos == 0
                        ? "Nenhum Kinect encontrado. Confira a fonte de energia (o sensor não liga só pelo USB) e o cabo."
                        : "O Kinect foi encontrado mas não está pronto. O estado acima diz o que falta — "
                          + "NotPowered significa fonte desligada, DeviceNotSupported significa modelo incompatível com o SDK 1.8.";
                    return null;
                }

                object fluxo = escolhido.GetType().GetProperty("DepthStream").GetValue(escolhido, null);

                // Enable(DepthImageFormat.Resolution640x480Fps30)
                Type tipoFormato = kinect.GetType("Microsoft.Kinect.DepthImageFormat");
                object formato = Enum.Parse(tipoFormato, "Resolution640x480Fps30");
                fluxo.GetType().GetMethod("Enable", new[] { tipoFormato }).Invoke(fluxo, new[] { formato });

                if (modoPerto) DefinirModoPerto(kinect, fluxo);

                escolhido.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(escolhido, null);

                if (anguloDesejado != int.MinValue) DefinirAngulo(escolhido, anguloDesejado);

                MethodInfo abrir = fluxo.GetType().GetMethod("OpenNextFrame", new[] { typeof(int) });
                if (abrir == null)
                {
                    motivo = "O fluxo de profundidade não expõe OpenNextFrame — versão inesperada do SDK.";
                    return null;
                }

                string apelido = escolhido.GetType().GetProperty("UniqueKinectId")?.GetValue(escolhido, null) as string;
                motivo = null;
                return new Sensor(escolhido, fluxo, abrir, apelido ?? "Kinect v1");
            }
            catch (Exception erro)
            {
                motivo = "Falha ao abrir o sensor: " + PrimeiraLinha(erro);
                return null;
            }
        }

        private static Assembly CarregarBiblioteca(out string motivo)
        {
            motivo = null;
            try
            {
                // Se o SDK registrou a biblioteca no GAC, isto resolve sozinho.
                return Assembly.Load("Microsoft.Kinect, Version=1.8.0.0, Culture=neutral, "
                                     + "PublicKeyToken=31bf3856ad364e35");
            }
            catch
            {
                // Sem GAC: procura no lugar onde o instalador põe o arquivo.
            }

            foreach (string caminho in CaminhosDoSdk)
            {
                if (!File.Exists(caminho)) continue;
                try { return Assembly.LoadFrom(caminho); }
                catch (Exception erro)
                {
                    motivo = $"Achei {caminho} mas não consegui carregar: {PrimeiraLinha(erro)}. "
                             + "Quase sempre é arquitetura trocada — compile o projeto para x86.";
                    return null;
                }
            }

            motivo = "Kinect for Windows SDK 1.8 não encontrado. Instale o SDK e o Runtime 1.8 da Microsoft.";
            return null;
        }

        private static void DefinirModoPerto(Assembly kinect, object fluxo)
        {
            try
            {
                Type tipoAlcance = kinect.GetType("Microsoft.Kinect.DepthRange");
                PropertyInfo alcance = fluxo.GetType().GetProperty("Range");
                alcance.SetValue(fluxo, Enum.Parse(tipoAlcance, "Near"), null);
                Console.Error.WriteLine("[ponte] modo perto ligado (40cm a 3m)");
            }
            catch (Exception erro)
            {
                // Só o modelo 1517 aceita modo perto; nos Kinect de Xbox isso falha.
                Console.Error.WriteLine("[ponte] modo perto indisponível neste sensor: " + PrimeiraLinha(erro));
            }
        }

        private static void DefinirAngulo(object sensor, int graus)
        {
            try
            {
                int limitado = Math.Max(-27, Math.Min(27, graus));
                sensor.GetType().GetProperty("ElevationAngle").SetValue(sensor, limitado, null);
                Console.Error.WriteLine($"[ponte] motor inclinado para {limitado} graus");
            }
            catch (Exception erro)
            {
                Console.Error.WriteLine("[ponte] não consegui mover o motor: " + PrimeiraLinha(erro));
            }
        }

        /// <summary>
        /// Espera o próximo quadro e escreve a profundidade em milímetros no
        /// destino. Devolve false quando o quadro não veio a tempo.
        /// </summary>
        public bool LerQuadro(ushort[] destino, int esperaMs = 500)
        {
            object quadro = _abrirProximoQuadro.Invoke(_fluxoDeProfundidade, new object[] { esperaMs });
            if (quadro == null) return false;

            try
            {
                quadro.GetType().GetMethod("CopyPixelDataTo", new[] { typeof(short[]) })
                      .Invoke(quadro, new object[] { _cru });

                // O SDK 1.8 guarda a profundidade nos 13 bits altos; os 3 bits
                // baixos são o índice do jogador, que não usamos.
                for (int i = 0; i < _cru.Length; i++)
                {
                    destino[i] = (ushort)((_cru[i] >> 3) & 0x1FFF);
                }
                return true;
            }
            finally
            {
                (quadro as IDisposable)?.Dispose();
            }
        }

        private static string PrimeiraLinha(Exception erro)
        {
            Exception raiz = erro is TargetInvocationException && erro.InnerException != null
                ? erro.InnerException
                : erro;
            string texto = raiz.Message ?? "erro desconhecido";
            int quebra = texto.IndexOf('\n');
            return quebra > 0 ? texto.Substring(0, quebra).Trim() : texto;
        }

        public void Dispose()
        {
            try { _sensor.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(_sensor, null); }
            catch { /* encerrando mesmo assim */ }
        }
    }
}
