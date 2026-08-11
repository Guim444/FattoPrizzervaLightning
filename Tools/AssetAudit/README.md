# Auditor de assets

Herramienta externa y no destructiva para revisar el contenido del proyecto sin abrir Unity.

## Ejecución

Haz doble clic en `run_asset_audit.bat` o ejecútalo desde una terminal:

```bat
Tools\AssetAudit\run_asset_audit.bat
```

En la primera ejecución, el BAT busca Python 3.8 o superior y crea automáticamente:

```text
Tools/AssetAudit/.venv/
```

Las ejecuciones siguientes reutilizan ese entorno. El auditor no tiene dependencias externas y no descarga paquetes. La carpeta `.venv` está excluida de Git.

Cada ejecución crea una carpeta distinta:

```text
AssetAuditReports/asset_audit_YYYY-MM-DD_HH-MM-SS/
```

Las auditorías anteriores no se eliminan ni se sobrescriben.
La carpeta completa `AssetAuditReports` está excluida de Git.

## Informes

- Resumen legible en Markdown.
- Resumen estructurado en JSON.
- Inventario completo en CSV.
- Archivos más pesados.
- Duplicados exactos.
- Posibles assets sin uso.
- Texturas grandes y datos básicos del importador.
- Archivos grandes fuera de Git LFS.
- Archivos `.meta` ausentes o huérfanos.

## Opciones útiles

```bat
run_asset_audit.bat --skip-duplicates
run_asset_audit.bat --large-mb 20
run_asset_audit.bat --top 200
```

La búsqueda de duplicados utiliza SHA-256 y puede tardar cuando existen archivos grandes con el mismo tamaño.

## Advertencia sobre assets sin uso

La lista muestra **candidatos**, no archivos seguros para borrar. El script analiza GUID y referencias YAML, pero no puede confirmar cargas creadas dinámicamente mediante strings, reflexión, AssetBundles o código personalizado. `Resources`, `StreamingAssets`, `Plugins`, `Editor` y la configuración de Addressables se protegen para reducir falsos positivos.

El auditor nunca elimina ni mueve archivos.
