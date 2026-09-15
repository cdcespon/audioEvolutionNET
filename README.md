# Audio Evolution .NET

Clon de Audio Evolution Mobile para Windows, en .NET MAUI + SQLite. Proyecto de alcance
multi-sesión — ver "Estado actual" antes de asumir cobertura de funcionalidades.

## Arquitectura

```
AudioEvolution.sln
├── src/AudioEvolution.Core   → motor de audio, modelo de dominio, códecs (net8.0, multiplataforma)
├── src/AudioEvolution.Data   → persistencia SQLite vía EF Core (net8.0, multiplataforma)
├── src/AudioEvolution.App    → UI MAUI, head Windows/WinUI3 (net8.0-windows10.0.19041.0)
└── tests/AudioEvolution.Core.Tests → xUnit, motor + persistencia
```

- **AudioEvolution.Core** no depende de MAUI, EF Core ni de ningún backend de audio real —
  es puro C#, testeable en cualquier plataforma. Contiene el modelo de dominio (`Project`,
  `Track`, `AudioClip`, `AutomationLane`), el motor de mezcla (`MixEngine`/`TrackRenderer`,
  suma multipista con solo/mute/pan/fades/automatización de volumen y pan aplicada
  sample-accurately), el códec WAV (16/24/32-bit PCM + float32) y las interfaces del
  backend de audio real (`IAudioDevice`, sin implementación aún).
