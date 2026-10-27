# 🚀 DashTweaks v2.3 (Pro Peripheral & Core Edition)

**DashTweaks** es un optimizador de Windows 10/11 de código abierto, diseñado para jugadores competitivos que buscan maximizar el rendimiento, minimizar la latencia DPC y eliminar el stutter **sin sacrificar la seguridad del sistema operativo**.

A diferencia de optimizadores invasivos o suites de pago de código cerrado (cajas negras), DashTweaks se basa en dos pilares inquebrantables: **Backup First & Reversibilidad Total**.

---

## ✨ Características Principales (v2.3)

- 🛡️ **Punto de Restauración Automático (Checkpoint):** Toda sesión de modificación genera un punto de restauración de Windows y vuelca ramas clave del registro y planes de energía antes de tocar el sistema.
- ⚡ **19 Optimizaciones de Sistema Reversibles:**
  1. *Plan de Energía de Alto Rendimiento* (GUID de máximo desempeño sin alterar planes de fábrica).
  2. *Desactivación de GameDVR y GameBar* (libera recursos de captura).
  3. *Prioridad Multimedia MMCSS para Gaming* (GamingTasks / SFIO con prioridad High).
  4. *Algoritmo de Nagle (TcpAckFrequency & TCPNoDelay)* (eliminación de buffering de red).
  5. *Suspensión de Procesos en Segundo Plano (Game Mode)* (NtSuspendProcess con lista blanca de exclusiones).
  6. *Purga Inteligente de Memoria Standby* (activada únicamente bajo presión de RAM >= 80%).
  7. *Optimización de Red Avanzada* (ThrottlingIndex = 0xFFFFFFFF, SystemResponsiveness = 0, LLMNR disabled).
  8. *Búferes de Entrada HID* (reducción de input lag en ratón y teclado).
  9. *Programación de GPU Acelerada por Hardware (HAGS)* (HwSchMode = 2).
  10. *SvcHost Split Threshold* (aislamiento de procesos svchost en equipos de 4GB+ RAM).
  11. *Aceleración de Ratón 1:1* (MouseSpeed=0 para puntería pura sin saltos).
  12. *Optimización FSE (Full Screen Exclusive)* (GameDVR_FSEBehaviorMode=2).
  13. *Resolución del Temporizador del Sistema a 0.5ms* (NtSetTimerResolution con liberación segura al cerrar).
  14. *Eficiencia EcoQoS en Procesos de Fondo* (ahorro de ciclos de CPU sin tocar procesos críticos).
  15. *Optimizador de Latencia de Red (DPC / NIC Tuning)* (Interrupt Moderation, EEE y Green Ethernet desactivados con bloqueo estricto en Wi-Fi).
  16. *Modo MSI en GPU y Red (Message Signaled Interrupts)* (interrupciones basadas en mensajes PCI con script de rescate en Escritorio).
  17. *Desactivación de Core Parking (100% Núcleos Activos)* (CPMINCORES = 100 para evitar latencias de despertar de cores).
  18. *Prioridad Cuántica de Procesos (Win32PrioritySeparation 26)* (0x1A: intervalos cortos y máxima prioridad al juego en foreground).
  19. *Prioridad IRQ para Ratón y Teclado (Affinity Policy High)* (DevicePriority = 3 y DevicePolicy = 0 seguro para máxima fluidez).
- 🚨 **Scripts de Rescate de Emergencia en el Escritorio:**
  - `RESCATE_MSI.bat`: Restablece el modo normal de interrupciones si el hardware no soporta MSI.
  - `RESCATE_HID.bat`: Restaura la prioridad IRQ y elimina Affinity Policy por si los periféricos dejaran de responder.
- 🩺 **Centro de Diagnóstico de Red (Packet Loss, Jitter y Bufferbloat):** Medición en vivo de estabilidad competitiva con fallback TCP ante bloqueos ICMP, test de Bufferbloat bajo descarga activa (con liberación inmediata de ancho de banda) y panel de recomendaciones inteligentes.
- 🧹 **Desinstalador Seguro de Bloatware UWP:** Escaneo y eliminación reversible de aplicaciones preinstaladas no esenciales de Windows con enlace directo de reinstalación vía Microsoft Store.
- ⚡ **Gestor de Inicio Rápido (Startup Manager):** Aislamiento seguro (`Software\DashTweaks\Disabled`) de aplicaciones en arranque de Windows sin borrar datos y con advertencia en controladores críticos.
- 🎮 **Auto-Game Mode en Bandeja:** Detección en segundo plano de juegos competitivos (CS2, Valorant, Fortnite, Apex, LoL, Dota 2, GTA V) con aplicación automática al jugar y reversión al salir.
- 📊 **Dashboard en Tiempo Real:** Monitor dinámico de consumo de CPU, RAM y estado de latencia de red (NIC Tuning).
- 🔄 **Auto-Actualizador Integrado:** Comprobación directa con la API de GitHub Releases para recibir nuevas versiones con un clic.
- 🚫 **Cero Telemetría:** Sin recolección de datos, sin anuncios, 100% transparente y local.

---

## 🛡️ Filosofía de Seguridad

DashTweaks **NUNCA** realizará las siguientes acciones:
- Desactivar Microsoft Defender, VBS o HVCI (Integridad de Memoria).
- Modificar el BCD (Boot Configuration Data) o invocar `bcdedit`.
- Desactivar mitigaciones del kernel ante vulnerabilidades Spectre/Meltdown.
- Ejecutar limpiadores de memoria en bucle que destruyen la caché del procesador.

---

## 🚀 Requisitos y Ejecución

1. **Sistema Operativo:** Windows 10 (versión 20H2 o superior) o Windows 11.
2. **Entorno de Ejecución:** .NET 8.0 Desktop Runtime.
3. **Privilegios:** Ejecutar como Administrador (UAC forzado automáticamente en el manifiesto).

---

## 📄 Licencia

Este proyecto está bajo la [Licencia MIT](file:///D:/DashTweaks/LICENSE). Desarrollado por Jose Seminario (2026).
