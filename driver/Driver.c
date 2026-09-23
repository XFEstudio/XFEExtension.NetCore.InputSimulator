#include <windows.h>
#include <wdf.h>
#include <hidport.h>
#include "HidDescriptor.h"
#include "HidControl.h"

DRIVER_INITIALIZE DriverEntry;
EVT_WDF_DRIVER_DEVICE_ADD XfeDeviceAdd;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL XfeDeviceControl;
EVT_WDF_DEVICE_D0_ENTRY XfeD0Entry;
EVT_WDF_DEVICE_D0_EXIT XfeD0Exit;
EVT_WDF_TIMER XfeWatchdog;

typedef struct XFE_DEVICE {
    WDFQUEUE Reads;
    WDFWAITLOCK Lock;
    WDFTIMER Watchdog;
    BOOLEAN Online;
    XFE_CONTROL_STATE Control;
} XFE_DEVICE;
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(XFE_DEVICE, XfeDevice);

static const BYTE ControlDescriptor[] = {
    0x06,0x00,0xFF, 0x09,0x01, 0xA1,0x01, 0x85,0x03,
    0x15,0x00, 0x26,0xFF,0x00, 0x75,0x08, 0x95,0x3F,
    0x09,0x01, 0xB1,0x02, 0xC0
};
static BYTE ReportDescriptor[sizeof(XfeReportDescriptor) + sizeof(ControlDescriptor)];

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

/* Caller holds the device lock. Reports stay queued until a HID read accepts them. */
static void Drain(XFE_DEVICE* context)
{
    const XFE_PENDING_REPORT* pending;
    WDFREQUEST request;
    NTSTATUS status;
    while (context->Online && (pending = XfeControlPeek(&context->Control)) != NULL) {
        if (!NT_SUCCESS(WdfIoQueueRetrieveNextRequest(context->Reads, &request))) break;
        status = CopyOutput(request, &pending->Report, pending->Length);
        if (NT_SUCCESS(status)) XfeControlPop(&context->Control);
        WdfRequestComplete(request, status);
    }
}

NTSTATUS DriverEntry(PDRIVER_OBJECT driver, PUNICODE_STRING registry)
{
    WDF_DRIVER_CONFIG config;
    CopyMemory(ReportDescriptor, XfeReportDescriptor, sizeof(XfeReportDescriptor));
    CopyMemory(ReportDescriptor + sizeof(XfeReportDescriptor), ControlDescriptor, sizeof(ControlDescriptor));
    WDF_DRIVER_CONFIG_INIT(&config, XfeDeviceAdd);
    return WdfDriverCreate(driver, registry, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
}

NTSTATUS XfeDeviceAdd(WDFDRIVER driver, PWDFDEVICE_INIT init)
{
    WDFDEVICE device;
    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_IO_QUEUE_CONFIG queue;
    WDF_PNPPOWER_EVENT_CALLBACKS power;
    WDF_TIMER_CONFIG timer;
    XFE_DEVICE* context;
    NTSTATUS status;
    UNREFERENCED_PARAMETER(driver);
    WdfFdoInitSetFilter(init);
    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&power);
    power.EvtDeviceD0Entry = XfeD0Entry;
    power.EvtDeviceD0Exit = XfeD0Exit;
    WdfDeviceInitSetPnpPowerEventCallbacks(init, &power);
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attributes, XFE_DEVICE);
    attributes.ExecutionLevel = WdfExecutionLevelPassive;
    status = WdfDeviceCreate(&init, &attributes, &device);
    if (!NT_SUCCESS(status)) return status;
    context = XfeDevice(device);
    XfeControlInitialize(&context->Control);
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = device;
    status = WdfWaitLockCreate(&attributes, &context->Lock);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT(&queue, WdfIoQueueDispatchManual);
    queue.PowerManaged = WdfFalse;
    status = WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, &context->Reads);
    if (!NT_SUCCESS(status)) return status;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queue, WdfIoQueueDispatchSequential);
    queue.PowerManaged = WdfFalse;
    queue.EvtIoDeviceControl = XfeDeviceControl;
    status = WdfIoQueueCreate(device, &queue, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
    if (!NT_SUCCESS(status)) return status;
    WDF_TIMER_CONFIG_INIT_PERIODIC(&timer, XfeWatchdog, 100);
    timer.AutomaticSerialization = FALSE;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);
    attributes.ParentObject = device;
    return WdfTimerCreate(&timer, &attributes, &context->Watchdog);
}

NTSTATUS XfeD0Entry(WDFDEVICE device, WDF_POWER_DEVICE_STATE previous)
{
    XFE_DEVICE* context = XfeDevice(device);
    UNREFERENCED_PARAMETER(previous);
    WdfWaitLockAcquire(context->Lock, NULL);
    XfeControlInitialize(&context->Control);
    context->Online = TRUE;
    Drain(context);
    WdfWaitLockRelease(context->Lock);
    WdfTimerStart(context->Watchdog, WDF_REL_TIMEOUT_IN_MS(100));
    return STATUS_SUCCESS;
}

NTSTATUS XfeD0Exit(WDFDEVICE device, WDF_POWER_DEVICE_STATE target)
{
    XFE_DEVICE* context = XfeDevice(device);
    UNREFERENCED_PARAMETER(target);
    WdfTimerStop(context->Watchdog, TRUE);
    WdfWaitLockAcquire(context->Lock, NULL);
    XfeControlInitialize(&context->Control);
    Drain(context);
    context->Online = FALSE;
    WdfWaitLockRelease(context->Lock);
    return STATUS_SUCCESS;
}

void XfeWatchdog(WDFTIMER timer)
{
    XFE_DEVICE* context = XfeDevice((WDFDEVICE)WdfTimerGetParentObject(timer));
    WdfWaitLockAcquire(context->Lock, NULL);
    XfeControlExpire(&context->Control, GetTickCount64());
    Drain(context);
    WdfWaitLockRelease(context->Lock);
}

