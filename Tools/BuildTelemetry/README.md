# Telemetría de builds

Este sistema registra los datos de rendimiento mientras juegas una build ya creada. Primero se genera la build manualmente desde Unity; después se ejecuta con un parámetro especial para guardar los datos; finalmente se genera un informe.

El flujo documentado está preparado para Windows.

## Paso 1: preparar la build en Unity

Haz estos pasos una sola vez cada vez que prepares una build nueva:

1. Abre el proyecto en Unity.
2. Ve a `File > Build Profiles` y comprueba que las escenas están activadas y en este orden:
   - `Assets/Level/MainScene.unity`
   - `Assets/Level/GameplayScene.unity`
   - `Assets/Level/LightingScene.unity`
   - `Assets/Level/DialogueScene.unity`
3. Comprueba que `MainScene` contiene el componente `BuildSceneLoader`. Carga automáticamente las otras tres escenas al iniciar el juego.
4. Ve a `Edit > Project Settings > Player > Other Settings > Scripting Define Symbols`.
5. En la plataforma para la que vas a crear la build, añade exactamente:

```text
BUILD_TELEMETRY
```

Si ya hay otros símbolos, sepáralos con `;`. Sin `BUILD_TELEMETRY`, el juego funcionará, pero no podrá registrar datos.

6. Pulsa `Build` desde Unity y guarda la build de Windows en una carpeta fácil de encontrar, por ejemplo `Builds/ManualTelemetry/FattoPrizzerva.exe`.

La build debe contener el recolector de telemetría, pero no empezará a registrar nada hasta que se ejecute con `-buildTelemetry`.

## Paso 2: ejecutar la build en Windows

### Abrir la consola

1. Abre el Explorador de archivos y entra en la carpeta raíz del proyecto.
2. Haz clic en la barra de direcciones, escribe `cmd` y pulsa `Enter`.
3. Se abrirá una ventana negra ya situada en la carpeta correcta.

### Lanzar el juego con telemetría

Copia este comando y cambia únicamente la ruta del `.exe` si tu build está en otra carpeta:

```bat
Tools\BuildTelemetry\build_and_run_telemetry.bat "D:\GuimGames\FattoPrizzervaLightning\Builds\ManualTelemetry\FattoPrizzerva.exe" PC
```

El texto `PC` es la calidad gráfica. Actualmente es la única calidad configurada en el proyecto.

El BAT añade automáticamente estos parámetros:

- `-buildTelemetry`: activa la grabación.
- `-telemetryOutput`: guarda las sesiones en `BuildTelemetryReports/raw`.
- `-telemetryQuality`: registra la calidad usada.
- `-screen-width 1920` y `-screen-height 1080`: fija la resolución.
- `-logFile`: guarda el log del jugador en `BuildTelemetryReports/player-logs`.

Si la ruta contiene espacios, conserva las comillas. Al terminar la partida, cierra el juego normalmente.

## Paso 3: generar el informe

Desde la carpeta raíz del proyecto, ejecuta:

```bat
Tools\BuildTelemetry\generate_telemetry_report.bat
```

Los informes se crearán en `BuildTelemetryReports/reports`. Cada ejecución genera una sesión independiente.

## Solución rápida de problemas

- **No aparece ninguna sesión:** comprueba que la build se creó con `BUILD_TELEMETRY` y que se ejecutó con `-buildTelemetry`.
- **El juego aparece vacío:** comprueba que las cuatro escenas están incluidas, que `MainScene` está en la posición 0 y que tiene `BuildSceneLoader`.
- **Windows dice que no encuentra el archivo:** revisa la ruta del `.exe` y mantén las comillas.
- **El informe no encuentra sesiones:** ejecuta el informe desde la carpeta raíz del proyecto y comprueba que existe `BuildTelemetryReports/raw`.

La calidad disponible actualmente es `PC`. El calentamiento excluido de las cifras principales se puede cambiar con `TELEMETRY_WARMUP_SECONDS` al usar los scripts de Windows; el valor predeterminado es 3 segundos.

## Qué se recoge durante el juego

- Tiempo crudo de cada frame y valores crudos disponibles de CPU principal, hilo de render y GPU.
- Memoria usada/reservada, memoria de vídeo, texturas, mallas y asignaciones de GC cuando Unity expone cada contador en esa build.
- Draw calls, batches, SetPass, triángulos y vértices.
- Contadores acumulados de recolecciones de basura.
- Escena, calidad, resolución, VSync y objetivo de FPS de cada muestra.
- Inicio/fin de sesión, cambios de escena y marcadores opcionales.
- Commit, rama, cambios sin commit, versión de Unity y hardware.

No se calculan medias, FPS, percentiles, picos ni regresiones dentro de la build.

## Archivos de una sesión

- `session.json`: contexto de la ejecución y disponibilidad de contadores.
- `frames.bin`: registros crudos por frame, en formato binario fijo `FBTL v1`.
- `events.jsonl`: eventos crudos de sesión, escenas y marcadores.
- `session_end.json`: estado de cierre y posibles muestras descartadas.
- `complete.flag`: indica que el juego se cerró limpiamente.

El constructor restaura después de cada intento los ajustes URP, Graphics Settings, Player Settings y archivos temporales de Performance Testing que Unity puede modificar durante el proceso. La restauración también se ejecuta si la build falla.

Los informes comparan automáticamente la última sesión con la anterior compatible (misma plataforma, CPU, GPU, calidad y resolución). Si cambian esas condiciones, el informe lo indica y evita presentar una comparación engañosa.

## Marcadores opcionales desde código

Si más adelante interesa separar fases concretas del combate, puede registrarse un evento crudo desde el hilo principal:

```csharp
FattoPrizzerva.BuildTelemetry.BuildTelemetryApi.Mark("combat_start", "boss_01");
```

El marcador no calcula nada; solo permite correlacionar esa fase durante el análisis posterior.
