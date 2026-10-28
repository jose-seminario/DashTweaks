## 🛠️ Catálogo de Optimizaciones (19 Tweaks)

DashTweaks no es una "caja negra". Aquí tienes el desglose exacto de lo que hace cada función, clasificada por su nivel de riesgo. **Recuerda: la app siempre creará un punto de restauración (Backup) antes de aplicar el primer cambio.**

### 🟢 Seguros (Safe)
*Actívalos sin miedo. Solo le dicen a Windows que deje de hacer tareas innecesarias en segundo plano.*

*   **Plan de Energía (Alto Rendimiento):** Evita que la CPU baje su frecuencia para ahorrar energía mientras juegas.
*   **Desactivar GameDVR y Game Bar:** Apaga la grabación en segundo plano de Windows. Libera recursos de GPU.
*   **Prioridad Multimedia (MMCSS):** Le dice al kernel de Windows que priorice los hilos de tu juego por encima de los procesos de fondo.
*   **FSE (Pantalla Completa Exclusiva):** Fuerza el modo de pantalla completa exclusiva para reducir el *input lag*.
*   **SvcHostSplit:** Separa los servicios de Windows en procesos independientes para que uno lento no congele todo el sistema.
*   **Desactivar Aceleración del Ratón:** Elimina la curva de aceleración de Windows para tener un *Raw Input* perfecto (1:1). *(Requiere reiniciar).*

### 🟡 Moderados (Moderate)
*Mejoran el rendimiento, pero dependen un poco de tu hardware o tipo de conexión.*

*   **Desactivar Nagle (Red):** Evita que Windows acumule paquetes pequeños de datos antes de enviarlos. Ideal para reducir el ping en juegos online. *(Recomendado solo si usas Cable Ethernet, no Wi-Fi).*
*   **Red Avanzada:** Desactiva el *Network Throttling* (límite de paquetes de Windows) y bloquea peticiones multicast (LLMNR) que causan micro-cortes.
*   **EcoQoS (Eficiencia):** Manda los procesos en segundo plano a los núcleos de eficiencia de la CPU, dejando los núcleos potentes libres para tu juego.
*   **HAGS (Planificación de GPU):** Activa la programación de GPU acelerada por hardware. Reduce el *input lag* en tarjetas gráficas modernas.

### 🔴 Agresivos (Aggressive)
*Estos tweaks tienen un impacto profundo en el hardware. La app pedirá confirmación extra y **creará un script de rescate en tu escritorio** por si algo falla. **NO recomendados para laptops**.*

*   **Timer Resolution:** Fuerza el reloj interno de Windows a 0.5ms (en lugar de 15.6ms). Reduce la latencia de entrada drásticamente. *(La app lo revierte automáticamente al cerrarse para no consumir batería).*
*   **Core Parking (CPU):** Desbloquea una opción oculta de energía para evitar que la CPU "apague" núcleos. Elimina los micro-tirones, pero sube la temperatura.
*   **MSI Mode (Interrupciones):** Cambia la forma en que tu GPU y tarjeta de red se comunican con la CPU (de hardware a memoria). Es el secreto mejor guardado de los optimizadores de élite.
*   **Prioridad HID (Ratón/Teclado):** Sube la prioridad de interrupción (IRQ) de tus periféricos para que tus clics y movimientos se procesen antes que cualquier otra cosa.
*   **Optimizador de Latencia de Red:** Desactiva la moderación de interrupciones y el ahorro de energía en tu tarjeta de red Ethernet para estabilizar los paquetes.

### 🧠 Gestión de Memoria y Procesos
*   **Purga de Memoria Inteligente:** Limpia la caché de RAM (Standby List) **SOLO si tu RAM está al 80% o más** de uso. Si tienes memoria de sobra, no la toca para evitar tirones.
*   **Suspensión de Procesos (Game Mode):** "Congela" temporalmente aplicaciones de fondo (como el Explorador de Windows o Chrome) para liberar CPU. No las cierra, solo las pausa, y respeta tu lista blanca (ej. Discord).

---

## 🧹 Herramientas del Sistema

*   **Backup y Reset Total:** Botones para crear un punto de restauración de Windows y revertir todo si algo sale mal.
*   **Game Mode Automático:** Detecta cuándo abres un juego en segundo plano y activa las optimizaciones automáticamente.
*   **Gestor de Servicios:** Te permite apagar servicios como la Telemetría (DiagTrack) o Superfetch (SysMain) guardando su estado original para restaurarlo con un clic.
*   **Gestor de Inicio:** Desactiva los programas que se abren solos al encender la PC sin borrarlos del registro (se mueven a una carpeta segura).
*   **Desinstalador de Bloatware:** Elimina apps basura preinstaladas (Noticias, Clima, Skype) con opción de restaurarlas desde la Microsoft Store.
*   **Diagnóstico de Red:** Mide tu Packet Loss, Jitter y Bufferbloat real para decirte si el problema de tu ping está en tu PC o en tu router.
