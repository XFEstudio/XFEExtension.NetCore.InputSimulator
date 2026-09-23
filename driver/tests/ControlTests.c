#include "../HidControl.h"
#include <assert.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>

static XFE_CONTROL_PACKET Packet(uint8_t operation, uint64_t session)
{
    XFE_CONTROL_PACKET packet = { 0 };
    packet.Id = XFE_CONTROL_REPORT_ID;
    packet.Version = XFE_PROTOCOL_VERSION;
    packet.Operation = operation;
    packet.Session = session;
    return packet;
}

static void Drain(XFE_CONTROL_STATE* state) { while (XfeControlPeek(state)) XfeControlPop(state); }

int main(void)
{
    XFE_CONTROL_STATE state, before;
    XFE_CONTROL_PACKET packet;
    XFE_CONTROL_INFO info;
    uint32_t i;
    assert(sizeof(XFE_CONTROL_PACKET) == 64 && sizeof(XFE_CONTROL_INFO) == 64);
    assert(offsetof(XFE_CONTROL_PACKET, Command) == 16 && offsetof(XFE_CONTROL_INFO, Protocol) == 8);
    XfeControlInitialize(&state);
    assert(state.Count == 2 && XfeControlPeek(&state)->Report.Keyboard.Id == 1);
    XfeControlPop(&state);
    assert(XfeControlPeek(&state)->Report.Mouse.Id == 2);
    Drain(&state);
    packet = Packet(XFE_SEND, 1);
    assert(XfeControlApply(&state, &packet, 10) == XfeControlNotOwner);
    packet = Packet(XFE_ACQUIRE, 1);
    assert(XfeControlApply(&state, &packet, 10) == XfeControlSuccess);
    Drain(&state);
    packet = Packet(XFE_ACQUIRE, 2);
    assert(XfeControlApply(&state, &packet, 10) == XfeControlBusy);
    packet = Packet(XFE_SEND, 1);
    packet.Command.Version = XFE_PROTOCOL_VERSION;
    packet.Command.Kind = XFE_KEY;
    packet.Command.Code = 0x1A;
    assert(XfeControlApply(&state, &packet, 20) == XfeControlSuccess);
    assert(XfeControlPeek(&state)->Length == sizeof(XFE_KEYBOARD_REPORT));
    assert(XfeControlPeek(&state)->Report.Keyboard.Keys[5] == 0x1A);
    Drain(&state);
    packet.Command.Flags = 1;
    assert(XfeControlApply(&state, &packet, 21) == XfeControlSuccess);
    assert(XfeControlPeek(&state)->Report.Keyboard.Keys[5] == 0);
    Drain(&state);
    packet.Command.Kind = XFE_MOVE;
    packet.Command.Code = packet.Command.Flags = 0;
    packet.Command.X = 1;
    for (i = 0; i < XFE_QUEUE_CAPACITY; ++i)
        assert(XfeControlApply(&state, &packet, 30 + i) == XfeControlSuccess);
    before = state;
    packet.Command.Kind = XFE_BUTTON;
    packet.Command.X = 0;
    assert(XfeControlApply(&state, &packet, 100) == XfeControlBusy);
    assert(memcmp(&state, &before, sizeof(state)) == 0);
    Drain(&state);
    assert(XfeControlApply(&state, &packet, 110) == XfeControlSuccess && state.Input.Buttons == 1);
    XfeControlExpire(&state, 3109);
    assert(state.Session == 1);
    XfeControlExpire(&state, 3110);
    assert(state.Session == 0 && state.Input.Buttons == 0 && state.Count == 2);
    packet = Packet(XFE_HEARTBEAT, 1);
    assert(XfeControlApply(&state, &packet, 3111) == XfeControlNotOwner);
    packet = Packet(XFE_ACQUIRE, 2);
    assert(XfeControlApply(&state, &packet, 3111) == XfeControlSuccess);
    before = state;
    packet.Reserved[2] = 1;
    assert(XfeControlApply(&state, &packet, 3112) == XfeControlInvalid);
    assert(memcmp(&state, &before, sizeof(state)) == 0);
    packet = Packet(XFE_RESET, 1);
    assert(XfeControlApply(&state, &packet, 3112) == XfeControlNotOwner);
    packet = Packet(XFE_RELEASE, 2);
    assert(XfeControlApply(&state, &packet, 3112) == XfeControlSuccess);
    assert(state.Session == 0 && state.Count == 2);
    XfeControlGetInfo(&state, &info);
    assert(info.Magic == XFE_CONTROL_MAGIC && info.Protocol.Version == 1 && !info.LeaseActive);
    puts("HID control tests passed: ABI, ownership, ordering, backpressure, watchdog, validation, reset.");
    return 0;
}
