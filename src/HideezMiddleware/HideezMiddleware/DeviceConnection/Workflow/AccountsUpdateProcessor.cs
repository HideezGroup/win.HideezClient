using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class AccountsUpdateProcessor : Logger, IAccountsUpdateProcessor
    {
        readonly IHesAppConnection _hesConnection;

        public AccountsUpdateProcessor(IHesAppConnection hesConnection, ILog log)
            : base(nameof(AccountsUpdateProcessor), log)
        {
            _hesConnection = hesConnection;
        }

        public async Task UpdateAccounts(IDevice device, HwVaultInfoFromHesDto vaultInfo, bool onlyOsAccounts)
        {
            if (_hesConnection.State == HesConnectionState.Connected)
            {
                if ((vaultInfo.NeedUpdateOSAccounts && onlyOsAccounts) || (vaultInfo.NeedUpdateNonOSAccounts && !onlyOsAccounts))
                {
                    await _hesConnection.UpdateHwVaultAccounts(device.SerialNo, onlyOsAccounts);
                    await device.RefreshDeviceInfo();
                }
            }
        }
    }
}
