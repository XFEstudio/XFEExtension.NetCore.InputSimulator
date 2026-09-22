#include "HidControl.h"
#include <string.h>

static void Enqueue(XFE_CONTROL_STATE* state, const XFE_REPORT* report, uint32_t length)
{
    XFE_PENDING_REPORT* target = &state->Queue[(state->Head + state->Count) % XFE_QUEUE_CAPACITY];
    target->Report = *report;
    target->Length = length;
    ++state->Count;
}

static void Neutralize(XFE_CONTROL_STATE* state)
{
    XFE_REPORT report = { 0 };
    state->Head = state->Count = 0;
    XfeClearState(&state->Input);
    report.Keyboard = state->Input.Keyboard;
    Enqueue(state, &report, sizeof(XFE_KEYBOARD_REPORT));
    memset(&report, 0, sizeof(report));
    report.Mouse.Id = 2;
    Enqueue(state, &report, sizeof(XFE_MOUSE_REPORT));
}

void XfeControlInitialize(XFE_CONTROL_STATE* state)
{
    memset(state, 0, sizeof(*state));
    Neutralize(state);
}

void XfeControlExpire(XFE_CONTROL_STATE* state, uint64_t now)
{
    if (state->Session && now - state->LastHeartbeat >= XFE_LEASE_TIMEOUT_MS) {
        state->Session = 0;
        Neutralize(state);
    }
}

void XfeControlGetInfo(const XFE_CONTROL_STATE* state, XFE_CONTROL_INFO* info)
{
    memset(info, 0, sizeof(*info));
    info->Id = XFE_CONTROL_REPORT_ID;
    info->Magic = XFE_CONTROL_MAGIC;
    info->Protocol.Version = XFE_PROTOCOL_VERSION;
    info->Protocol.Capabilities = XFE_CAPABILITIES;
    info->Protocol.MaxKeys = XFE_MAX_KEYS;
    info->LeaseTimeoutMs = XFE_LEASE_TIMEOUT_MS;
    info->QueueCapacity = XFE_QUEUE_CAPACITY;
    info->QueuedReports = state->Count;
    info->LeaseActive = state->Session != 0;
}

enum XFE_CONTROL_RESULT XfeControlApply(XFE_CONTROL_STATE* state, const XFE_CONTROL_PACKET* packet, uint64_t now)
{
    static const XFE_COMMAND empty = { 0 };
    uint32_t i;
    XFE_STATE next;
    XFE_REPORT report = { 0 };
    uint32_t length;
    if (packet->Id != XFE_CONTROL_REPORT_ID || packet->Reserved0 ||
        packet->Version != XFE_PROTOCOL_VERSION || !packet->Session ||
        packet->Operation < XFE_ACQUIRE || packet->Operation > XFE_RELEASE) return XfeControlInvalid;
    for (i = 0; i < 4; ++i) if (packet->Reserved[i]) return XfeControlInvalid;
    if (packet->Operation != XFE_SEND && memcmp(&packet->Command, &empty, sizeof(empty))) return XfeControlInvalid;
    XfeControlExpire(state, now);
    if (packet->Operation == XFE_ACQUIRE) {
        if (state->Session && state->Session != packet->Session) return XfeControlBusy;
        if (!state->Session) Neutralize(state);
        state->Session = packet->Session;
    } else {
        if (state->Session != packet->Session) return XfeControlNotOwner;
        if (packet->Operation == XFE_SEND) {
            if (XfeEncode(&state->Input, &packet->Command, &next, &report, &length) != XfeSuccess) return XfeControlInvalid;
            if (state->Count == XFE_QUEUE_CAPACITY) return XfeControlBusy;
            Enqueue(state, &report, length);
            state->Input = next;
        } else if (packet->Operation == XFE_RESET || packet->Operation == XFE_RELEASE) {
            Neutralize(state);
            if (packet->Operation == XFE_RELEASE) state->Session = 0;
        }
    }
    state->LastHeartbeat = now;
    return XfeControlSuccess;
}

const XFE_PENDING_REPORT* XfeControlPeek(const XFE_CONTROL_STATE* state)
{
    return state->Count ? &state->Queue[state->Head] : NULL;
}

void XfeControlPop(XFE_CONTROL_STATE* state)
{
    if (state->Count) { state->Head = (state->Head + 1) % XFE_QUEUE_CAPACITY; --state->Count; }
}
