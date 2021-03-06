using Hideez.SDK.Communication;
using Hideez.SDK.Communication.HES.Client;
using Hideez.SDK.Communication.HES.DTO;
using Hideez.SDK.Communication.Interfaces;
using Hideez.SDK.Communication.Log;
using HideezMiddleware.DeviceConnection.Workflow.Interfaces;
using HideezMiddleware.Localize;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HideezMiddleware.DeviceConnection.Workflow
{
    public class LicensingProcessor : Logger, ILicensingProcessor
    {
        readonly IClientUiManager _ui;
        readonly IHesAppConnection _hesConnection;

        public LicensingProcessor(IHesAppConnection hesConnection, IClientUiManager ui, ILog log)
            : base(nameof(LicensingProcessor), log)
        {
            _hesConnection = hesConnection;
            _ui = ui;
        }

        public async Task CheckLicense(IDevice device, HwVaultInfoFromHesDto vaultInfo, CancellationToken ct)
        {
            if ((device.AccessLevel.IsLinkRequired || vaultInfo.NeedUpdateLicense) && _hesConnection.State == HesConnectionState.Connected)
            {
                await _ui.SendNotification(TranslationSource.Instance["ConnectionFlow.License.UpdatingLicenseMessage"], device.Mac);

                IList<HwVaultLicenseDto> licenses;
                if (device.AccessLevel.IsLinkRequired)
                {
                    licenses = await _hesConnection.GetHwVaultLicenses(device.SerialNo, ct);
                    WriteLine($"Received {licenses.Count} TOTAL licenses from HES");
                }
                else
                {
                    licenses = await _hesConnection.GetNewHwVaultLicenses(device.SerialNo, ct);
                    WriteLine($"Received {licenses.Count} NEW licenses from HES");
                }

                ct.ThrowIfCancellationRequested();

                if (licenses.Count > 0)
                {
                    for (int i = 0; i < licenses.Count; i++)
                    {
                        var license = licenses[i];

                        ct.ThrowIfCancellationRequested();

                        if (license.Data == null)
                            throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.License.Error.EmptyLicenseData", device.SerialNo));

                        if (license.Id == null)
                            throw new WorkflowException(TranslationSource.Instance.Format("ConnectionFlow.License.Error.EmptyLicenseId", device.SerialNo));

                        try
                        {
                            await device.LoadLicense(license.Data, SdkConfig.DefaultCommandTimeout);
                            WriteLine($"Loaded license ({license.Id}) into vault ({device.SerialNo}) in available slot");
                        }
                        catch (HideezException ex) when (ex.ErrorCode == HideezErrorCode.ERR_DEVICE_LOCKED_BY_CODE || ex.ErrorCode == HideezErrorCode.ERR_DEVICE_LOCKED_BY_PIN)
                        { 
                            await device.LoadLicense(i, license.Data, SdkConfig.DefaultCommandTimeout);
                            WriteLine($"Loaded license ({license.Id}) into vault ({device.SerialNo}) into slot {i}");
                        }

                        await _hesConnection.OnHwVaultLicenseApplied(device.SerialNo, license.Id);
                    }
                }

                await device.RefreshDeviceInfo();

                ct.ThrowIfCancellationRequested();
            }

            if (device.LicenseInfo == 0)
            {
                if (_hesConnection.State == HesConnectionState.Connected)
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.License.Error.NoLicensesAvailable"]);
                else
                    throw new WorkflowException(TranslationSource.Instance["ConnectionFlow.License.Error.CannotDownloadLicense"]);
            }
        }
    }
}
