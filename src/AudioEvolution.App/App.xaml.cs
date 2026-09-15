using Microsoft.Extensions.DependencyInjection;

namespace AudioEvolution.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // MainPage se resuelve acá (perezoso), no por constructor injection en App: si
        // App(MainPage mainPage) recibe MainPage por parametro, el contenedor de DI tiene
        // que construir MainPage -- y correr su InitializeComponent(), que resuelve
        // {StaticResource ...}/{AppThemeBinding ...} -- ANTES de que el constructor de
        // App llegue a llamar a su propio InitializeComponent() (que es lo que fusiona
        // Colors.xaml/Styles.xaml en Application.Resources). MainPage terminaba
        // buscando recursos que todavia no existian, y esa resolucion fallida cruzaba el
        // limite nativo WinRT como excepcion "stowed" en vez de una excepcion .NET
        // manejable — crasheaba el proceso entero (0xc000027b dentro de
        // Microsoft.UI.Xaml.dll, sin excepcion .NET visible) antes de mostrar ninguna
        // ventana. Ver README.md.
        var mainPage = Handler!.MauiContext!.Services.GetRequiredService<MainPage>();
        return new Window(new NavigationPage(mainPage));
    }
}
