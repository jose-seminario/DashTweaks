# DashTweaks v2.0 - Documentación Técnica de Arquitectura

**DashTweaks** es una suite de optimización para Windows 10 y Windows 11 diseñada específicamente para e-sports y gaming competitivo, enfocándose en la seguridad, la reversibilidad matemática y la transparencia absoluta.

---

## 🛡️ Filosofía y Directrices de Seguridad

1. **Cero Cajas Negras:** Todo tweak es 100% auditable y reversible. No se ejecutan comandos destructivos en segundo plano.
2. **Backup First:** Toda sesión con cambios al sistema genera automáticamente un punto de restauración (`Checkpoint-Computer`) y realiza un volcado de las ramas clave del registro y planes de energía en `%LOCALAPPDATA%\DashTweaks\Backups\`.
3. **Seguridad Incondicional:** No se desactivan mitigaciones críticas de seguridad del núcleo (HVCI, VBS, Windows Defender) ni se modifica el almacén BCD (`bcdedit`).
4. **Rescate en Escritorio:** Modificaciones agresivas a nivel de interrupciones de hardware (Modo MSI y Prioridad HID) generan automáticamente scripts de rescate `RESCATE_MSI.bat` y `RESCATE_HID.bat` en el Escritorio del usuario con las instrucciones de restauración para Modo Seguro.
5. **Privilegios Elevados (UAC):** Manifiesto configurado con `requireAdministrator` para evitar fallos silenciosos por permisos.

---

## 📂 Arquitectura de la Solución (.NET 8.0)

```
DashTweaks/
├── DashTweaks.sln
├── LICENSE                         # Licencia MIT (Jose Seminario 2026)
├── README.md                       # Documentación principal de usuario
├── docs/
│   └── README.md                   # Documentación técnica de arquitectura
├── scripts/
│   └── Create-RestorePoint.ps1     # Script auxiliar de puntos de restauración
└── src/
    ├── DashTweaks.Core/            # Lógica de seguridad, métricas y utilidades del sistema
    │   ├── Managers/
    │   │   ├── BackupManager.cs    # Creación de checkpoints y volcados de registro/energía
    │   │   ├── RollbackManager.cs  # Importación de .reg y restauración de planes
    │   │   ├── SettingsManager.cs  # Configuración persistente en JSON (%LOCALAPPDATA%)
    │   │   ├── GameDetector.cs     # Detección asíncrona de juegos competitivos
    │   │   ├── SystemMonitor.cs    # Métricas de CPU y RAM en vivo
    │   │   ├── BloatwareManager.cs # Detección y desinstalación/restauración de apps UWP
    │   │   ├── StartupManager.cs   # Gestión de inicio de Windows en Run y carpetas
    │   │   ├── UpdateManager.cs    # Consultas a GitHub Releases API
    │   │   └── NetworkDiagnosticManager.cs # Diagnóstico de Packet Loss, Jitter y Bufferbloat
    │   └── Models/
    │       └── BackupInfo.cs       # Modelos de metadatos de respaldo
    ├── DashTweaks.Tweaks/          # Catálogo desacoplado de optimizaciones
    │   ├── ITweak.cs               # Contrato base con enum RiskLevel (Safe, Moderate, Aggressive)
    │   ├── ServiceManagerTweak.cs  # Control seguro de DiagTrack, SysMain y WSearch
    │   └── Implementations/        # 19 optimizaciones individuales
    │       ├── PowerPlanTweak.cs
    │       ├── GameDvrTweak.cs
    │       ├── MmcSsTweak.cs
    │       ├── NagleTweak.cs
    │       ├── ProcessSuspensionTweak.cs
    │       ├── MemoryPurgeTweak.cs
    │       ├── NetworkAdvancedTweak.cs
    │       ├── HidTweak.cs
    │       ├── HagsTweak.cs
    │       ├── SvcHostSplitTweak.cs
    │       ├── MouseAccelerationTweak.cs
    │       ├── FseOptimizationTweak.cs
    │       ├── TimerResolutionTweak.cs
    │       ├── EcoQosTweak.cs
    │       ├── NetworkLatencyTweak.cs
    │       ├── MsiModeTweak.cs
    │       ├── CoreParkingTweak.cs
    │       ├── PrioritySeparationTweak.cs
    │       └── HidPriorityTweak.cs
    └── DashTweaks.UI/              # Interfaz gráfica en WPF con tema oscuro Windows 11
        ├── app.manifest            # Forzado UAC (requireAdministrator)
        ├── App.xaml / App.xaml.cs  # Notificaciones en System Tray y cierre seguro
        ├── MainWindow.xaml         # 6 pestañas temáticas, dashboard de métricas y consola
        ├── MainWindow.xaml.cs      # Coordinación asíncrona y ciclo de vida
        └── ViewModels/             # ViewModels reactivos para UI
```

---

## ⚙️ Requisitos

- Windows 10 (versión 20H2 o superior) o Windows 11.
- .NET 8.0 Desktop Runtime.
- Permisos de Administrador.
