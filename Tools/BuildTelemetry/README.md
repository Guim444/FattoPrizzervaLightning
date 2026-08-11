# Telemetria de builds

Este sistema adapta la arquitectura del proyecto de referencia: cada ejecución es una sesión independiente, durante el juego se guardan datos crudos y el análisis se realiza después con Python.

## Uso habitual

1. Integra en esta rama los cambios de `main` que quieras comprobar y resuelve los conflictos normalmente.
2. Cierra Unity si tiene abierto este proyecto.
3. Ejecuta `build_and_run_telemetry.bat`.
4. Recorre en la build el contenido que quieras medir y cierra el juego de forma normal.
5. Ejecuta `generate_telemetry_report.bat`.

El primer BAT crea una build Windows Release instrumentada, la abre a 1920x1080 y registra una nueva sesión. El segundo calcula las métricas y genera un informe HTML, Markdown, JSON y CSV. Ni los datos crudos, ni las builds, ni los informes se suben a Git.

La primera build de Player puede tardar varios minutos si Unity todavía no tiene compiladas las variantes de URP. La consola muestra el shader y el progreso `x/y`; abrir previamente el proyecto no prepara necesariamente la caché de shaders del Player. Las ejecuciones posteriores reutilizan esa caché.

Para medir otro nivel de calidad, pásalo como primer parámetro:

```bat
build_and_run_telemetry.bat Bajo
```

Ahora mismo el proyecto solo define la calidad `PC`; los nombres adicionales funcionarán cuando se creen en `Quality Settings`.

Variables opcionales:

- `TELEMETRY_WIDTH` y `TELEMETRY_HEIGHT`: resolución de ejecución (por defecto 1920x1080).
- `TELEMETRY_UNITY_EXE`: ruta a otro ejecutable de Unity.
- `TELEMETRY_DEVELOPMENT=1`: crea una Development Build. Para comparativas finales conviene usar siempre Release.
- `TELEMETRY_WARMUP_SECONDS`: segundos iniciales excluidos de las cifras principales (por defecto 3). El informe también conserva las cifras de la sesión completa.

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