- **AudioEvolution.Data** persiste `Project` en SQLite vía EF Core. Las pistas/clips se
  guardan como JSON dentro de la fila del proyecto (no normalizado en tablas relacionales
  todavía — no hace falta hasta que se necesite consultar across-project, p.ej. "todos los
  clips que referencian el archivo X").
- **AudioEvolution.App** es el head MAUI Windows. Por ahora: lista de proyectos + crear
  proyecto nuevo, conectado de verdad a `ProjectRepository`. El editor multipista todavía no
  existe (Task pendiente). **Compilado y verificado en Windows real** (ver sección
  siguiente): la app abre su ventana, el flujo de crear/listar proyectos corre, y persiste
  de verdad en SQLite bajo `FileSystem.AppDataDirectory` — confirmado observando el archivo
  `audioevolution.db` creado en disco con cabecera SQLite válida tras ejecutar la app real.
  No se pudo hacer una inspección visual con captura de pantalla (permiso de acceso a
  pantalla denegado en esta sesión) — la verificación es funcional/de archivos, no visual.

## Estado actual (honesto, no aspiracional)

Esto es una base de arquitectura real y probada, no un clon funcional de AEM. Lo que
funciona hoy, con tests pasando:

- Modelo de dominio completo para proyecto/pista/clip/fade/automatización.
- Motor de mezcla multipista puro (sin backend de audio real todavía): suma clips
  solapados, aplica gain/pan/fade/mute/solo — con tests que cubren estos casos.
- Códec WAV completo (lectura+escritura, 16/24/32-bit PCM y float32) con tests de
  round-trip por formato.
- Persistencia SQLite de proyectos completos (pistas, clips, markers) con test de
  round-trip que específicamente cubre valores no-cero (crítico: un bug real encontrado
  y corregido durante el desarrollo hacía que toda posición no-cero se reseteara a 0 al
  recargar — ver sección siguiente).

Lo que falta, sin eufemismos:

- **Backend de audio real (WASAPI/ASIO)**: solo están las interfaces (`IAudioDevice`). No
  hay sonido real todavía. Esto requiere un proyecto adicional `AudioEvolution.Audio.Windows`
  con NAudio, que solo puede compilarse y probarse en Windows.
- **UI del editor multipista**: timeline, waveforms, transporte. No existe.
- **MIDI**, piano roll, instrumentos virtuales: no existe.
- **Efectos** (EQ, compresor, reverb, etc.): solo el modelo de slot (`EffectInstance`), sin
  ningún DSP implementado.
- **Códecs comprimidos** (MP3, FLAC, OGG): solo WAV por ahora.
- **Formato .aep de AEM**: no se intenta — es un formato propietario no documentado
  públicamente; leerlo requeriría ingeniería inversa aparte.

## Bugs reales encontrados y corregidos durante este desarrollo

Documentado porque es información que importa para confiar en el código, no para
autoelogio:

1. **`TrackRenderer`**: un `monoBuf.Clear()` mal ubicado dentro del loop por-clip borraba
   la contribución de clips anteriores en cada iteración — la mezcla de clips solapados
   estaba rota. Corregido + cubierto por test.
2. **`WavFile.Write` (Pcm32)**: `int.MaxValue` convertido a `float` pierde precisión y
   redondea a `2^31`, lo que producía overflow silencioso al escribir samples en el
   máximo (+1.0 se escribía como -1.0). Corregido usando `double` en el cálculo intermedio.
3. **Persistencia JSON de listas**: `System.Text.Json` (variante reflection-based) no
   rellena propiedades de colección expuestas como `{ get; } = new()` — las deserializa
   vacías sin lanzar error. Afectaba a *todas* las listas del modelo (`Track.Clips`,
   `Project.Tracks`, `AutomationLane.Points`, etc.). Corregido cambiando a `{ get; init; }`.
4. **Persistencia JSON de `SampleTime`**: al ser un `struct` sin setters públicos,
   `System.Text.Json` usaba el constructor implícito sin parámetros y dejaba todo en
   `(Samples: 0, SampleRate: 0)` — cualquier posición o duración no-cero se perdía al
   recargar un proyecto. Corregido con `[JsonConstructor]` sobre el constructor real.
5. **`AudioClip.FadeInLength`/`FadeOutLength` como `SampleTime`**: al no setearse quedaban
   en el valor `default` del struct (sample rate 0), lo que además rompía `TotalSeconds`
   (división 0/0 → NaN, que `System.Text.Json` rechaza al serializar). Rediseñado a `long`
   simple, porque de todas formas son siempre relativos al sample rate del propio clip.
6. **Orden de construcción entre `App` y `MainPage` vía DI (WinUI3 nativo)**: `App` recibía
   `MainPage` por constructor (`App(MainPage mainPage)`). Para satisfacer ese parámetro,
   el contenedor de DI tiene que construir `MainPage` — corriendo su `InitializeComponent()`,
   que resuelve `{StaticResource ...}` de `MainPage.xaml` (`AppBackground`, `AppText`,
   `AppAccent`) — **antes** de que el constructor de `App` llegue a llamar a su propio
   `InitializeComponent()`, que es lo que fusiona `Colors.xaml`/`Styles.xaml` en
   `Application.Resources`. `MainPage` terminaba buscando recursos que todavía no existían
   en el árbol de recursos de la aplicación. Esa resolución fallida, en vez de propagarse
   como una `XamlParseException` .NET manejable, cruzaba el límite nativo WinRT como
   excepción "stowed" (código de salida `0xc000027b` / `STATUS_STOWED_EXCEPTION`) y mataba
   el proceso entero — con `Microsoft.UI.Xaml.Application.Start()` como único frame visible
   en el stack, sin ninguna excepción .NET capturable ni siquiera en el hilo que originó el
   fallo (confirmado inspeccionando volcados de memoria reales con `dotnet-dump`). Este era
   el bug real detrás de "`AudioEvolution.App` no compila/no corre en Windows": no tenía
   nada que ver con MSIX, con la versión de Windows App SDK, ni con la versión de .NET/MAUI
   — se confirmó reproduciendo el mismo crash de forma aislada en un `dotnet new maui`
   oficial sin modificar (agregando la misma inyección de `MainPage` en `App`) y
   descartando como causa, una por una, cada otra variable probada (empaquetado MSIX vs.
   desempaquetado, self-contained vs. framework-dependent, manifest de Windows, versión de
   Windows App SDK 1.5/1.6/1.7, llamada síncrona a `EnsureCreated()` en el arranque). Ninguna
   de esas, por sí sola, causaba ni arreglaba el crash una vez corregido el orden de
   construcción. Corregido resolviendo `MainPage` de forma perezosa dentro de
   `App.CreateWindow()` (`Handler.MauiContext.Services.GetRequiredService<MainPage>()`),
   después de que `App.InitializeComponent()` ya corrió.
7. **`Resources/Styles/Colors.xaml`/`Styles.xaml` ausentes**: el proyecto no traía los
   diccionarios de recursos estándar de MAUI (la plantilla oficial siempre los incluye);
   `App.xaml` sólo definía 4 colores propios sueltos, sin los estilos implícitos que las
   plantillas de control nativas de WinUI3 (`Button`, `Label`, `Page`, etc.) necesitan para
   inicializarse. Sin ellos, el motor XAML nativo de WinUI3 fallaba catastróficamente
   durante `Application.Start()` — con el mismo patrón de crash sin excepción .NET que el
   bug anterior, y era una causa real e independiente (confirmada agregando/quitando
   `ResourceDictionary.MergedDictionaries` en un `dotnet new maui` limpio, sin ninguna otra
   variable de por medio). Corregido agregando `Resources/Styles/Colors.xaml` y
   `Resources/Styles/Styles.xaml` estándar (con las fuentes OpenSans que `Styles.xaml`
   referencia por `FontFamily`) y fusionándolos en `App.xaml`.

También se encontraron y corrigieron, durante la misma investigación, dos problemas
menores que **no** eran la causa del crash (se descartaron explícitamente probándolos de
forma aislada) pero sí bugs reales por derecho propio:

- `Platforms/Windows/Package.appxmanifest` tenía rutas hardcodeadas a PNGs que no existen
  en el proyecto (`Assets\StoreLogo.png`, `Assets\Square150x150Logo.png`, etc.) en vez del
  token `$placeholder$.png` que usa la plantilla oficial para que el Resizetizer los
  reemplace a todos por los assets generados desde los SVG de `MauiIcon`/`MauiSplashScreen`.
  El Resizetizer sólo reconocía y reemplazaba el token, así que dejaba esas 4 referencias
  hardcodeadas apuntando a archivos inexistentes. No causaba el crash (WindowsPackageType
  es `None`, el manifest MSIX no se empaqueta), pero sí hubiera roto un build empaquetado a
  futuro. Corregido usando `$placeholder$.png` en todas las referencias de imagen.
- `ProjectRepository` no llamaba a `Database.EnsureCreated()` — esa llamada vivía en
  `MauiProgram.CreateMauiApp()`, síncrona, durante el arranque. No era la causa del crash
  (probado explícitamente: con el bug #6 corregido, la llamada síncrona en el arranque
  funciona sin problema), pero bloquear el hilo de arranque con I/O de archivo real sigue
  siendo mala práctica. Movida a `ProjectRepository`, perezosa (se ejecuta una sola vez, en
  el primer uso real del repositorio).

## Verificación en Windows real

`AudioEvolution.App` fue escrito a mano en un sesión previa, en un contenedor Linux sin
workload MAUI disponible (`apt install dotnet-sdk-8.0` no trae los manifiestos del
workload MAUI — el instalador oficial de `dot.net` que sí los trae estaba bloqueado por
política de red), así que nunca se había compilado ni corrido. En una sesión posterior, en
Windows real con el SDK/workload `maui-windows` instalado, se compiló, se encontraron y
corrigieron los bugs #6 y #7 de arriba (la causa real de que no arrancara), y se verificó
el flujo completo: la app abre, lista proyectos (vacío al inicio), crea un proyecto nuevo
desde la UI, y persiste de verdad — confirmado con el archivo `audioevolution.db` real en
`%LOCALAPPDATA%\Audio Evolution\com.audioevolution.app\Data\`, con cabecera SQLite válida.

Nota sobre la versión de MAUI: el proyecto apuntaba a `net8.0-windows` +
`Microsoft.Maui.Controls 8.0.100`. Se subió a `net10.0-windows` + `Microsoft.Maui.Controls
10.0.1` (alineado con el workload `maui-windows` instalado, banda 10.0.100) porque es la
combinación que quedó verificada de punta a punta en esta máquina. Con los bugs #6/#7 ya
corregidos, `net8.0-windows` + `Microsoft.Maui.Controls 8.0.100` también compila y el
proceso queda vivo — pero la ventana no llega a mostrar contenido real (la base SQLite
nunca se crea), señal de que hay algo más, no diagnosticado, específico de esa combinación
en esta máquina. Se prioriza la combinación verificada en vez de perseguir esa causa
adicional. `AudioEvolution.Core`/`Data` se quedan en `net8.0` (multiplataforma, verificados
en Linux); sólo el head Windows-only subió de banda.

## Cómo continuar

1. Crear `AudioEvolution.Audio.Windows` implementando `IAudioDevice` con NAudio (WASAPI
   primero, ASIO después) — es el bloqueador real para tener sonido.
2. Construir el editor multipista en `AudioEvolution.App` sobre `MixEngine`.

## Criterios del "agente juez" (auditoría de calidad)

Ver `docs/quality-criteria.md`.
