using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class NagleTweak : ITweak
{
    private const string InterfacesPath = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public string Id => "network-disable-nagle";
    public string Name => "Desactivar Algoritmo de Nagle (Reducción de Latencia)";
    public string Description => "Reduce el ping en juegos online evitando que Windows retenga paquetes de datos antes de enviarlos. (Ideal para conexiones por cable).";
    public string Category => "Network";
    public RiskLevel Risk => RiskLevel.Moderate;
    public bool RequiresRestart => true;

    public bool Apply()
    {
        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(InterfacesPath, true);
            if (interfacesKey == null) return false;

            bool anyModified = false;
            foreach (var subKeyName in interfacesKey.GetSubKeyNames())
            {
                using var ifaceKey = interfacesKey.OpenSubKey(subKeyName, true);
                if (ifaceKey == null) continue;

                if (IsActiveInterface(ifaceKey))
                {
                    ifaceKey.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord);
                    ifaceKey.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord);
                    anyModified = true;
                }
            }

            return anyModified;
        }
        catch
        {
            return false;
        }
    }

    public bool Revert()
    {
        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(InterfacesPath, true);
            if (interfacesKey == null) return false;

            bool anyReverted = false;
            foreach (var subKeyName in interfacesKey.GetSubKeyNames())
            {
                using var ifaceKey = interfacesKey.OpenSubKey(subKeyName, true);
                if (ifaceKey == null) continue;

                bool hadAck = ifaceKey.GetValue("TcpAckFrequency") != null;
                bool hadDelay = ifaceKey.GetValue("TCPNoDelay") != null;

                if (hadAck) ifaceKey.DeleteValue("TcpAckFrequency", false);
                if (hadDelay) ifaceKey.DeleteValue("TCPNoDelay", false);

                if (hadAck || hadDelay) anyReverted = true;
            }

            return anyReverted;
        }
        catch
        {
            return false;
        }
    }

    public bool CheckIsApplied()
    {
        try
        {
            using var interfacesKey = Registry.LocalMachine.OpenSubKey(InterfacesPath, false);
            if (interfacesKey == null) return false;

            bool foundActiveApplied = false;
            bool foundAnyApplied = false;

            foreach (var subKeyName in interfacesKey.GetSubKeyNames())
            {
                using var ifaceKey = interfacesKey.OpenSubKey(subKeyName, false);
                if (ifaceKey == null) continue;

                bool isAckOne = IsValueOne(ifaceKey.GetValue("TcpAckFrequency"));
                bool isDelayOne = IsValueOne(ifaceKey.GetValue("TCPNoDelay"));

                if (isAckOne && isDelayOne)
                {
                    foundAnyApplied = true;
                    if (IsActiveInterface(ifaceKey))
                    {
                        foundActiveApplied = true;
                    }
                }
            }

            return foundActiveApplied || foundAnyApplied;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValueOne(object? val)
    {
        if (val is int intVal) return intVal == 1;
        if (val != null && int.TryParse(val.ToString(), out int parsed)) return parsed == 1;
        return false;
    }

    private static bool IsActiveInterface(RegistryKey ifaceKey)
    {
        // 1. Verificar DHCP IP
        var dhcpIp = ifaceKey.GetValue("DhcpIPAddress") as string;
        if (!string.IsNullOrWhiteSpace(dhcpIp) && dhcpIp != "0.0.0.0") return true;

        // 2. Verificar IP estática
        var ipObj = ifaceKey.GetValue("IPAddress");
        if (ipObj is string[] ipArray && ipArray.Length > 0 && !string.IsNullOrWhiteSpace(ipArray[0]) && ipArray[0] != "0.0.0.0") return true;
        if (ipObj is string ipStr && !string.IsNullOrWhiteSpace(ipStr) && ipStr != "0.0.0.0") return true;

        // 3. Verificar puerta de enlace activa
        var gateway = ifaceKey.GetValue("DhcpDefaultGateway") ?? ifaceKey.GetValue("DefaultGateway");
        if (gateway is string[] gwArray && gwArray.Length > 0 && !string.IsNullOrWhiteSpace(gwArray[0])) return true;
        if (gateway is string gwStr && !string.IsNullOrWhiteSpace(gwStr)) return true;

        return false;
    }
}
