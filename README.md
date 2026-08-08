# DupliKiller — Buscador de archivos duplicados

![CI](https://github.com/shoropio/duplikiller/actions/workflows/ci.yml/badge.svg)
![License](https://img.shields.io/github/license/shoropio/duplikiller)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)

Buscador de archivos duplicados para Windows 10/11. Escanea directorios usando un motor de detección de 4 etapas (tamaño exacto → hash rápido → hash completo → comparación binaria) con protección integrada del sistema.

## Características

- **Detección en 4 etapas**: agrupa por tamaño exacto, descarta falsos positivos con hash rápido muestreado, confirma con hash completo (SHA256/SHA1) y verificación binaria final.
- **Dos modos de escaneo**:
  - `Standard`: hash rápido muestreado + hash completo solo en colisiones (rápido).
  - `Deep`: hash completo del contenido para todos los candidatos (máxima precisión).
- **Protección del sistema**: evita marcar como duplicados archivos de `C:\Windows`, `WindowsApps`, AppData y extensiones críticas (`.sys`, `.dll`, `.efi`, ...).
- **Acciones seguras**: mover a la Papelera de Reciclaje (`SHFileOperation`), borrado permanente con confirmación, mover a carpeta y comprimir/backup en ZIP.
- **Filtros**: por tamaño, fecha de modificación, extensiones excluidas y rutas excluidas.
- **Exportación**: CSV, JSON, XML y TXT.
- **UI WPF con MVVM**: tema oscuro/claro, vista detallada o compacta, ordenamiento de resultados y selección rápida (Ctrl+A / Ctrl+Shift+A).
- **Registro (logging)**: logs por día en `%LOCALAPPDATA%\DupliKiller\Logs`.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Compilar y ejecutar

```bash
dotnet build
dotnet run --project src/DuplicateFinder.App
```

## Tests

```bash
dotnet test tests/DuplicateFinder.Core.Tests
```

## Herramienta CLI (scan-test)

Utilidad de consola para probar el motor de escaneo desde la línea de comandos:

```bash
dotnet run --project tools/scan-test -- "C:\ruta\a\escanear"
```

## Estructura del proyecto

```
src/
├── DuplicateFinder.Core/        # Lógica de negocio
│   ├── Models/                   # FileItem, DuplicateGroup
│   ├── Services/                 # HashService, FileScanner, FileActionService, ExportService
│   └── Utils/                    # SystemProtector (seguridad)
└── DuplicateFinder.App/          # UI WPF con MVVM
    ├── Views/                    # MainWindow.xaml
    ├── Resources/                # Styles.xaml (tema Fluent oscuro/claro)
    └── app.manifest              # DPI alto, compatibilidad Win10/11
tests/
└── DuplicateFinder.Core.Tests/   # Tests xUnit del motor de escaneo
tools/
└── scan-test/                    # Utilidad CLI de prueba del escáner
```

## Licencia

[MIT](LICENSE)
