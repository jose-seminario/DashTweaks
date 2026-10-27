# DashTweaks - Script auxiliar de creación de puntos de restauración
[CmdletBinding()]
param (
    [string]$Description = "DashTweaks_Backup"
)

Write-Host "[DashTweaks] Verificando protección del sistema..." -ForegroundColor Cyan

try {
    # Habilitar protección si estuviera desactivada en la unidad del sistema
    $systemDrive = $env:SystemDrive
    Enable-ComputerRestore -Drive "$systemDrive\" -ErrorAction SilentlyContinue

    Write-Host "[DashTweaks] Generando punto de restauración '$Description'..." -ForegroundColor Yellow
    Checkpoint-Computer -Description $Description -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
    Write-Host "[DashTweaks] Punto de restauración creado exitosamente." -ForegroundColor Green
    exit 0
}
catch {
    Write-Warning "[DashTweaks] No se pudo crear el punto de restauración: $($_.Exception.Message)"
    exit 1
}
