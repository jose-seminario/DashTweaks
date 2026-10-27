# DashTweaks

**DashTweaks** es una herramienta open-source de optimización para Windows 10 y Windows 11 diseñada específicamente para gaming, enfocándose en la seguridad, la reversibilidad y la transparencia total. Nace como una alternativa segura frente a suites invasivas como Italian Tweaks.

---

## 🛡️ Filosofía de Diseño

1. **Cero Cajas Negras:** Todo tweak implementado debe ser 100% reversible y documentado. No se realizan cambios silenciosos ni opacos.
2. **Backup First:** Antes de aplicar cualquier modificación al sistema (registro, planes de energía o servicios), el sistema genera un punto de restauración (`Checkpoint-Computer`) y realiza un volcado del estado original en disco.
3. **Seguridad Incondicional:** No se desactivan mitigaciones críticas de seguridad del núcleo (HVCI, VBS, Windows Defender) ni se compromete la integridad del arranque con modificaciones destructivas de `bcdedit`. Solo se aplican tweaks clasificados como **Seguros** o **Moderados**.
4. **Privilegios Elevados (UAC):** La aplicación requiere ejecución con permisos de Administrador (`requireAdministrator`) de manera transparente para garantizar operaciones directas sin fallos de permisos.

---

## 📂 Estructura de la Solución

```
DashTweaks/
├── DashTweaks.sln
├── docs/
│   └── README.md
├── scripts/
│   └── Create-RestorePoint.ps1
└── src/
    ├── DashTweaks.Core/        # Motor de Backup, Rollback y utilidades del sistema
    ├── DashTweaks.Tweaks/      # Definición de la interfaz ITweak y catálogo de tweaks
    └── DashTweaks.UI/          # Interfaz gráfica en WPF (.NET 8.0) con diseño Windows 11
```

---

## ⚙️ Requisitos

- Windows 10 (versión 20H2 o superior) o Windows 11.
- .NET 8.0 Desktop Runtime.
- Permisos de Administrador en el equipo.
