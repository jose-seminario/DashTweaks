# 🚀 DashTweaks

**DashTweaks** es un optimizador de Windows 10/11 de código abierto, diseñado para jugadores competitivos que buscan mejorar la latencia, los FPS y la consistencia del frametime **sin sacrificar la seguridad del sistema**.

A diferencia de otros optimizadores de pago (cajas negras), DashTweaks se basa en un principio fundamental: **Backup First & Reversibilidad Total**.

## ✨ Características Principales

- 🛡️ **Punto de Restauración Automático:** La app crea un Checkpoint de Windows antes de tocar tu sistema.
- 🔄 **Reversión por Tweak:** Cada optimización tiene su propio botón de "Revertir".
- ⚡ **9 Tweaks de Sistema:** Energía, GameDVR, MMCSS, Nagle, Red Avanzada, HID (Ratón/Teclado), HAGS.
- 🧠 **Gestión Avanzada de Memoria:** Purga de Standby List (solo bajo presión de RAM).
- ⏸️ **Suspensión de Procesos (Game Mode):** Congela Explorer, Steam y Discord temporalmente para liberar CPU.
- 🛠️ **Gestor de Servicios Seguro:** Desactiva Telemetría (DiagTrack), SysMain y WSearch guardando su estado original.
- 🚫 **Cero Telemetría:** No recopilamos datos. Todo es 100% local.

## 🚀 Instalación y Uso

1. Ve a la pestaña **Releases** en este repositorio.
2. Descarga el archivo `DashTweaks.UI.exe`.
3. Ejecútalo como Administrador (requerido para tocar el registro y los servicios).
4. Haz clic en **Crear Backup** antes de aplicar cualquier tweak. ¡La app lo hará automáticamente la primera vez de todos modos!

## 🛡️ Filosofía de Seguridad

DashTweaks **NUNCA** hará lo siguiente:
- Desactivar Microsoft Defender, VBS o HVCI.
- Modificar el BCD (Boot Configuration Data) o usar `bcdedit`.
- Desactivar mitigaciones de CPU (Spectre/Meltdown).
- Usar limpiadores de RAM agresivos de fondo (causantes de stutter).

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Si quieres añadir un nuevo tweak, asegúrate de que implemente la interfaz `ITweak` y tenga su método `Revert()` funcional.

## 📄 Licencia

Este proyecto está bajo la Licencia MIT.
