#define UNICODE
#define _UNICODE
#include <windows.h>
#include <setupapi.h>
#include <hidsdi.h>
#include <hidpi.h>
#include <stdio.h>

/* Enumerates the probe's vendor-defined collection and reads one feature.
   This program has no input-writing operation. */
int wmain(void)
{
    GUID hid;
    HDEVINFO devices;
    SP_DEVICE_INTERFACE_DATA item = { 0 };
    DWORD index;
    int result = ERROR_NOT_FOUND;
    HidD_GetHidGuid(&hid);
    devices = SetupDiGetClassDevsW(&hid, NULL, NULL, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);
    if (devices == INVALID_HANDLE_VALUE) return (int)GetLastError();
    item.cbSize = sizeof(item);
    for (index = 0; SetupDiEnumDeviceInterfaces(devices, NULL, &hid, index, &item); ++index) {
        DWORD length = 0;
        PSP_DEVICE_INTERFACE_DETAIL_DATA_W detail;
        HANDLE handle;
        HIDD_ATTRIBUTES attributes = { 0 };
        PHIDP_PREPARSED_DATA preparsed;
        HIDP_CAPS caps;
        BYTE feature[17] = { 3 };
        SetupDiGetDeviceInterfaceDetailW(devices, &item, NULL, 0, &length, NULL);
        if (length < sizeof(*detail)) continue;
        detail = (PSP_DEVICE_INTERFACE_DETAIL_DATA_W)HeapAlloc(GetProcessHeap(), HEAP_ZERO_MEMORY, length);
        if (!detail) { result = ERROR_OUTOFMEMORY; break; }
        detail->cbSize = sizeof(*detail);
        if (!SetupDiGetDeviceInterfaceDetailW(devices, &item, detail, length, NULL, NULL)) {
            HeapFree(GetProcessHeap(), 0, detail); continue;
        }
        handle = CreateFileW(detail->DevicePath, 0, FILE_SHARE_READ | FILE_SHARE_WRITE,
            NULL, OPEN_EXISTING, 0, NULL);
        HeapFree(GetProcessHeap(), 0, detail);
        if (handle == INVALID_HANDLE_VALUE) continue;
        attributes.Size = sizeof(attributes);
        if (HidD_GetAttributes(handle, &attributes) && attributes.VendorID == 0xFEFE &&
            attributes.ProductID == 0x0002 && HidD_GetPreparsedData(handle, &preparsed)) {
            NTSTATUS status = HidP_GetCaps(preparsed, &caps);
            HidD_FreePreparsedData(preparsed);
            if (status == HIDP_STATUS_SUCCESS && caps.UsagePage == 0xFF00 && caps.Usage == 1 &&
                caps.FeatureReportByteLength == sizeof(feature)) {
                if (HidD_GetFeature(handle, feature, sizeof(feature)) &&
                    memcmp(feature + 1, "XFE-UMDF-PROBE1", sizeof("XFE-UMDF-PROBE1")) == 0) {
                    puts("UMDF probe loaded: vendor HID feature response verified. No input was sent.");
                    result = ERROR_SUCCESS;
                } else {
                    result = (int)GetLastError();
                    if (!result) result = ERROR_INVALID_DATA;
                    fwprintf(stderr, L"Probe feature failed: %d\n", result);
                }
            }
        }
        CloseHandle(handle);
        if (result == ERROR_SUCCESS) break;
    }
    SetupDiDestroyDeviceInfoList(devices);
    if (result == ERROR_NOT_FOUND) fputs("The UMDF probe is not installed or not started.\n", stderr);
    return result;
}
