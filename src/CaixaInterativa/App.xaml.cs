using System.Windows;

namespace CaixaInterativa;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Uma excecao nao tratada numa aula significa tela preta na parede. Preferimos
        // avisar e continuar rodando a derrubar o processo.
        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(
                $"Erro nao tratado:\n\n{args.Exception.Message}",
                "Caixa Interativa",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };
    }
}
