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
  suma multipista con solo/mute/pan/fades), el códec WAV (16/24/32-bit PCM + float32) y las
  interfaces del backend de audio real (`IAudioDevice`, sin implementación aún).
- **AudioEvolution.Data** persiste `Project` en SQLite vía EF Core. Las pistas/clips se
  guardan como JSON dentro de la fila del proyecto (no normalizado en tablas relacionales
  todavía — no hace falta hasta que se necesite consultar across-project, p.ej. "todos los
  clips que referencian el archivo X").
- **AudioEvolution.App** es el head MAUI Windows. Por ahora: lista de proyectos + crear
  proyecto nuevo, conectado de verdad a `ProjectRepository`. El editor multipista todavía no
  existe (Task pendiente).

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

## Limitación real del entorno de desarrollo (contenedor Linux)

El SDK de .NET instalado aquí viene del repositorio de Ubuntu (`apt install dotnet-sdk-8.0`)
porque el dominio `dot.net` (instalador oficial de Microsoft, que sí trae los manifiestos
del workload MAUI) está bloqueado por política de red de la organización. El paquete de
Ubuntu **no incluye los manifiestos del workload MAUI** — no es cuestión de espacio en
disco ni de reintentar, es una limitación estructural del paquete distro.

Consecuencia concreta: `AudioEvolution.App` (el head MAUI/WinUI3) fue escrito a mano
siguiendo la estructura estándar de un proyecto MAUI, pero **no se pudo restaurar ni
compilar en este contenedor**, y por lo tanto no está verificado de la misma forma que
Core/Data/Tests (esos sí compilan y sus 16 tests pasan, verificado en este entorno). Al
abrirlo en Visual Studio en Windows con el workload MAUI instalado, es posible que haga
falta que Visual Studio regenere algún asset (íconos, manifest) — trátenlo como un punto
de partida sólido, no como código garantizado de compilar al primer intento.

## Cómo continuar

1. En Windows, con Visual Studio 2022 (workload ".NET Multi-platform App UI development")
   instalado: abrir `AudioEvolution.sln`, dejar que VS reconcilie `AudioEvolution.App`.
2. Verificar que `AudioEvolution.Core`/`Data`/`Tests` compilan igual que en este contenedor
   (`dotnet build`, `dotnet test` desde la raíz).
3. Crear `AudioEvolution.Audio.Windows` implementando `IAudioDevice` con NAudio (WASAPI
   primero, ASIO después) — es el bloqueador real para tener sonido.
4. Construir el editor multipista en `AudioEvolution.App` sobre `MixEngine`.

## Criterios del "agente juez" (auditoría de calidad)

Ver `docs/quality-criteria.md`.