static NTSTATUS GetString(WDFREQUEST request)
{
    ULONG* input;
    NTSTATUS status = WdfRequestRetrieveInputBuffer(request, sizeof(ULONG), (void**)&input, NULL);
    if (!NT_SUCCESS(status)) return status;
    switch (*input & 0xFFFF) {
    case HID_STRING_ID_IMANUFACTURER: return CopyOutput(request, L"XFEstudio", sizeof(L"XFEstudio"));
    case HID_STRING_ID_IPRODUCT: return CopyOutput(request, L"XFE UMDF Keyboard and Mouse", sizeof(L"XFE UMDF Keyboard and Mouse"));
    case HID_STRING_ID_ISERIALNUMBER: return CopyOutput(request, L"XFE-UMDF-INPUT-1", sizeof(L"XFE-UMDF-INPUT-1"));
    default: return STATUS_INVALID_PARAMETER;
    }
}

void XfeDeviceControl(WDFQUEUE queue, WDFREQUEST request, size_t outputLength, size_t inputLength, ULONG ioctl)
{
    XFE_DEVICE* context = XfeDevice(WdfIoQueueGetDevice(queue));
    NTSTATUS status = STATUS_NOT_SUPPORTED;
    void* input;
    UNREFERENCED_PARAMETER(inputLength);
    WdfWaitLockAcquire(context->Lock, NULL);
    XfeControlExpire(&context->Control, GetTickCount64());
    switch (ioctl) {
    case IOCTL_HID_GET_DEVICE_DESCRIPTOR: {
        const HID_DESCRIPTOR descriptor = { sizeof(HID_DESCRIPTOR), HID_HID_DESCRIPTOR_TYPE,
            0x0111, 0, 1, {{ HID_REPORT_DESCRIPTOR_TYPE, sizeof(ReportDescriptor) }} };
        status = CopyOutput(request, &descriptor, sizeof(descriptor));
        break;
    }
    case IOCTL_HID_GET_REPORT_DESCRIPTOR:
        status = CopyOutput(request, ReportDescriptor, sizeof(ReportDescriptor)); break;
    case IOCTL_HID_GET_DEVICE_ATTRIBUTES: {
        HID_DEVICE_ATTRIBUTES attributes = { 0 };
        attributes.Size = sizeof(attributes);
        attributes.VendorID = 0xFEFE;
        attributes.ProductID = 0x0003;
        attributes.VersionNumber = 1;
        status = CopyOutput(request, &attributes, sizeof(attributes));
        break;
    }
    case IOCTL_HID_GET_STRING: status = GetString(request); break;
    case IOCTL_HID_READ_REPORT:
        status = WdfRequestForwardToIoQueue(request, context->Reads);
        if (NT_SUCCESS(status)) {
            Drain(context);
            WdfWaitLockRelease(context->Lock);
            return;
        }
        break;
    case IOCTL_UMDF_HID_GET_FEATURE:
        status = WdfRequestRetrieveInputBuffer(request, 1, &input, NULL);
        if (NT_SUCCESS(status)) {
            if (*(BYTE*)input == XFE_CONTROL_REPORT_ID) {
                XFE_CONTROL_INFO info;
                XfeControlGetInfo(&context->Control, &info);
                status = CopyOutput(request, &info, sizeof(info));
            } else status = STATUS_INVALID_PARAMETER;
        }
        break;
    case IOCTL_UMDF_HID_SET_FEATURE: {
        size_t length;
        status = WdfRequestRetrieveInputBuffer(request, sizeof(XFE_CONTROL_PACKET), &input, &length);
        if (!NT_SUCCESS(status)) break;
        if (length != sizeof(XFE_CONTROL_PACKET) || outputLength != XFE_CONTROL_REPORT_ID) {
            status = STATUS_INVALID_PARAMETER; break;
        }
        if (!context->Online) { status = STATUS_DEVICE_NOT_READY; break; }
        Drain(context);
        switch (XfeControlApply(&context->Control, (const XFE_CONTROL_PACKET*)input, GetTickCount64())) {
        case XfeControlSuccess: status = STATUS_SUCCESS; break;
        case XfeControlBusy: status = STATUS_DEVICE_BUSY; break;
        case XfeControlNotOwner: status = STATUS_INVALID_DEVICE_STATE; break;
        default: status = STATUS_INVALID_PARAMETER; break;
        }
        break;
    }
    case IOCTL_UMDF_HID_GET_INPUT_REPORT:
        status = WdfRequestRetrieveInputBuffer(request, 1, &input, NULL);
        if (NT_SUCCESS(status)) {
            if (*(BYTE*)input == 1) status = CopyOutput(request, &context->Control.Input.Keyboard, sizeof(XFE_KEYBOARD_REPORT));
            else if (*(BYTE*)input == 2) {
                XFE_MOUSE_REPORT mouse = { 0 };
                mouse.Id = 2;
                mouse.Buttons = context->Control.Input.Buttons;
                status = CopyOutput(request, &mouse, sizeof(mouse));
            } else status = STATUS_INVALID_PARAMETER;
        }
        break;
    case IOCTL_HID_WRITE_REPORT:
    case IOCTL_UMDF_HID_SET_OUTPUT_REPORT:
        status = WdfRequestRetrieveInputBuffer(request, 2, &input, NULL);
        if (NT_SUCCESS(status)) status = *(BYTE*)input == 1 ? STATUS_SUCCESS : STATUS_INVALID_PARAMETER;
        break;
    default: break;
    }
    Drain(context);
    WdfWaitLockRelease(context->Lock);
    WdfRequestComplete(request, status);
}
