using Microsoft.Win32;

namespace DashTweaks.Tweaks.Implementations;

public class NetworkAdvancedTweak : ITweak
{
    private const string SystemProfilePath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
    private const string DnsClientPath = @"SOFTWARE\Policies\Microsoft\Windows NT\DNSClient";

    // Respaldo en memoria de valores originales
    private object? _originalNetworkThrottlingIndex;
    private RegistryValueKind? _originalNetworkThrottlingIndexKind;
    private bool _hadNetworkThrottlingIndex;

    private object? _originalSystemResponsiveness;
    private RegistryValueKind? _originalSystemResponsivenessKind;
    private bool _hadSystemResponsiveness;

    private object? _originalEnableMulticast;
    private RegistryValueKind? _originalEnableMulticastKind;
    private bool _hadEnableMulticast;

    public string Id => "network-advanced-throttling";
    public string Name => "Optimización de Red y Responsividad (Desactivar Throttling)";
    public string Description => "Elimina la limitación de red de Windows (NetworkThrottlingIndex=0xFFFFFFFF), prioriza la interfaz (SystemResponsiveness=0) y deshabilita LLMNR para menor ping.";
    public string Category => "Network";
    public RiskLevel Risk => RiskLevel.Safe;
    public bool RequiresRestart => true;

    public bool Apply()
    {
        try
        {
            // 1. SystemProfile: NetworkThrottlingIndex & SystemResponsiveness
            using (var sysKey = Registry.LocalMachine.CreateSubKey(SystemProfilePath, true))
            {
                if (sysKey == null) return false;

                // Respaldar antes de modificar
                _originalNetworkThrottlingIndex = sysKey.GetValue("NetworkThrottlingIndex");
                _hadNetworkThrottlingIndex = _originalNetworkThrottlingIndex != null;
                if (_hadNetworkThrottlingIndex) _originalNetworkThrottlingIndexKind = sysKey.GetValueKind("NetworkThrottlingIndex");

                _originalSystemResponsiveness = sysKey.GetValue("SystemResponsiveness");
                _hadSystemResponsiveness = _originalSystemResponsiveness != null;
                if (_hadSystemResponsiveness) _originalSystemResponsivenessKind = sysKey.GetValueKind("SystemResponsiveness");

                // Aplicar: 0xFFFFFFFF (-1 en entero con signo) y 0
                sysKey.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                sysKey.SetValue("SystemResponsiveness", 0, RegistryValueKind.DWord);
            }

            // 2. DNSClient: EnableMulticast (Desactivar LLMNR)
            using (var dnsKey = Registry.LocalMachine.CreateSubKey(DnsClientPath, true))
            {
                if (dnsKey != null)
                {
                    _originalEnableMulticast = dnsKey.GetValue("EnableMulticast");
                    _hadEnableMulticast = _originalEnableMulticast != null;
                    if (_hadEnableMulticast) _originalEnableMulticastKind = dnsKey.GetValueKind("EnableMulticast");

                    dnsKey.SetValue("EnableMulticast", 0, RegistryValueKind.DWord);
                }
            }

            return true;
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
            // 1. Revertir SystemProfile
            using (var sysKey = Registry.LocalMachine.OpenSubKey(SystemProfilePath, true))
            {
                if (sysKey != null)
                {
                    if (_hadNetworkThrottlingIndex && _originalNetworkThrottlingIndex != null && _originalNetworkThrottlingIndexKind.HasValue)
                    {
                        sysKey.SetValue("NetworkThrottlingIndex", _originalNetworkThrottlingIndex, _originalNetworkThrottlingIndexKind.Value);
                    }
                    else
                    {
                        sysKey.DeleteValue("NetworkThrottlingIndex", false);
                    }

                    if (_hadSystemResponsiveness && _originalSystemResponsiveness != null && _originalSystemResponsivenessKind.HasValue)
                    {
                        sysKey.SetValue("SystemResponsiveness", _originalSystemResponsiveness, _originalSystemResponsivenessKind.Value);
                    }
                    else
                    {
                        sysKey.DeleteValue("SystemResponsiveness", false);
                    }
                }
            }

            // 2. Revertir DNSClient
            using (var dnsKey = Registry.LocalMachine.OpenSubKey(DnsClientPath, true))
            {
                if (dnsKey != null)
                {
                    if (_hadEnableMulticast && _originalEnableMulticast != null && _originalEnableMulticastKind.HasValue)
                    {
                        dnsKey.SetValue("EnableMulticast", _originalEnableMulticast, _originalEnableMulticastKind.Value);
                    }
                    else
                    {
                        dnsKey.DeleteValue("EnableMulticast", false);
                    }
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
