#define UNICODE
#define _UNICODE
#include <windows.h>
#include <setupapi.h>
#include <newdev.h>
#include <initguid.h>
#include <devguid.h>
#include <stdio.h>

/* Elevated helper embedded in the managed DLL. Uses only supported PnP APIs. */
static DWORD Install(const wchar_t* inf)
{
    HDEVINFO devices;
    SP_DEVINFO_DATA device;
    DWORD index, error = ERROR_SUCCESS;
    BOOL found = FALSE, created = FALSE, reboot = FALSE, stageReboot = FALSE;
    wchar_t ids[1024];
    const wchar_t hardwareId[] = L"Root\\XFEInputUmProbe\0";
    if (!DiInstallDriverW(NULL, inf, 0, &stageReboot)) return GetLastError();
    devices = SetupDiGetClassDevsW(&GUID_DEVCLASS_HIDCLASS, NULL, NULL, DIGCF_PRESENT);
    if (devices == INVALID_HANDLE_VALUE) return GetLastError();
    ZeroMemory(&device, sizeof(device));
    device.cbSize = sizeof(device);
    for (index = 0; SetupDiEnumDeviceInfo(devices, index, &device); ++index) {
        if (SetupDiGetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID,
            NULL, (BYTE*)ids, sizeof(ids), NULL)) {
            const wchar_t* id;
            ids[ARRAYSIZE(ids) - 1] = 0;
            for (id = ids; *id; id += wcslen(id) + 1)
                if (_wcsicmp(id, L"Root\\XFEInputUmProbe") == 0) { found = TRUE; break; }
            if (found) break;
        }
    }
    if (!found) {
        error = GetLastError();
        if (error != ERROR_NO_MORE_ITEMS) goto done;
        if (!SetupDiCreateDeviceInfoW(devices, L"XFEInputUmProbe", &GUID_DEVCLASS_HIDCLASS,
            L"XFE UMDF HID Probe", NULL, DICD_GENERATE_ID, &device) ||
            !SetupDiSetDeviceRegistryPropertyW(devices, &device, SPDRP_HARDWAREID,
                (const BYTE*)hardwareId, sizeof(hardwareId)) ||
            !SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devices, &device)) {
            error = GetLastError();
            goto done;
        }
        created = TRUE;
    }
    if (!UpdateDriverForPlugAndPlayDevicesW(NULL, L"Root\\XFEInputUmProbe", inf, INSTALLFLAG_FORCE, &reboot)) {
        error = GetLastError();
        if (created) (void)SetupDiCallClassInstaller(DIF_REMOVE, devices, &device);
        goto done;
    }
    error = (reboot || stageReboot) ? ERROR_SUCCESS_REBOOT_REQUIRED : ERROR_SUCCESS;
done:
    SetupDiDestroyDeviceInfoList(devices);
    return error;
}

int wmain(int argc, wchar_t** argv)
{
    DWORD error;
    DWORD wait;
    HANDLE mutex;
    if (argc != 3 || wcscmp(argv[1], L"install") != 0) {
        fwprintf(stderr, L"Usage: XfeInputUmProbeSetup.exe install <full-path-to-XfeInputUmProbe.inf>\n");
        return ERROR_BAD_ARGUMENTS;
    }
    mutex = CreateMutexW(NULL, FALSE, L"Global\\XFEInputUmProbe.DriverSetup.v1");
    if (!mutex) return (int)GetLastError();
    wait = WaitForSingleObject(mutex, 120000);
    if (wait != WAIT_OBJECT_0 && wait != WAIT_ABANDONED) {
        error = wait == WAIT_TIMEOUT ? ERROR_TIMEOUT : GetLastError();
        CloseHandle(mutex);
        return (int)error;
    }
    error = Install(argv[2]);
    ReleaseMutex(mutex);
    CloseHandle(mutex);
    if (error) fwprintf(stderr, L"Driver installation returned Win32 error %lu.\n", error);
    return (int)error;
}
