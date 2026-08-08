# Windows Duplicate Finder

Buscador de archivos duplicados para Windows 10/11. Escanea directorios usando un motor de detección de 4 etapas (tamaño exacto → hash rápido → hash completo → comparación binaria) con protección integrada del sistema.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Compilar y ejecutar

```bash
dotnet build
dotnet run --project src/DuplicateFinder.App
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
    ├── Resources/                # Styles.xaml (tema Fluent oscuro)
    └── app.manifest              # DPI alto, compatibilidad Win10/11
```
