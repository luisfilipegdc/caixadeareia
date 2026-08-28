// Caixa de Areia Interativa — sistema de projeção topográfica interativa
// Copyright (C) 2026 Projeto Caixa de Areia
//
// Este programa é software livre: você pode redistribuí-lo e/ou modificá-lo
// sob os termos da Licença Pública Geral GNU, conforme publicada pela Free
// Software Foundation, na versão 2 da Licença ou (a seu critério) qualquer
// versão posterior.
//
// Este programa é distribuído na esperança de que seja útil, mas SEM QUALQUER
// GARANTIA; sem sequer a garantia implícita de COMERCIALIZAÇÃO ou ADEQUAÇÃO A
// UMA FINALIDADE ESPECÍFICA. Consulte a Licença Pública Geral GNU para mais
// detalhes. Uma cópia acompanha este programa no arquivo LICENSE.

using System.Windows;
using CaixaInterativa.Diagnostico;

namespace CaixaInterativa;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Registro.Iniciar(AppInfo.Versao);

        // Uma excecao nao tratada numa aula significa tela preta na parede. Preferimos
        // avisar e continuar rodando a derrubar o processo.
        DispatcherUnhandledException += (_, args) =>
        {
            // Registrar antes de mostrar: se a caixa de diálogo travar, o registro já foi.
            Registro.Erro("Exceção não tratada", args.Exception);

            MessageBox.Show(
                $"Erro nao tratado:\n\n{args.Exception.Message}",
                "Caixa Interativa",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        Exit += (_, _) => Registro.Info("Sessão encerrada.");
    }
}
