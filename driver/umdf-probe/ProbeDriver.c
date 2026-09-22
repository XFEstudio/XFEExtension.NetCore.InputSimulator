/* Feasibility probe only: enumerates HID collections and answers a read-only
   feature request. It cannot send keyboard or mouse input. */
#include <windows.h>
#include <wdf.h>
#include <hidport.h>
#include "../HidDescriptor.h"

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD ProbeDeviceAdd;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL ProbeDeviceControl;

typedef struct PROBE_CONTEXT { WDFQUEUE Reads; } PROBE_CONTEXT;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(PROBE_CONTEXT, ProbeContext);

static const BYTE ControlDescriptor[] = {
    0x06,0x00,0xFF, 0x09,0x01, 0xA1,0x01, 0x85,0x03,
    0x15,0x00, 0x26,0xFF,0x00, 0x75,0x08, 0x95,0x10,
    0x09,0x01, 0xB1,0x02, 0xC0
};
static BYTE ReportDescriptor[sizeof(XfeReportDescriptor) + sizeof(ControlDescriptor)];
static const BYTE ProbeFeature[17] = { 3, 'X','F','E','-','U','M','D','F','-','P','R','O','B','E','1',0 };

static NTSTATUS CopyOutput(WDFREQUEST request, const void* source, size_t length)
{
    void* output;
    NTSTATUS status = WdfRequestRetrieveOutputBuffer(request, length, &output, NULL);
    if (NT_SUCCESS(status)) {
        CopyMemory(output, source, length);
        WdfRequestSetInformation(request, length);
    }
    return status;
}

NTSTATUS DriverEntry(PDRIVER_OBJECT driver, PUNICODE_STRING registry)
{
    WDF_DRIVER_CONFIG config;
    CopyMemory(ReportDescriptor, XfeReportDescriptor, sizeof(XfeReportDescriptor));
    CopyMemory(ReportDescriptor + sizeof(XfeReportDescriptor), ControlDescriptor, sizeof(ControlDescriptor));
    WDF_DRIVER_CONFIG_INIT(&config, ProbeDeviceAdd);
    return WdfDriverCreate(driver, registry, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS ProbeDeviceAdd(WDFDRIVER driver, PWDFDEVICE_INIT init)
{
    WDFDEVICE device;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_IO_QUEUE_CONFIG queue;
    NTSTATUS status;
    UNREFERENCED_PARAMETER(driver);
    WdfFdoInitSetFilter(init);
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, PROBE_CONTEXT);
    status = WdfDeviceCreate(&init, &attributes, &device);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT(&queue, WdfIoQueueDispatchManual);
    queue.PowerManaged = WdfFalse;
    status = WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, &ProbeContext(device)->Reads);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue, WdfIoQueueDispatchSequential);
    queue.PowerManaged = WdfFalse;
    queue.EvtIoDeviceControl = ProbeDeviceControl;
    return WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
}

void ProbeDeviceControl(WDFQUEUE queue, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG ioctl)
{
    NTSTATUS status = STATUS_NOT_SUPPORTED;
    void* input;
    UNREFERENCED_PARAMETER(outputLength);
    UNREFERENCED_PARAMETER(inputLength);
    switch (ioctl) {
    case IOCTL_HID_GET_DEVICE_DESCRIPTOR: {
        const HID_DESCRIPTOR descriptor = { sizeof(HID_DESCRIPTOR), HID_HID_DESCRIPTOR_TYPE,
            0x0111, 0, 1, {{ HID_REPORT_DESCRIPTOR_TYPE, sizeof(ReportDescriptor) }} };
        status = CopyOutput(request, &descriptor, sizeof(descriptor));
        break;
    }
    case IOCTL_HID_GET_REPORT_DESCRIPTOR:
        status = CopyOutput(request, ReportDescriptor, sizeof(ReportDescriptor));
        break;
    case IOCTL_HID_GET_DEVICE_ATTRIBUTES: {
        HID_DEVICE_ATTRIBUTES attributes = { 0 };
        attributes.Size = sizeof(attributes);
        attributes.VendorID = 0xFEFE;
        attributes.ProductID = 0x0002;
        attributes.VersionNumber = 1;
        status = CopyOutput(request, &attributes, sizeof(attributes));
        break;
    }
    case IOCTL_HID_GET_STRING:
        status = WdfRequestRetrieveInputBuffer(request, sizeof(ULONG), &input, NULL);
        if (NT_SUCCESS(status)) {
            switch (*(ULONG*)input & 0xFFFF) {
            case HID_STRING_ID_IMANUFACTURER:
                status = CopyOutput(request, L"XFEstudio", sizeof(L"XFEstudio")); break;
            case HID_STRING_ID_IPRODUCT:
                status = CopyOutput(request, L"XFE UMDF HID Probe", sizeof(L"XFE UMDF HID Probe")); break;
            case HID_STRING_ID_ISERIALNUMBER:
                status = CopyOutput(request, L"XFE-UMDF-PROBE1", sizeof(L"XFE-UMDF-PROBE1")); break;
            default: status = STATUS_INVALID_PARAMETER; break;
            }
        }
        break;
    case IOCTL_UMDF_HID_GET_FEATURE:
        status = WdfRequestRetrieveInputBuffer(request, 1, &input, NULL);
        if (NT_SUCCESS(status))
            status = *(BYTE*)input == 3 ? CopyOutput(request, ProbeFeature, sizeof(ProbeFeature)) : STATUS_INVALID_PARAMETER;
        break;
    case IOCTL_HID_READ_REPORT:
        status = WdfRequestForwardToIoQueue(request, ProbeContext(WdfIoQueueGetDevice(queue))->Reads);
        if (NT_SUCCESS(status)) return; /* Framework cancels these requests on removal. */
        break;
    case IOCTL_UMDF_HID_GET_INPUT_REPORT:
        status = WdfRequestRetrieveInputBuffer(request, 1, &input, NULL);
        if (NT_SUCCESS(status)) {
            BYTE neutral[9] = { 0 };
            neutral[0] = *(BYTE*)input;
            status = neutral[0] == 1 || neutral[0] == 2
                ? CopyOutput(request, neutral, neutral[0] == 1 ? 9 : 8) : STATUS_INVALID_PARAMETER;
        }
        break;
    case IOCTL_HID_WRITE_REPORT:
    case IOCTL_UMDF_HID_SET_OUTPUT_REPORT:
        status = WdfRequestRetrieveInputBuffer(request, 2, &input, NULL);
        if (NT_SUCCESS(status)) status = *(BYTE*)input == 1 ? STATUS_SUCCESS : STATUS_INVALID_PARAMETER;
        break;
    default: break;
    }
    WdfRequestComplete(request, status);
}
