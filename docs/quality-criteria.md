# Criterios de auditoría ("agente juez")

Checklist verificable para evaluar cualquier entrega de este proyecto. La regla general:
un criterio "cumplido" tiene que ser demostrable con un comando o un archivo, no con una
afirmación. Nada de "excelencia" sin evidencia.

## 1. Build y tests (bloqueante, no negociable)

- [ ] `dotnet build` limpio (0 errores, 0 warnings nuevos) en `AudioEvolution.Core`,
      `AudioEvolution.Data`, `AudioEvolution.Core.Tests` — verificable en Linux, sin excusas.
- [ ] `dotnet test` en verde. Ningún test se comenta, se skippea o se debilita para que
      pase — si un test falla, se corrige el código o se corrige el test porque estaba mal
      planteado (con justificación explícita de por qué).
- [x] `AudioEvolution.App` compila en Windows con el workload MAUI — verificado en Windows
      real (`dotnet build AudioEvolution.sln`, 0 errores, 0 warnings nuevos). Además se
      ejecutó la app real: abre ventana, el flujo crear/listar proyecto corre, y persiste
      de verdad en SQLite (archivo `audioevolution.db` con cabecera válida confirmado en
      disco). No se hizo inspección visual con captura de pantalla (permiso de pantalla
      denegado en la sesión) — verificación funcional/de archivos, no visual. Ver
      README.md sección "Verificación en Windows real" y bugs #6/#7.
- [x] `AudioEvolution.Audio.Windows`/`AudioEvolution.Audio.Windows.Tests` compilan y
      corren en Windows real (`dotnet build`, 0 errores, 0 warnings; `dotnet test`, 7/7).
      Incluye un test de hardware real (reproduce un tono y lo verifica con WASAPI loopback
      capture, no un mock) — ver README.md sección "Backend de audio WASAPI".

## 2. Cobertura de casos DSP/motor de audio

Para cualquier cambio al motor de mezcla, verificar explícitamente (con test, no de
palabra):

- [ ] Suma correcta de clips solapados en una misma pista.
- [ ] Mute/Solo con la semántica estándar (cualquier pista soloed silencia las no-soloed).
- [ ] Fades (in/out) con curva lineal como mínimo; si se agregan otras curvas
      (logarítmica, equal-power, etc.), cada una con su propio test de valores en los
      extremos (t=0, t=1) y en un punto intermedio conocido.
- [ ] Ningún `stackalloc` dentro de un loop sin acotar (riesgo de stack overflow) — el
      compilador ya avisa con CA2014, tratar ese warning como error de facto.
- [ ] Conversión de formato de sample (PCM16/24/32, float32) sin pérdida de rango: el
      valor máximo representable (+1.0 / -1.0) debe hacer round-trip exacto o dentro de la
      tolerancia del formato, verificado con test paramétrico por formato.

## 3. Persistencia

- [ ] Round-trip completo de un objeto de dominio con **valores no-cero** en todos los
      campos numéricos — un test que solo usa ceros no prueba nada (ver bug #4 en README).
- [ ] Cualquier tipo `struct` custom usado en persistencia JSON debe tener o bien
      `[JsonConstructor]` explícito, o setters públicos — nunca depender del constructor
      implícito sin parámetros para reconstituir estado válido.
- [ ] Cualquier propiedad de colección expuesta para persistencia debe usar `init` (o
      `set`), nunca un `get` desnudo — de lo contrario `System.Text.Json` la deserializa
      vacía sin avisar.
- [ ] Ninguna query LINQ-to-Entities usa un tipo/método que el proveedor (SQLite) no
      traduce a SQL — verificar contra el proveedor real, no asumir que EF Core lo resuelve.

## 4. Paridad funcional con AEM (checklist de producto, se actualiza por fase)

Marcar solo lo que tiene implementación real y testeada — no "diseñado" ni "planeado":

- [ ] Grabación multipista
- [ ] Edición no destructiva (trim, split, move, fade) — modelo listo, falta UI
- [x] Automatización de volumen/pan — `TrackRenderer` la evalúa sample-accurately
      (`TrackRenderer_VolumeAutomation_OverridesStaticVolumePerSample`). Falta UI para
      dibujar/editar las curvas.
- [ ] MIDI + piano roll
- [ ] Instrumentos virtuales / síntesis
- [ ] Cadena de efectos con DSP real (no solo el slot `EffectInstance`)
- [ ] Time-stretching / pitch-shifting
- [ ] Exportación mezclada (mixdown a WAV como mínimo)
- [ ] Codecs comprimidos (MP3/FLAC/OGG) de entrada y salida
- [ ] Backend de audio real (WASAPI, luego ASIO) con latencia medida — WASAPI implementado
      y verificado con hardware real (`AudioEvolution.Audio.Windows`), pero sin latencia
      medida todavía y sin ASIO; no se marca completo hasta cerrar ambos.

## 5. Honestidad del reporte

- [ ] Ningún commit/PR describe algo como "implementado" si no tiene test que lo cubra
      cuando es lógicamente testeable (DSP, persistencia, parsing). UI visual sin acceso a
      Windows real se reporta explícitamente como "no verificado visualmente", no como
      "listo".
- [ ] Toda limitación de entorno (SDK, red, plataforma) se documenta con su causa raíz, no
      se oculta detrás de un "no se pudo" genérico.
